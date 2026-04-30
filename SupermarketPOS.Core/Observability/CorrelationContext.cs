using System;
using System.Threading;

namespace SupermarketPOS.Core.Observability
{
    /// <summary>
    /// Phase 5, Step 2: Correlation context flowing through all async operations.
    /// Uses AsyncLocal to propagate CorrelationId across API, Workflow, Outbox, Jobs.
    /// </summary>
    public static class CorrelationContext
    {
        private static readonly AsyncLocal<string> _correlationId = new AsyncLocal<string>();

        public static string CorrelationId
        {
            get => _correlationId.Value;
            set => _correlationId.Value = value;
        }

        public static string EnsureCorrelationId()
        {
            if (string.IsNullOrEmpty(_correlationId.Value))
                _correlationId.Value = Guid.NewGuid().ToString("N").Substring(0, 16);
            return _correlationId.Value;
        }

        public static IDisposable BeginScope()
        {
            return new CorrelationScope();
        }

        public static IDisposable BeginScope(string correlationId)
        {
            return new CorrelationScope(correlationId);
        }

        private sealed class CorrelationScope : IDisposable
        {
            private readonly string _previous;

            public CorrelationScope()
            {
                _previous = _correlationId.Value;
                _correlationId.Value = Guid.NewGuid().ToString("N").Substring(0, 16);
            }

            public CorrelationScope(string correlationId)
            {
                _previous = _correlationId.Value;
                _correlationId.Value = correlationId;
            }

            public void Dispose()
            {
                _correlationId.Value = _previous;
            }
        }
    }
}
