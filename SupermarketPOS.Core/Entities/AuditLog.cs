using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string ActionType { get; set; }
        public string EntityName { get; set; }
        public int? EntityId { get; set; }
        public string Description { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string IPAddress { get; set; }
        public virtual User User { get; set; }
    }
}