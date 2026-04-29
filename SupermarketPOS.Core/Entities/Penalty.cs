using System;

namespace SupermarketPOS.Core.Entities
{
    public class Penalty
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string Reason { get; set; }
        public decimal Amount { get; set; }
        public int? PayrollItemId { get; set; }
        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }
    }
}
