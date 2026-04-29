using Newtonsoft.Json;
using SupermarketPOS.Business.Metadata;
using SupermarketPOS.Core.Metadata;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Phase 4, Steps 3-6: Workflow execution engine.
    /// Hooks into GenericDataService lifecycle (Create/Update/Delete/ExecuteAction).
    /// Evaluates rules, executes actions, prevents loops, and logs audit trail.
    /// </summary>
    public sealed class WorkflowEngine
    {
        private const int MaxExecutionDepth = 5;
        private const int TimeoutMs = 30000;

        private static readonly AsyncLocal<ExecutionContext> _context = new AsyncLocal<ExecutionContext>();

        private readonly Func<AppDbContext> _dbFactory;
        private readonly ConditionEvaluator _evaluator;
        private readonly IGenericDataService _dataService;

        public WorkflowEngine(
            Func<AppDbContext> dbFactory,
            IGenericDataService dataService)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _evaluator = new ConditionEvaluator();
        }

        /// <summary>
        /// Execute all active workflows for the given entity and trigger.
        /// Called by GenericDataService after Create/Update/Delete/ExecuteAction.
        /// </summary>
        public async Task ExecuteAsync(
            string entityName,
            string trigger,
            int? entityId,
            Dictionary<string, object> data,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(trigger))
                return;

            var ctx = _context.Value ?? new ExecutionContext();
            _context.Value = ctx;

            // Loop protection: max depth
            if (ctx.Depth >= MaxExecutionDepth)
            {
                LogExecution(0, null, null, entityName, entityId, trigger,
                    "Blocked", null, "Max execution depth reached");
                return;
            }

            // Loop protection: prevent same entity+trigger re-entry
            var reentryKey = $"{entityName}:{trigger}:{entityId}";
            if (ctx.ExecutedKeys.Contains(reentryKey))
            {
                LogExecution(0, null, null, entityName, entityId, trigger,
                    "Blocked", null, "Re-entry loop detected");
                return;
            }

            ctx.ExecutedKeys.Add(reentryKey);
            ctx.Depth++;

            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(TimeoutMs);

                    var workflows = LoadWorkflows(entityName, trigger);
                    if (workflows.Count == 0)
                        return;

                    foreach (var workflow in workflows)
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        await ExecuteWorkflowAsync(workflow, entityName, entityId, data, cts.Token)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogExecution(0, null, null, entityName, entityId, trigger,
                    "Timeout", null, "Workflow execution timed out");
            }
            finally
            {
                ctx.Depth--;
                ctx.ExecutedKeys.Remove(reentryKey);
                if (ctx.Depth == 0)
                    _context.Value = null;
            }
        }

        private async Task ExecuteWorkflowAsync(
            WorkflowDefinition workflow,
            string entityName,
            int? entityId,
            Dictionary<string, object> data,
            CancellationToken cancellationToken)
        {
            var rules = workflow.Rules
                .OrderBy(r => r.Priority)
                .ToList();

            foreach (var rule in rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool conditionMet;
                try
                {
                    conditionMet = _evaluator.Evaluate(rule.ConditionExpression, data);
                }
                catch (Exception ex)
                {
                    LogExecution(workflow.Id, rule.Id, null, entityName, entityId,
                        workflow.Trigger, "ConditionError", null, ex.Message);
                    continue;
                }

                if (!conditionMet)
                    continue;

                LogExecution(workflow.Id, rule.Id, null, entityName, entityId,
                    workflow.Trigger, "RuleMatched", null, null);

                foreach (var action in rule.Actions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var result = await ExecuteActionAsync(
                            action, entityName, entityId, data, cancellationToken)
                            .ConfigureAwait(false);

                        LogExecution(workflow.Id, rule.Id, action.Id, entityName, entityId,
                            workflow.Trigger, "ActionExecuted",
                            result != null ? JsonConvert.SerializeObject(result) : null, null);
                    }
                    catch (ValidationException vex)
                    {
                        LogExecution(workflow.Id, rule.Id, action.Id, entityName, entityId,
                            workflow.Trigger, "ActionBlocked", null, vex.Message);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LogExecution(workflow.Id, rule.Id, action.Id, entityName, entityId,
                            workflow.Trigger, "ActionFailed", null, ex.Message);
                    }
                }
            }
        }

        private async Task<object> ExecuteActionAsync(
            WorkflowAction action,
            string entityName,
            int? entityId,
            Dictionary<string, object> data,
            CancellationToken cancellationToken)
        {
            var parameters = !string.IsNullOrWhiteSpace(action.ParametersJson)
                ? JsonConvert.DeserializeObject<Dictionary<string, object>>(action.ParametersJson)
                : new Dictionary<string, object>();

            switch (action.ActionType?.ToLowerInvariant())
            {
                case "setfield":
                    return await ExecuteSetFieldAsync(entityName, entityId, data, parameters)
                        .ConfigureAwait(false);

                case "notify":
                    ExecuteNotify(entityName, entityId, parameters);
                    return "Notified";

                case "block":
                    ExecuteBlock(entityName, parameters);
                    return null;

                case "executeaction":
                    return await ExecuteChainedActionAsync(entityName, entityId, data, parameters, cancellationToken)
                        .ConfigureAwait(false);

                default:
                    return null;
            }
        }

        // ================================================================
        //  Action Type Implementations (Step 4)
        // ================================================================

        private async Task<object> ExecuteSetFieldAsync(
            string entityName, int? entityId,
            Dictionary<string, object> data,
            Dictionary<string, object> parameters)
        {
            if (entityId == null || entityId <= 0) return null;

            var updateData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in parameters)
            {
                updateData[kvp.Key] = kvp.Value;
                if (data != null)
                    data[kvp.Key] = kvp.Value;
            }

            if (updateData.Count > 0)
                return await _dataService.UpdateAsync(entityName, entityId.Value, updateData)
                    .ConfigureAwait(false);

            return 0;
        }

        private void ExecuteNotify(string entityName, int? entityId, Dictionary<string, object> parameters)
        {
            object messageObj;
            string message = parameters.TryGetValue("Message", out messageObj)
                ? Convert.ToString(messageObj) : $"Workflow notification for {entityName}";

            object levelObj;
            string level = parameters.TryGetValue("Level", out levelObj)
                ? Convert.ToString(levelObj) : "Info";

            PublishNotificationEvent(entityName, entityId, message, level);
        }

        private void ExecuteBlock(string entityName, Dictionary<string, object> parameters)
        {
            object messageObj;
            string message = parameters.TryGetValue("Message", out messageObj)
                ? Convert.ToString(messageObj) : $"Operation blocked by workflow rule on {entityName}";

            throw new ValidationException(entityName, new List<string> { message });
        }

        private async Task<object> ExecuteChainedActionAsync(
            string entityName, int? entityId,
            Dictionary<string, object> data,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken)
        {
            object actionNameObj;
            if (!parameters.TryGetValue("ActionName", out actionNameObj))
                return null;

            string actionName = Convert.ToString(actionNameObj);
            if (string.IsNullOrWhiteSpace(actionName))
                return null;

            var actionData = data != null
                ? new Dictionary<string, object>(data, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (entityId.HasValue && entityId.Value > 0)
                actionData["Id"] = entityId.Value;

            return await _dataService.ExecuteActionAsync(entityName, actionName, actionData)
                .ConfigureAwait(false);
        }

        // ================================================================
        //  Workflow Loading
        // ================================================================

        private List<WorkflowDefinition> LoadWorkflows(string entityName, string trigger)
        {
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open)
                        conn.Open();

                    var workflows = new Dictionary<int, WorkflowDefinition>();
                    var rules = new Dictionary<int, WorkflowRule>();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT w.Id AS WId, w.EntityId, w.Name AS WName, w.Trigger, w.IsActive,
                                   r.Id AS RId, r.ConditionExpression, r.Priority,
                                   a.Id AS AId, a.ActionType, a.ParametersJson
                            FROM [Workflows] w
                            INNER JOIN [Entities] e ON e.Id = w.EntityId
                            LEFT JOIN [WorkflowRules] r ON r.WorkflowId = w.Id
                            LEFT JOIN [WorkflowActions] a ON a.RuleId = r.Id
                            WHERE e.Name = @entityName
                              AND w.Trigger = @trigger
                              AND w.IsActive = 1
                            ORDER BY r.Priority, a.Id";
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.Add(new SqlParameter("@entityName", entityName));
                        cmd.Parameters.Add(new SqlParameter("@trigger", trigger));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int wId = reader.GetInt32(reader.GetOrdinal("WId"));
                                if (!workflows.ContainsKey(wId))
                                {
                                    workflows[wId] = new WorkflowDefinition
                                    {
                                        Id = wId,
                                        EntityId = reader.GetInt32(reader.GetOrdinal("EntityId")),
                                        Name = reader.GetString(reader.GetOrdinal("WName")),
                                        Trigger = reader.GetString(reader.GetOrdinal("Trigger")),
                                        IsActive = true
                                    };
                                }

                                if (!reader.IsDBNull(reader.GetOrdinal("RId")))
                                {
                                    int rId = reader.GetInt32(reader.GetOrdinal("RId"));
                                    if (!rules.ContainsKey(rId))
                                    {
                                        var rule = new WorkflowRule
                                        {
                                            Id = rId,
                                            WorkflowId = wId,
                                            ConditionExpression = reader.IsDBNull(reader.GetOrdinal("ConditionExpression"))
                                                ? null : reader.GetString(reader.GetOrdinal("ConditionExpression")),
                                            Priority = reader.GetInt32(reader.GetOrdinal("Priority"))
                                        };
                                        rules[rId] = rule;
                                        workflows[wId].Rules.Add(rule);
                                    }

                                    if (!reader.IsDBNull(reader.GetOrdinal("AId")))
                                    {
                                        rules[rId].Actions.Add(new WorkflowAction
                                        {
                                            Id = reader.GetInt32(reader.GetOrdinal("AId")),
                                            RuleId = rId,
                                            ActionType = reader.GetString(reader.GetOrdinal("ActionType")),
                                            ParametersJson = reader.IsDBNull(reader.GetOrdinal("ParametersJson"))
                                                ? null : reader.GetString(reader.GetOrdinal("ParametersJson"))
                                        });
                                    }
                                }
                            }
                        }
                    }

                    return workflows.Values.ToList();
                }
            }
            catch (Exception)
            {
                return new List<WorkflowDefinition>();
            }
        }

        // ================================================================
        //  Audit Logging (Step 6)
        // ================================================================

        private void LogExecution(
            int workflowId, int? ruleId, int? actionId,
            string entityName, int? entityId,
            string trigger, string status,
            string resultJson, string errorMessage)
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
                            INSERT INTO [WorkflowExecutionLogs]
                                ([WorkflowId], [RuleId], [ActionId], [EntityName], [EntityId],
                                 [Trigger], [Status], [ResultJson], [ErrorMessage], [ExecutedAt], [UserId])
                            VALUES
                                (@wId, @rId, @aId, @entity, @eId,
                                 @trigger, @status, @result, @error, @now, @userId)";
                        cmd.CommandTimeout = 5;
                        cmd.Parameters.Add(new SqlParameter("@wId", workflowId));
                        cmd.Parameters.Add(new SqlParameter("@rId", (object)ruleId ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@aId", (object)actionId ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@entity", entityName));
                        cmd.Parameters.Add(new SqlParameter("@eId", (object)entityId ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@trigger", trigger));
                        cmd.Parameters.Add(new SqlParameter("@status", status));
                        cmd.Parameters.Add(new SqlParameter("@result", (object)resultJson ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@error", (object)errorMessage ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@now", DateTime.UtcNow));

                        var session = SupermarketPOS.Business.Metadata.GenericDataService.CurrentSession;
                        cmd.Parameters.Add(new SqlParameter("@userId", (object)(session?.UserId) ?? DBNull.Value));

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                // Audit logging is best-effort
            }
        }

        private void PublishNotificationEvent(string entityName, int? entityId, string message, string level)
        {
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open)
                        conn.Open();

                    var payload = JsonConvert.SerializeObject(new
                    {
                        EntityName = entityName,
                        EntityId = entityId,
                        Message = message,
                        Level = level,
                        Timestamp = DateTime.UtcNow
                    });

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO [OutboxEvents] ([EventType], [Payload], [CreatedAt], [Status], [RetryCount])
                            VALUES ('WorkflowNotification', @payload, @now, 'Pending', 0)";
                        cmd.CommandTimeout = 5;
                        cmd.Parameters.Add(new SqlParameter("@payload", payload));
                        cmd.Parameters.Add(new SqlParameter("@now", DateTime.UtcNow));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception)
            {
                // Notification publishing is best-effort
            }
        }

        // ================================================================
        //  Loop Protection Context (Step 5)
        // ================================================================

        private sealed class ExecutionContext
        {
            public int Depth { get; set; }
            public HashSet<string> ExecutedKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

}
