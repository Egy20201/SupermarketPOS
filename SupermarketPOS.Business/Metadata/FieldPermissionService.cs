using SupermarketPOS.Core.Metadata;
using SupermarketPOS.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business.Metadata
{
    /// <summary>
    /// Phase 2.5: Field-level permission enforcement.
    /// Loads permission rules from DB, caches per entity+role.
    /// 
    /// Default policy: no rule = full access. Rules explicitly restrict.
    /// Thread-safe via ConcurrentDictionary.
    /// </summary>
    public sealed class FieldPermissionService
    {
        private readonly Func<AppDbContext> _dbFactory;

        // Cache key = "EntityId:RoleId" → dictionary of fieldName → permission
        private readonly ConcurrentDictionary<string, Dictionary<string, FieldPermission>> _cache =
            new ConcurrentDictionary<string, Dictionary<string, FieldPermission>>(StringComparer.OrdinalIgnoreCase);

        public FieldPermissionService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <summary>
        /// Get readable field names for a role on an entity.
        /// Returns null if no restrictions exist (= all fields allowed).
        /// </summary>
        public HashSet<string> GetReadableFields(int entityId, int? roleId)
        {
            if (!roleId.HasValue) return null; // No role = no restrictions

            var perms = GetPermissions(entityId, roleId.Value);
            if (perms == null || perms.Count == 0) return null; // No rules = full access

            var readable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in perms)
            {
                if (kvp.Value.CanRead)
                    readable.Add(kvp.Key);
            }
            return readable;
        }

        /// <summary>
        /// Get writable field names for a role on an entity.
        /// Returns null if no restrictions exist (= all fields allowed).
        /// </summary>
        public HashSet<string> GetWritableFields(int entityId, int? roleId)
        {
            if (!roleId.HasValue) return null;

            var perms = GetPermissions(entityId, roleId.Value);
            if (perms == null || perms.Count == 0) return null;

            var writable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in perms)
            {
                if (kvp.Value.CanWrite)
                    writable.Add(kvp.Key);
            }
            return writable;
        }

        /// <summary>
        /// Filter a data dictionary to only writable fields for the given role.
        /// Returns a new dictionary with restricted fields removed.
        /// If no restrictions exist, returns the original dictionary unchanged.
        /// </summary>
        public Dictionary<string, object> FilterWritableData(
            int entityId, int? roleId, Dictionary<string, object> data)
        {
            var writable = GetWritableFields(entityId, roleId);
            if (writable == null) return data; // No restrictions

            var filtered = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in data)
            {
                if (kvp.Key.Equals("Id", StringComparison.OrdinalIgnoreCase) || writable.Contains(kvp.Key))
                    filtered[kvp.Key] = kvp.Value;
            }
            return filtered;
        }

        /// <summary>
        /// Strip non-readable fields from query result rows.
        /// Modifies rows in-place.
        /// </summary>
        public void FilterReadableRows(
            int entityId, int? roleId, List<Dictionary<string, object>> rows)
        {
            var readable = GetReadableFields(entityId, roleId);
            if (readable == null) return; // No restrictions

            // Always allow Id
            readable.Add("Id");

            foreach (var row in rows)
            {
                var keysToRemove = row.Keys
                    .Where(k => !readable.Contains(k) &&
                                !k.EndsWith("_Display", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var key in keysToRemove)
                    row.Remove(key);
            }
        }

        /// <summary>
        /// Invalidate cache for an entity (call after permission changes).
        /// </summary>
        public void InvalidateCache(int entityId)
        {
            var prefix = $"{entityId}:";
            foreach (var key in _cache.Keys.ToList())
            {
                if (key.StartsWith(prefix))
                    _cache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Clear all cached permissions.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        private Dictionary<string, FieldPermission> GetPermissions(int entityId, int roleId)
        {
            var cacheKey = $"{entityId}:{roleId}";
            return _cache.GetOrAdd(cacheKey, _ =>
            {
                using (var db = _dbFactory())
                {
                    var perms = db.FieldPermissions
                        .AsNoTracking()
                        .Where(fp => fp.EntityId == entityId && fp.RoleId == roleId)
                        .ToList();

                    if (perms.Count == 0) return null;

                    return perms.ToDictionary(
                        p => p.FieldName,
                        p => p,
                        StringComparer.OrdinalIgnoreCase);
                }
            });
        }
    }
}
