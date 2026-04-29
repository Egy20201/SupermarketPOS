using SupermarketPOS.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;

namespace SupermarketPOS.Business.Observability
{
    /// <summary>
    /// Phase 5, Step 5: In-memory metrics system.
    /// Tracks latency, failure counts, and execution stats per entity/operation.
    /// </summary>
    public sealed class MetricsService
    {
        private readonly ConcurrentDictionary<string, OperationMetrics> _metrics =
            new ConcurrentDictionary<string, OperationMetrics>(StringComparer.OrdinalIgnoreCase);

        private readonly Func<AppDbContext> _dbFactory;

        public MetricsService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public void RecordOperation(string entityName, string operation, long durationMs, bool success)
        {
            string key = $"{entityName}:{operation}";
            var m = _metrics.GetOrAdd(key, _ => new OperationMetrics(entityName, operation));
            m.Record(durationMs, success);
        }

        public OperationMetrics GetMetrics(string entityName, string operation)
        {
            string key = $"{entityName}:{operation}";
            OperationMetrics m;
            _metrics.TryGetValue(key, out m);
            return m;
        }

        public List<OperationMetrics> GetAllMetrics()
        {
            return _metrics.Values.OrderByDescending(m => m.TotalCount).ToList();
        }

        public List<OperationMetrics> GetSlowOperations(long thresholdMs = 1000)
        {
            return _metrics.Values
                .Where(m => m.AvgDurationMs > thresholdMs)
                .OrderByDescending(m => m.AvgDurationMs)
                .ToList();
        }

        public List<OperationMetrics> GetFailedOperations()
        {
            return _metrics.Values
                .Where(m => m.FailCount > 0)
                .OrderByDescending(m => m.FailCount)
                .ToList();
        }

        public void SnapshotToDb()
        {
            if (_dbFactory == null) return;

            try
            {
                using (var db = _dbFactory())
                {
                    var conn = db.Database.Connection;
                    if (conn.State != ConnectionState.Open) conn.Open();

                    foreach (var m in _metrics.Values)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"
                                INSERT INTO [ExecutionTraces]
                                    ([CorrelationId], [EntityName], [Operation], [StartTime], [EndTime],
                                     [DurationMs], [Status], [ErrorMessage])
                                VALUES
                                    ('METRIC_SNAP', @entity, @op, @now, @now,
                                     @avgMs, 'MetricSnapshot',
                                     @summary)";
                            cmd.CommandTimeout = 5;
                            cmd.Parameters.Add(new SqlParameter("@entity", m.EntityName));
                            cmd.Parameters.Add(new SqlParameter("@op", m.Operation));
                            cmd.Parameters.Add(new SqlParameter("@now", DateTime.UtcNow));
                            cmd.Parameters.Add(new SqlParameter("@avgMs", (long)m.AvgDurationMs));
                            cmd.Parameters.Add(new SqlParameter("@summary",
                                $"Total={m.TotalCount}, Fail={m.FailCount}, AvgMs={m.AvgDurationMs:F0}, MaxMs={m.MaxDurationMs}"));
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception) { }
        }
    }

    public sealed class OperationMetrics
    {
        public string EntityName { get; }
        public string Operation { get; }

        private long _totalCount;
        private long _failCount;
        private long _totalDurationMs;
        private long _maxDurationMs;
        private long _minDurationMs = long.MaxValue;

        public long TotalCount => Interlocked.Read(ref _totalCount);
        public long FailCount => Interlocked.Read(ref _failCount);
        public long MaxDurationMs => Interlocked.Read(ref _maxDurationMs);
        public long MinDurationMs { get { var v = Interlocked.Read(ref _minDurationMs); return v == long.MaxValue ? 0 : v; } }
        public double AvgDurationMs
        {
            get
            {
                long total = Interlocked.Read(ref _totalCount);
                return total == 0 ? 0 : (double)Interlocked.Read(ref _totalDurationMs) / total;
            }
        }

        public OperationMetrics(string entityName, string operation)
        {
            EntityName = entityName;
            Operation = operation;
        }

        public void Record(long durationMs, bool success)
        {
            Interlocked.Increment(ref _totalCount);
            Interlocked.Add(ref _totalDurationMs, durationMs);

            if (!success)
                Interlocked.Increment(ref _failCount);

            long currentMax;
            do { currentMax = Interlocked.Read(ref _maxDurationMs); }
            while (durationMs > currentMax && Interlocked.CompareExchange(ref _maxDurationMs, durationMs, currentMax) != currentMax);

            long currentMin;
            do { currentMin = Interlocked.Read(ref _minDurationMs); }
            while (durationMs < currentMin && Interlocked.CompareExchange(ref _minDurationMs, durationMs, currentMin) != currentMin);
        }
    }
}
