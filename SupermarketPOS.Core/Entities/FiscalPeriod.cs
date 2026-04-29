using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class FiscalPeriod
    {
        [Key]
        public int Id { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockedAt { get; set; }
        public int? LockedById { get; set; }
        public virtual User LockedBy { get; set; }
        public string LockReason { get; set; }
        public string PeriodDisplay { get; set; }
    }
}