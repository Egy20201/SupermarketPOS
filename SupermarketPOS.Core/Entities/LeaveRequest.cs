using System;

namespace SupermarketPOS.Core.Entities
{
    public class LeaveRequest : BaseDocument
    {
        public override string DocumentType => "LeaveRequest";

        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        public DateTime LeaveFrom { get; set; }
        public DateTime LeaveTo { get; set; }
        public int TotalDays { get; set; }
        public string LeaveType { get; set; }
        public string Reason { get; set; }

        public override void Validate()
        {
            base.Validate();
            if (EmployeeId <= 0) throw new InvalidOperationException("Leave must have employee");
            if (LeaveFrom > LeaveTo) throw new InvalidOperationException("From date must be before To date");
            if (TotalDays <= 0) throw new InvalidOperationException("Total days must be greater than zero");
        }
    }
}
