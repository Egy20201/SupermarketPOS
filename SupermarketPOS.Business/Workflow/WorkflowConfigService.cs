using Newtonsoft.Json;
using SupermarketPOS.Core.Metadata;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Phase 4.5, Step 10: Configurable business logic — manage workflows from DB.
    /// Add/update/delete rules without redeployment.
    /// </summary>
    public sealed class WorkflowConfigService
    {
        private readonly Func<AppDbContext> _dbFactory;

        public WorkflowConfigService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public int CreateWorkflow(int entityId, string name, string trigger)
        {
            using (var db = _dbFactory())
            {
                var conn = db.Database.Connection;
                if (conn.State != ConnectionState.Open) conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO [Workflows] ([EntityId], [Name], [Trigger], [IsActive])
                        OUTPUT INSERTED.Id
                        VALUES (@entityId, @name, @trigger, 1)";
                    cmd.Parameters.Add(new SqlParameter("@entityId", entityId));
                    cmd.Parameters.Add(new SqlParameter("@name", name));
                    cmd.Parameters.Add(new SqlParameter("@trigger", trigger));
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        public int AddRule(int workflowId, string conditionExpression, int priority)
        {
            using (var db = _dbFactory())
            {
                var conn = db.Database.Connection;
                if (conn.State != ConnectionState.Open) conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO [WorkflowRules] ([WorkflowId], [ConditionExpression], [Priority])
                        OUTPUT INSERTED.Id
                        VALUES (@wId, @condition, @priority)";
                    cmd.Parameters.Add(new SqlParameter("@wId", workflowId));
                    cmd.Parameters.Add(new SqlParameter("@condition", (object)conditionExpression ?? DBNull.Value));
                    cmd.Parameters.Add(new SqlParameter("@priority", priority));
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        public int AddAction(int ruleId, string actionType, Dictionary<string, object> parameters)
        {
            string json = parameters != null ? JsonConvert.SerializeObject(parameters) : null;

            using (var db = _dbFactory())
            {
                var conn = db.Database.Connection;
                if (conn.State != ConnectionState.Open) conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO [WorkflowActions] ([RuleId], [ActionType], [ParametersJson])
                        OUTPUT INSERTED.Id
                        VALUES (@ruleId, @actionType, @json)";
                    cmd.Parameters.Add(new SqlParameter("@ruleId", ruleId));
                    cmd.Parameters.Add(new SqlParameter("@actionType", actionType));
                    cmd.Parameters.Add(new SqlParameter("@json", (object)json ?? DBNull.Value));
                    return (int)cmd.ExecuteScalar();
                }
            }
        }

        public void SetWorkflowActive(int workflowId, bool isActive)
        {
            using (var db = _dbFactory())
            {
                var conn = db.Database.Connection;
                if (conn.State != ConnectionState.Open) conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE [Workflows] SET [IsActive] = @active WHERE [Id] = @id";
                    cmd.Parameters.Add(new SqlParameter("@active", isActive));
                    cmd.Parameters.Add(new SqlParameter("@id", workflowId));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteWorkflow(int workflowId)
        {
            using (var db = _dbFactory())
            {
                var conn = db.Database.Connection;
                if (conn.State != ConnectionState.Open) conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM [Workflows] WHERE [Id] = @id";
                    cmd.Parameters.Add(new SqlParameter("@id", workflowId));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateRule(int ruleId, string conditionExpression, int? priority)
        {
            using (var db = _dbFactory())
            {
                var conn = db.Database.Connection;
                if (conn.State != ConnectionState.Open) conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE [WorkflowRules]
                        SET ConditionExpression = @condition,
                            Priority = COALESCE(@priority, Priority)
                        WHERE Id = @id";
                    cmd.Parameters.Add(new SqlParameter("@condition", (object)conditionExpression ?? DBNull.Value));
                    cmd.Parameters.Add(new SqlParameter("@priority", (object)priority ?? DBNull.Value));
                    cmd.Parameters.Add(new SqlParameter("@id", ruleId));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<WorkflowDefinition> GetWorkflowsForEntity(int entityId)
        {
            var workflows = new List<WorkflowDefinition>();

            using (var db = _dbFactory())
            {
                var conn = db.Database.Connection;
                if (conn.State != ConnectionState.Open) conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT Id, EntityId, Name, Trigger, IsActive
                        FROM [Workflows]
                        WHERE EntityId = @entityId
                        ORDER BY Name";
                    cmd.Parameters.Add(new SqlParameter("@entityId", entityId));

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            workflows.Add(new WorkflowDefinition
                            {
                                Id = reader.GetInt32(0),
                                EntityId = reader.GetInt32(1),
                                Name = reader.GetString(2),
                                Trigger = reader.GetString(3),
                                IsActive = reader.GetBoolean(4)
                            });
                        }
                    }
                }
            }

            return workflows;
        }
    }
}
