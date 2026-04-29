using Newtonsoft.Json;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class AuditService
    {
        private readonly Func<AppDbContext> _dbFactory;

        public AuditService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public void Log(string action, string entityName, int? entityId, int userId)
        {
            InsertLog(action, entityName, entityId, userId, null, null);
        }

        public void LogChange(string action, string entityName, int? entityId, int userId,
            object oldValues, object newValues)
        {
            string oldJson = oldValues != null ? JsonConvert.SerializeObject(oldValues) : null;
            string newJson = newValues != null ? JsonConvert.SerializeObject(newValues) : null;
            InsertLog(action, entityName, entityId, userId, oldJson, newJson);
        }

        public List<AuditLogEntry> GetLogs(int userId, DateTime? from = null, DateTime? to = null, int maxResults = 500)
        {
            using (var db = _dbFactory())
            {
                var query = db.AuditLogEntries.Where(l => l.UserId == userId);
                if (from.HasValue) query = query.Where(l => l.Timestamp >= from.Value);
                if (to.HasValue) query = query.Where(l => l.Timestamp <= to.Value);
                return query.OrderByDescending(l => l.Timestamp).Take(maxResults).ToList();
            }
        }

        public List<AuditLogEntry> GetEntityLogs(string entityName, int entityId, int maxResults = 200)
        {
            using (var db = _dbFactory())
            {
                return db.AuditLogEntries
                    .Where(l => l.EntityName == entityName && l.EntityId == entityId)
                    .OrderByDescending(l => l.Timestamp).Take(maxResults).ToList();
            }
        }

        public List<AuditLogEntry> GetAllLogs(DateTime? from = null, DateTime? to = null,
            string action = null, string entityName = null, int maxResults = 1000)
        {
            using (var db = _dbFactory())
            {
                var query = db.AuditLogEntries.AsQueryable();
                if (from.HasValue) query = query.Where(l => l.Timestamp >= from.Value);
                if (to.HasValue) query = query.Where(l => l.Timestamp <= to.Value);
                if (!string.IsNullOrWhiteSpace(action)) query = query.Where(l => l.Action == action);
                if (!string.IsNullOrWhiteSpace(entityName)) query = query.Where(l => l.EntityName == entityName);
                return query.OrderByDescending(l => l.Timestamp).Take(maxResults).ToList();
            }
        }

        public int PurgeLogs(DateTime olderThan)
        {
            using (var db = _dbFactory())
            {
                var oldLogs = db.AuditLogEntries.Where(l => l.Timestamp < olderThan).ToList();
                if (oldLogs.Any()) { db.AuditLogEntries.RemoveRange(oldLogs); db.SaveChanges(); }
                return oldLogs.Count;
            }
        }

        private void InsertLog(string action, string entityName, int? entityId, int userId,
            string oldValues, string newValues)
        {
            try
            {
                using (var db = _dbFactory())
                {
                    db.AuditLogEntries.Add(new AuditLogEntry
                    {
                        UserId = userId,
                        Action = action,
                        EntityName = entityName,
                        EntityId = entityId,
                        OldValues = oldValues,
                        NewValues = newValues,
                        Timestamp = DateTime.Now,
                        Device = Environment.MachineName
                    });
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Audit log failed: {action} on {entityName} by user {userId}");
            }
        }
    }
}