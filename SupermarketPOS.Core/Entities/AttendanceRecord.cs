using System;

namespace SupermarketPOS.Core.Entities
{
    public class AttendanceRecord
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }
        public DateTime Date { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public decimal LateMinutes { get; set; }
        public decimal OvertimeMinutes { get; set; }
        public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
        public string Notes { get; set; }
        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }
    }
}