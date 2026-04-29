using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SupermarketPOS.Business.Workflow;
using SupermarketPOS.Core.Metadata;
using SupermarketPOS.Core.Observability;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace SupermarketPOS.Business.Observability
{
    /// <summary>
    /// Phase 5, Step 7: Safe replay of failed outbox events and workflow executions.
    /// </summary>
    public sealed class ReplayService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly WorkflowEngine _workflowEngine;
        private readonly IGenericDataService _dataService;

        public ReplayService(
            Func<AppDbContext> dbFactory,
            WorkflowEngine workflowEngine,
            IGenericDataService dataService)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _workflowEngine = workflowEngine;
            _dataService = dataService;
        }

        public async Task<ReplayResult> ReplayDeadLetterAsync(int deadLetterId)
        {
            var result = new ReplayResult { DeadLetterId = deadLetterId };

            try
            {
                var item = LoadDeadLetter(deadLetterId);
                if (item == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Dead letter event not found.";
                    return result;
                }

                using (CorrelationContext.BeginScope())
                {
                    result.CorrelationId = CorrelationContext.CorrelationId;

                    switch (item.SourceType)
                    {
                        case "OutboxEvent":
                            await ReplayOutboxEventAsync(item).ConfigureAwait(false);
                            break;
                        case "ScheduledJob":
                            await ReplayScheduledJobAsync(item).ConfigureAwait(false);
                            break;
                        default:
                            result.Success = false;
                            result.ErrorMessage = $"Unknown source type: {item.SourceType}";
                            return result;
                    }
                }

                MarkDeadLetterReplayed(deadLetterId);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<ReplayResult> ReplayWorkflowExecutionAsync(int workflowLogId)
        {
            var result = new ReplayResult { WorkflowLogId = workflowLogId };

            try
            {
                var log = LoadWorkflowLog(workflowLogId);
                if (log == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Workflow execution log not found.";
                    return result;
                }

                using (CorrelationContext.BeginScope())
                {
                    result.CorrelationId = CorrelationContext.CorrelationId;

                    await _workflowEngine.ExecuteAsync(
                        log.EntityName, log.Trigger, log.EntityId,
                        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
                        CancellationToken.None).ConfigureAwait(false);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private async Task ReplayOutboxEventAsync(DeadLetterItem item)
        {
            var payload = JsonConvert.DeserializeObject<JObject>(item.Payload);
            string entityName = payload?.Value<string>("EntityName");
            int? entityId = payload?.Value<int?>("EntityId");
            var data = payload?["Data"]?.ToObject<Dictionary<string, object>>();
            string eventType = payload?.Value<string>("EventType") ?? "MetadataEntityCreated";

            string trigger;
            switch (eventType)
            {
                case "MetadataEntityUpdated": trigger = "OnUpdate"; break;
                case "MetadataEntityDeleted": trigger = "OnDelete"; break;
                default: trigger = "OnCreate"; break;
            }

            if (!string.IsNullOrWhiteSpace(entityName))
            {
                await _workflowEngine.ExecuteAsync(entityName, trigger, entityId, data, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        private async Task ReplayScheduledJobAsync(DeadLetterItem item)
        {
            var payload = JsonConvert.DeserializeObject<JObject>(item.Payload);
            string entityName = payload?.Value<string>("EntityName");
            string actionName = payload?.Value<string>("ActionName");
            var parameters = payload?["Parameters"]?.ToObject<Dictionary<string, object>>();

            if (!string.IsNullOrWhiteSpace(entityName) && !string.IsNullOrWhiteSpace(actionName))
            {
                await _dataService.ExecuteActionAsync(entityName, actionName,
                    parameters ?? new Dictionary<string, object>()).ConfigureAwait(false);
            }
        }

        private DeadLetterItem LoadDeadLetter(int id)
        {
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open) conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT Id, SourceType, Payload FROM [DeadLetterEvents] WHERE Id = @id";
                        cmd.Parameters.Add(new SqlParameter("@id", id));

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new DeadLetterItem
                                {
                                    Id = reader.GetInt32(0),
                                    SourceType = reader.GetString(1),
                                    Payload = reader.GetString(2)
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        private WorkflowLogItem LoadWorkflowLog(int id)
        {
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open) conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT Id, WorkflowId, EntityName, [Trigger], EntityId
                            FROM [WorkflowExecutionLogs]
                            WHERE Id = @id";
                        cmd.Parameters.Add(new SqlParameter("@id", id));

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new WorkflowLogItem
                                {
                                    Id = reader.GetInt32(0),
                                    WorkflowId = reader.GetInt32(1),
                                    EntityName = reader.GetString(2),
                                    Trigger = reader.GetString(3),
                                    EntityId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        private void MarkDeadLetterReplayed(int id)
        {
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open) conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            UPDATE [DeadLetterEvents]
                            SET Status = 'Replayed', ReplayedAt = @now
                            WHERE Id = @id";
                        cmd.Parameters.Add(new SqlParameter("@now", DateTime.UtcNow));
                        cmd.Parameters.Add(new SqlParameter("@id", id));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception) { }
        }

        private class DeadLetterItem
        {
            public int Id { get; set; }
            public string SourceType { get; set; }
            public string Payload { get; set; }
        }

        private class WorkflowLogItem
        {
            public int Id { get; set; }
            public int WorkflowId { get; set; }
            public string EntityName { get; set; }
            public string Trigger { get; set; }
            public int? EntityId { get; set; }
        }
    }

    public class ReplayResult
    {
        public int? DeadLetterId { get; set; }
        public int? WorkflowLogId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string CorrelationId { get; set; }
    }
}
