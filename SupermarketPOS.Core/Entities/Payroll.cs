using System;
using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Payroll : BaseDocument
    {
        public override string DocumentType => "Payroll";

        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public int EmployeeCount { get; set; }
        public decimal TotalBasicSalary { get; set; }
        public decimal TotalAllowances { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal TotalOvertime { get; set; }
        public decimal TotalLateDeductions { get; set; }
        public decimal TotalNetSalary { get; set; }

        public virtual ICollection<PayrollItem> Items { get; set; }

        public override void Validate()
        {
            base.Validate();
            if (Year <= 0 || Month <= 0 || Month > 12)
                throw new InvalidOperationException("Invalid payroll period");
            if (EmployeeCount <= 0)
                throw new InvalidOperationException("Payroll must include at least one employee");
            if (TotalNetSalary <= 0)
                throw new InvalidOperationException("Total net salary must be greater than zero");
        }
    }
}