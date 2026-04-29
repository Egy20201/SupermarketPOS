using System;
using System.Collections.Concurrent;

namespace SupermarketPOS.Business.Workflow
{
    /// <summary>
    /// Tracks which (key, scope) tuples have already executed, so retries
    /// don't duplicate side effects.
    /// </summary>
    public interface IIdempotencyStore
    {
        /// <summary>
        /// Atomically records execution. Returns true if this is the first
        /// time the (scope, key) pair has been seen, false if it was already
        /// executed.
        /// </summary>
        bool TryMarkExecuted(string scope, string key);

        /// <summary>Returns true if the (scope, key) pair was previously executed.</summary>
        bool HasExecuted(string scope, string key);

        /// <summary>Removes the (scope, key) entry. Primarily used in tests.</summary>
        void Forget(string scope, string key);

        /// <summary>Removes everything. Primarily used in tests.</summary>
        void Clear();
    }

    /// <summary>
    /// Thread-safe in-memory implementation backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// Suitable for single-process workflow execution; swap with a durable
    /// implementation if cross-process safety is needed.
    /// </summary>
    public sealed class InMemoryIdempotencyStore : IIdempotencyStore
    {
        private readonly ConcurrentDictionary<string, DateTime> _entries
            = new ConcurrentDictionary<string, DateTime>(StringComparer.Ordinal);

        public bool TryMarkExecuted(string scope, string key)
        {
            var composite = Compose(scope, key);
            if (composite == null) return true;
            return _entries.TryAdd(composite, DateTime.UtcNow);
        }

        public bool HasExecuted(string scope, string key)
        {
            var composite = Compose(scope, key);
            if (composite == null) return false;
            return _entries.ContainsKey(composite);
        }

        public void Forget(string scope, string key)
        {
            var composite = Compose(scope, key);
            if (composite == null) return;
            DateTime _;
            _entries.TryRemove(composite, out _);
        }

        public void Clear() => _entries.Clear();

        private static string Compose(string scope, string key)
        {
            if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(key)) return null;
            return scope + "::" + key;
        }
    }
}
