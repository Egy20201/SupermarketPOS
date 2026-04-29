using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketPOS.Core.Observability
{
    /// <summary>
    /// Phase 5, Step 4: Dead letter queue for failed Outbox events and scheduled jobs.
    /// </summary>
    [Table("DeadLetterEvents")]
    public class DeadLetterEvent
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string SourceType { get; set; }

        public int? SourceEventId { get; set; }

        [Required]
        public string Payload { get; set; }

        public string ErrorMessage { get; set; }

        public int RetryCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReplayedAt { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Dead";

        [MaxLength(16)]
        public string CorrelationId { get; set; }
    }
}
