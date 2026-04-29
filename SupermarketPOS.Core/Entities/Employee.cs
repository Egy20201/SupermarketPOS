using System;
using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Employee
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Position { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal Allowances { get; set; }
        public decimal Deductions { get; set; }
        public DateTime HireDate { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public int? UserId { get; set; }
        public virtual User User { get; set; }
        public int? DepartmentId { get; set; }
        public virtual Department Department { get; set; }
        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }
        public int? ShiftId { get; set; }
        public virtual Shift Shift { get; set; }
        public decimal HourlyRate { get; set; }
        public int MaxLeaveDays { get; set; } = 21;
        public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; }
    }
}