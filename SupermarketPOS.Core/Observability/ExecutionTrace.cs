using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketPOS.Core.Observability
{
    /// <summary>
    /// Phase 5, Step 1: Execution trace for every operation.
    /// Captures timing, status, and error details for observability.
    /// </summary>
    [Table("ExecutionTraces")]
    public class ExecutionTrace
    {
        public int Id { get; set; }

        [MaxLength(16)]
        public string CorrelationId { get; set; }

        [Required, MaxLength(200)]
        public string EntityName { get; set; }

        [Required, MaxLength(50)]
        public string Operation { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public long? DurationMs { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Running";

        public string ErrorMessage { get; set; }

        [MaxLength(50)]
        public string ErrorClassification { get; set; }

        public int? UserId { get; set; }
    }
}
