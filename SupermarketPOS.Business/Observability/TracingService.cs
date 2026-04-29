using SupermarketPOS.Core.Observability;
using SupermarketPOS.Data;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

namespace SupermarketPOS.Business.Observability
{
    /// <summary>
    /// Phase 5, Step 1: Execution trace service.
    /// Records timing, status, and errors for every operation.
    /// </summary>
    public sealed class TracingService
    {
        private readonly Func<AppDbContext> _dbFactory;

        public TracingService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public TraceHandle BeginTrace(string entityName, string operation, int? userId = null)
        {
            var correlationId = CorrelationContext.EnsureCorrelationId();
            var startTime = DateTime.UtcNow;
            var sw = Stopwatch.StartNew();

            int traceId = InsertTrace(correlationId, entityName, operation, startTime, userId);

            return new TraceHandle(this, traceId, sw, entityName, operation, correlationId);
        }

        internal void CompleteTrace(int traceId, Stopwatch sw, string status, string errorMessage, string errorClassification)
        {
            sw.Stop();
            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open) conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            UPDATE [ExecutionTraces]
                            SET EndTime = @end, DurationMs = @dur, Status = @status,
                                ErrorMessage = @error, ErrorClassification = @classification
                            WHERE Id = @id";
                        cmd.CommandTimeout = 5;
                        cmd.Parameters.Add(new SqlParameter("@end", DateTime.UtcNow));
                        cmd.Parameters.Add(new SqlParameter("@dur", sw.ElapsedMilliseconds));
                        cmd.Parameters.Add(new SqlParameter("@status", status ?? "Success"));
                        cmd.Parameters.Add(new SqlParameter("@error", (object)errorMessage ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@classification", (object)errorClassification ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@id", traceId));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception) { }
        }

        private int InsertTrace(string correlationId, string entityName, string operation, DateTime startTime, int? userId)
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
                            INSERT INTO [ExecutionTraces]
                                ([CorrelationId], [EntityName], [Operation], [StartTime], [Status], [UserId])
                            OUTPUT INSERTED.Id
                            VALUES (@cid, @entity, @op, @start, 'Running', @userId)";
                        cmd.CommandTimeout = 5;
                        cmd.Parameters.Add(new SqlParameter("@cid", (object)correlationId ?? DBNull.Value));
                        cmd.Parameters.Add(new SqlParameter("@entity", entityName));
                        cmd.Parameters.Add(new SqlParameter("@op", operation));
                        cmd.Parameters.Add(new SqlParameter("@start", startTime));
                        cmd.Parameters.Add(new SqlParameter("@userId", (object)userId ?? DBNull.Value));
                        return (int)cmd.ExecuteScalar();
                    }
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// RAII handle for tracing. Dispose to complete the trace.
    /// </summary>
    public sealed class TraceHandle : IDisposable
    {
        private readonly TracingService _service;
        private readonly int _traceId;
        private readonly Stopwatch _sw;
        private bool _completed;

        public string EntityName { get; }
        public string Operation { get; }
        public string CorrelationId { get; }
        public long ElapsedMs => _sw.ElapsedMilliseconds;

        internal TraceHandle(TracingService service, int traceId, Stopwatch sw,
            string entityName, string operation, string correlationId)
        {
            _service = service;
            _traceId = traceId;
            _sw = sw;
            EntityName = entityName;
            Operation = operation;
            CorrelationId = correlationId;
        }

        public void Complete(string status = "Success")
        {
            if (_completed || _traceId == 0) return;
            _completed = true;
            _service.CompleteTrace(_traceId, _sw, status, null, null);
        }

        public void Fail(Exception ex)
        {
            if (_completed || _traceId == 0) return;
            _completed = true;
            var classification = FailureClassification.Classify(ex);
            _service.CompleteTrace(_traceId, _sw, "Fail", ex?.Message, classification);
        }

        public void Fail(string errorMessage, string classification = null)
        {
            if (_completed || _traceId == 0) return;
            _completed = true;
            _service.CompleteTrace(_traceId, _sw, "Fail", errorMessage, classification ?? FailureClassification.SystemError);
        }

        public void Dispose()
        {
            if (!_completed && _traceId > 0)
                Complete();
        }
    }
}
