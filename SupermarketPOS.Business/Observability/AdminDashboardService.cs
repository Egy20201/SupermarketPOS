using Newtonsoft.Json;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace SupermarketPOS.Business.Observability
{
    /// <summary>
    /// Phase 5, Step 6: Admin dashboard data service.
    /// Exposes recent errors, failed workflows, slow operations.
    /// </summary>
    public sealed class AdminDashboardService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly MetricsService _metrics;

        public AdminDashboardService(Func<AppDbContext> dbFactory, MetricsService metrics)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _metrics = metrics;
        }

        public List<DashboardError> GetRecentErrors(int count = 50)
        {
            var errors = new List<DashboardError>();
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open) conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT TOP (@count) Id, CorrelationId, EntityName, Operation,
                                   StartTime, DurationMs, ErrorMessage, ErrorClassification
                            FROM [ExecutionTraces]
                            WHERE Status = 'Fail'
                            ORDER BY StartTime DESC";
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.Add(new SqlParameter("@count", count));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                errors.Add(new DashboardError
                                {
                                    TraceId = reader.GetInt32(0),
                                    CorrelationId = reader.IsDBNull(1) ? null : reader.GetString(1),
                                    EntityName = reader.GetString(2),
                                    Operation = reader.GetString(3),
                                    Timestamp = reader.GetDateTime(4),
                                    DurationMs = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                                    ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    Classification = reader.IsDBNull(7) ? null : reader.GetString(7)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
            return errors;
        }

        public List<DashboardWorkflowFailure> GetFailedWorkflows(int count = 50)
        {
            var failures = new List<DashboardWorkflowFailure>();
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open) conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT TOP (@count) l.Id, l.WorkflowId, l.RuleId, l.ActionId,
                                   l.EntityName, l.[Trigger], l.Status, l.ErrorMessage,
                                   l.ExecutedAt, l.CorrelationId,
                                   w.Name AS WorkflowName
                            FROM [WorkflowExecutionLogs] l
                            LEFT JOIN [Workflows] w ON w.Id = l.WorkflowId
                            WHERE l.Status IN ('ActionFailed', 'ConditionError', 'Blocked', 'Timeout', 'ActionBlocked')
                            ORDER BY l.ExecutedAt DESC";
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.Add(new SqlParameter("@count", count));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                failures.Add(new DashboardWorkflowFailure
                                {
                                    LogId = reader.GetInt32(0),
                                    WorkflowId = reader.GetInt32(1),
                                    RuleId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                    ActionId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                                    EntityName = reader.GetString(4),
                                    Trigger = reader.GetString(5),
                                    Status = reader.GetString(6),
                                    ErrorMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                                    Timestamp = reader.GetDateTime(8),
                                    CorrelationId = reader.IsDBNull(9) ? null : reader.GetString(9),
                                    WorkflowName = reader.IsDBNull(10) ? null : reader.GetString(10)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
            return failures;
        }

        public List<DashboardDeadLetter> GetDeadLetterEvents(int count = 50)
        {
            var items = new List<DashboardDeadLetter>();
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open) conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT TOP (@count) Id, SourceType, SourceEventId, ErrorMessage,
                                   RetryCount, CreatedAt, Status, CorrelationId
                            FROM [DeadLetterEvents]
                            WHERE Status = 'Dead'
                            ORDER BY CreatedAt DESC";
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.Add(new SqlParameter("@count", count));

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                items.Add(new DashboardDeadLetter
                                {
                                    Id = reader.GetInt32(0),
                                    SourceType = reader.GetString(1),
                                    SourceEventId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                                    ErrorMessage = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    RetryCount = reader.GetInt32(4),
                                    CreatedAt = reader.GetDateTime(5),
                                    Status = reader.GetString(6),
                                    CorrelationId = reader.IsDBNull(7) ? null : reader.GetString(7)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
            return items;
        }

        public List<OperationMetrics> GetSlowOperations(long thresholdMs = 1000)
        {
            return _metrics?.GetSlowOperations(thresholdMs) ?? new List<OperationMetrics>();
        }

        public DashboardSummary GetSummary()
        {
            var summary = new DashboardSummary();
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open) conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT
                                (SELECT COUNT(*) FROM [ExecutionTraces] WHERE Status = 'Fail' AND StartTime >= @since) AS RecentErrors,
                                (SELECT COUNT(*) FROM [WorkflowExecutionLogs] WHERE Status IN ('ActionFailed','Timeout') AND ExecutedAt >= @since) AS FailedWorkflows,
                                (SELECT COUNT(*) FROM [DeadLetterEvents] WHERE Status = 'Dead') AS DeadLetters,
                                (SELECT AVG(DurationMs) FROM [ExecutionTraces] WHERE StartTime >= @since AND DurationMs IS NOT NULL) AS AvgLatency";
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.Add(new SqlParameter("@since", DateTime.UtcNow.AddHours(-24)));

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                summary.RecentErrorCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                summary.FailedWorkflowCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                summary.DeadLetterCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                                summary.AvgLatencyMs = reader.IsDBNull(3) ? 0 : Convert.ToDouble(reader[3]);
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
            return summary;
        }
    }

    public class DashboardError
    {
        public int TraceId { get; set; }
        public string CorrelationId { get; set; }
        public string EntityName { get; set; }
        public string Operation { get; set; }
        public DateTime Timestamp { get; set; }
        public long DurationMs { get; set; }
        public string ErrorMessage { get; set; }
        public string Classification { get; set; }
    }

    public class DashboardWorkflowFailure
    {
        public int LogId { get; set; }
        public int WorkflowId { get; set; }
        public int? RuleId { get; set; }
        public int? ActionId { get; set; }
        public string EntityName { get; set; }
        public string Trigger { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
        public string CorrelationId { get; set; }
        public string WorkflowName { get; set; }
    }

    public class DashboardDeadLetter
    {
        public int Id { get; set; }
        public string SourceType { get; set; }
        public int? SourceEventId { get; set; }
        public string ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public string CorrelationId { get; set; }
    }

    public class DashboardSummary
    {
        public int RecentErrorCount { get; set; }
        public int FailedWorkflowCount { get; set; }
        public int DeadLetterCount { get; set; }
        public double AvgLatencyMs { get; set; }
    }
}
