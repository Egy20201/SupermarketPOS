using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using SupermarketPOS.Core.Metadata;

namespace SupermarketPOS.Business.Metadata
{
    /// <summary>
    /// Phase 2.5: Caches generated SQL strings by entity + filter shape.
    /// The SQL structure is identical for the same entity + set of filter field names + operators;
    /// only parameter values change between calls. This avoids rebuilding SQL on every request.
    /// 
    /// Thread-safe via ConcurrentDictionary. Cache is per-entity, per-operation type.
    /// </summary>
    internal static class QueryPlanCache
    {
        // Key = "{Operation}|{EntityName}|{FilterShape}" → cached SQL template
        private static readonly ConcurrentDictionary<string, string> _cache =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Try to get a cached SQL plan for a SELECT query.
        /// Returns null if not cached.
        /// </summary>
        public static string TryGetSelect(string entityName, QueryRequest request, bool withLookups)
        {
            var key = BuildKey("SELECT", entityName, request, withLookups);
            _cache.TryGetValue(key, out var sql);
            return sql;
        }

        /// <summary>
        /// Store a generated SQL plan for future reuse.
        /// </summary>
        public static void StoreSelect(string entityName, QueryRequest request, bool withLookups, string sql)
        {
            var key = BuildKey("SELECT", entityName, request, withLookups);
            _cache.TryAdd(key, sql);
        }

        /// <summary>
        /// Invalidate all cached plans for an entity (call after metadata changes).
        /// </summary>
        public static void Invalidate(string entityName)
        {
            if (string.IsNullOrWhiteSpace(entityName)) return;
            var prefix = entityName + "|";
            var keysToRemove = new List<string>();
            foreach (var key in _cache.Keys)
            {
                // Key format: "SELECT|EntityName|..." — check second segment
                var parts = key.Split('|');
                if (parts.Length >= 2 &&
                    parts[1].Equals(entityName, StringComparison.OrdinalIgnoreCase))
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove)
                _cache.TryRemove(key, out _);
        }

        /// <summary>
        /// Clear the entire cache (e.g., on metadata reload).
        /// </summary>
        public static void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Current number of cached plans (for diagnostics).
        /// </summary>
        public static int Count => _cache.Count;

        /// <summary>
        /// Build a deterministic cache key from the query shape.
        /// Shape = operation + entity + selected fields + filter fields+operators + sort fields + hasOffset + hasLimit + lookups.
        /// Parameter values are NOT part of the key — only the structural shape matters.
        /// </summary>
        private static string BuildKey(string operation, string entityName, QueryRequest request, bool withLookups)
        {
            // Filter shape: field names + operators, sorted for determinism
            var filterShape = string.Empty;
            if (request.Filters != null && request.Filters.Count > 0)
            {
                filterShape = string.Join(",",
                    request.Filters
                        .OrderBy(f => f.FieldName, StringComparer.OrdinalIgnoreCase)
                        .Select(f => $"{f.FieldName}:{f.Operator ?? "eq"}"));
            }

            // Sort shape
            var sortShape = string.Empty;
            if (request.SortBy != null && request.SortBy.Count > 0)
            {
                sortShape = string.Join(",",
                    request.SortBy.Select(s => $"{s.FieldName}:{(s.Descending ? "D" : "A")}"));
            }

            // Select shape
            var selectShape = string.Empty;
            if (request.SelectFields != null && request.SelectFields.Count > 0)
            {
                selectShape = string.Join(",",
                    request.SelectFields.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
            }

            var paging = $"{(request.Offset.HasValue ? "O" : "")}{(request.Limit.HasValue ? "L" : "")}";

            return $"{operation}|{entityName}|{selectShape}|{filterShape}|{sortShape}|{paging}|{(withLookups ? "LK" : "")}";
        }
    }
}
