using System;

namespace SupermarketPOS.Business
{
    public enum HealthStatus { Healthy, Degraded, Unhealthy }

    public class SystemHealthService
    {
        private const int DegradedQueueThreshold = 50;
        private const int UnhealthyQueueThreshold = 200;
        private const double DegradedFailureRateThreshold = 0.10;
        private const double UnhealthyFailureRateThreshold = 0.30;
        private const double DegradedLatencyThreshold = 2000;
        private const double UnhealthyLatencyThreshold = 5000;

        private readonly OutboxBackgroundProcessor _processor;

        public SystemHealthService(OutboxBackgroundProcessor processor)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        }

        public int QueueSize => _processor.Metrics.LastQueueSize;

        public double FailureRate
        {
            get
            {
                var metrics = _processor.Metrics;
                var total = metrics.TotalProcessed + metrics.TotalFailed;
                return total == 0 ? 0 : (double)metrics.TotalFailed / total;
            }
        }

        public double AvgLatency => _processor.Metrics.AverageLatencyMs;

        public bool IsProcessorRunning => _processor.IsRunning;

        public HealthStatus Status
        {
            get
            {
                if (!_processor.IsRunning)
                    return HealthStatus.Unhealthy;

                if (QueueSize >= UnhealthyQueueThreshold ||
                    FailureRate >= UnhealthyFailureRateThreshold ||
                    AvgLatency >= UnhealthyLatencyThreshold)
                    return HealthStatus.Unhealthy;

                if (QueueSize >= DegradedQueueThreshold ||
                    FailureRate >= DegradedFailureRateThreshold ||
                    AvgLatency >= DegradedLatencyThreshold)
                    return HealthStatus.Degraded;

                return HealthStatus.Healthy;
            }
        }

        public bool IsHealthy => Status == HealthStatus.Healthy;

        public string StatusSummary
        {
            get
            {
                var status = Status;
                var metrics = _processor.Metrics;
                return $"[{status}] Queue={QueueSize}, FailRate={FailureRate:P1}, AvgLatency={AvgLatency:F1}ms, " +
                       $"Processed={metrics.TotalProcessed}, Failed={metrics.TotalFailed}, Running={IsProcessorRunning}";
            }
        }
    }
}
