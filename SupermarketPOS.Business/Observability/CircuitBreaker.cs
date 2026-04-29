using SupermarketPOS.Core.Observability;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SupermarketPOS.Business.Observability
{
    /// <summary>
    /// Phase 5, Step 8: Circuit breaker for workflow execution and external calls.
    /// States: Closed (normal), Open (rejecting), HalfOpen (testing).
    /// </summary>
    public sealed class CircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _openDuration;
        private readonly string _name;

        private int _failureCount;
        private CircuitState _state = CircuitState.Closed;
        private DateTime _lastFailureTime = DateTime.MinValue;
        private readonly object _lock = new object();

        public CircuitBreaker(string name, int failureThreshold = 5, int openDurationSeconds = 60)
        {
            _name = name;
            _failureThreshold = failureThreshold;
            _openDuration = TimeSpan.FromSeconds(openDurationSeconds);
        }

        public CircuitState State
        {
            get
            {
                lock (_lock)
                {
                    if (_state == CircuitState.Open && DateTime.UtcNow - _lastFailureTime > _openDuration)
                        _state = CircuitState.HalfOpen;
                    return _state;
                }
            }
        }

        public string Name => _name;
        public int FailureCount => _failureCount;

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            if (State == CircuitState.Open)
                throw new CircuitBreakerOpenException(_name);

            try
            {
                T result = await action().ConfigureAwait(false);
                OnSuccess();
                return result;
            }
            catch (Exception ex)
            {
                OnFailure();
                throw;
            }
        }

        public async Task ExecuteAsync(Func<Task> action)
        {
            if (State == CircuitState.Open)
                throw new CircuitBreakerOpenException(_name);

            try
            {
                await action().ConfigureAwait(false);
                OnSuccess();
            }
            catch (Exception)
            {
                OnFailure();
                throw;
            }
        }

        public void OnSuccess()
        {
            lock (_lock)
            {
                _failureCount = 0;
                _state = CircuitState.Closed;
            }
        }

        public void OnFailure()
        {
            lock (_lock)
            {
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;
                if (_failureCount >= _failureThreshold)
                    _state = CircuitState.Open;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _failureCount = 0;
                _state = CircuitState.Closed;
            }
        }
    }

    public enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    public class CircuitBreakerOpenException : Exception
    {
        public string CircuitName { get; }

        public CircuitBreakerOpenException(string circuitName)
            : base($"Circuit breaker '{circuitName}' is open. Requests are being rejected.")
        {
            CircuitName = circuitName;
        }
    }
}
