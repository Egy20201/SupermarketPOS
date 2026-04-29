using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class AuditLogEntry
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        [Required, MaxLength(100)]
        public string Action { get; set; }
        [Required, MaxLength(100)]
        public string EntityName { get; set; }
        public int? EntityId { get; set; }
        public string OldValues { get; set; }
        public string NewValues { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        [MaxLength(50)]
        public string IPAddress { get; set; }
        [MaxLength(100)]
        public string Device { get; set; }
        public virtual User User { get; set; }
    }
}