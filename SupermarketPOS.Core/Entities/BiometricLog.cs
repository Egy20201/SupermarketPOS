using System;

namespace SupermarketPOS.Core.Entities
{
    public class BiometricLog
    {
        public int Id { get; set; }
        public string EmployeeCode { get; set; }
        public DateTime Timestamp { get; set; }
        public string Direction { get; set; }
        public int? AttendanceRecordId { get; set; }
        public virtual AttendanceRecord AttendanceRecord { get; set; }
        public bool IsProcessed { get; set; }
        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }
    }
}
