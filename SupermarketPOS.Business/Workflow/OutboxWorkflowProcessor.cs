using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Phase 4.5, Step 8: Event-based automation.
    /// Polls OutboxEvents for EntityCreated/EntityUpdated events
    /// and triggers workflows asynchronously.
    /// </summary>
    public sealed class OutboxWorkflowProcessor
    {
        private const int BatchSize = 50;

        private readonly Func<AppDbContext> _dbFactory;
        private readonly WorkflowEngine _workflowEngine;
        private CancellationTokenSource _cts;
        private Task _runningTask;

        public OutboxWorkflowProcessor(
            Func<AppDbContext> dbFactory,
            WorkflowEngine workflowEngine)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        }

        public void Start()
        {
            if (_runningTask != null) return;
            _cts = new CancellationTokenSource();
            _runningTask = RunLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _runningTask = null;
        }

        private async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    int processed = await ProcessBatchAsync(cancellationToken).ConfigureAwait(false);
                    if (processed == 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception) { await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false); }
            }
        }

        private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
        {
            var events = LoadPendingEvents();
            foreach (var evt in events)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string trigger = MapEventToTrigger(evt.EventType);
                if (trigger == null)
                {
                    MarkProcessed(evt.Id, null);
                    continue;
                }

                try
                {
                    var payload = JsonConvert.DeserializeObject<JObject>(evt.Payload);
                    string entityName = payload?.Value<string>("EntityName");
                    int? entityId = payload?.Value<int?>("EntityId");
                    var data = payload?["Data"]?.ToObject<Dictionary<string, object>>();

                    if (!string.IsNullOrWhiteSpace(entityName))
                    {
                        await _workflowEngine.ExecuteAsync(entityName, trigger, entityId, data, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    MarkProcessed(evt.Id, null);
                }
                catch (Exception ex)
                {
                    MarkFailed(evt.Id, ex.Message);
                }
            }

            return events.Count;
        }

        private string MapEventToTrigger(string eventType)
        {
            if (string.IsNullOrWhiteSpace(eventType)) return null;

            switch (eventType)
            {
                case "MetadataEntityCreated": return "OnCreate";
                case "MetadataEntityUpdated": return "OnUpdate";
                case "MetadataEntityDeleted": return "OnDelete";
                default: return null;
            }
        }

        private List<OutboxItem> LoadPendingEvents()
        {
            var items = new List<OutboxItem>();
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open)
                        conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT TOP (@batch) Id, EventType, Payload
                            FROM [OutboxEvents]
                            WHERE Status = 'Pending'
                              AND EventType IN ('MetadataEntityCreated', 'MetadataEntityUpdated', 'MetadataEntityDeleted')
                            ORDER BY CreatedAt";
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.Add(new SqlParameter("@batch", BatchSize));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                items.Add(new OutboxItem
                                {
                                    Id = reader.GetInt32(0),
                                    EventType = reader.GetString(1),
                                    Payload = reader.GetString(2)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
            return items;
        }

        private void MarkProcessed(int id, string error)
        {
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open)
                        conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            UPDATE [OutboxEvents]
                            SET Status = 'Processed', ProcessedAt = @now
                            WHERE Id = @id";
                        cmd.CommandTimeout = 5;
                        cmd.Parameters.Add(new SqlParameter("@now", DateTime.UtcNow));
                        cmd.Parameters.Add(new SqlParameter("@id", id));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception) { }
        }

        private void MarkFailed(int id, string error)
        {
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open)
                        conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            UPDATE [OutboxEvents]
                            SET RetryCount = RetryCount + 1, 
                                LastAttemptAt = @now,
                                LastError = @error,
                                Status = CASE WHEN RetryCount >= 3 THEN 'Failed' ELSE 'Pending' END
                            WHERE Id = @id";
                        cmd.CommandTimeout = 5;
                        cmd.Parameters.Add(new SqlParameter("@now", DateTime.UtcNow));
                        cmd.Parameters.Add(new SqlParameter("@error", (object)error ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@id", id));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception) { }
        }

        private class OutboxItem
        {
            public int Id { get; set; }
            public string EventType { get; set; }
            public string Payload { get; set; }
        }
    }
}
