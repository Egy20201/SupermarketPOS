using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketPOS.Core.Metadata
{
    [Table("WorkflowExecutionLogs")]
    public class WorkflowExecutionLog
    {
        public int Id { get; set; }

        public int WorkflowId { get; set; }

        public int? RuleId { get; set; }

        public int? ActionId { get; set; }

        [Required, MaxLength(200)]
        public string EntityName { get; set; }

        public int? EntityId { get; set; }

        [Required, MaxLength(50)]
        public string Trigger { get; set; }

        [Required, MaxLength(50)]
        public string Status { get; set; }

        public string ResultJson { get; set; }

        public string ErrorMessage { get; set; }

        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        public int? UserId { get; set; }
    }
}
