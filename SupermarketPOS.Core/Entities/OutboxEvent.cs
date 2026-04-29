using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class OutboxEvent
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string EventType { get; set; }

        [Required]
        public string Payload { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAt { get; set; }

        [Required, MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public int RetryCount { get; set; }

        public DateTime? LastAttemptAt { get; set; }

        public string LastError { get; set; }
    }
}
