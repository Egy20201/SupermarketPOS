using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SupermarketPOS.Core.Metadata
{
    [Table("ScheduledJobs")]
    public class ScheduledJob
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; }

        [Required, MaxLength(100)]
        public string CronExpression { get; set; }

        [Required, MaxLength(200)]
        public string ActionName { get; set; }

        [Required, MaxLength(200)]
        public string EntityName { get; set; }

        public string ParametersJson { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? LastRunAt { get; set; }

        public DateTime? NextRunAt { get; set; }

        [MaxLength(50)]
        public string LastRunStatus { get; set; }

        public string LastRunError { get; set; }
    }
}
