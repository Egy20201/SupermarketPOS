namespace SupermarketPOS.Core.Entities
{
    public class PayrollItem
    {
        public int Id { get; set; }
        public int PayrollId { get; set; }
        public virtual Payroll Payroll { get; set; }
        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal Allowances { get; set; }
        public decimal Deductions { get; set; }
        public decimal OvertimeAmount { get; set; }
        public decimal LateDeduction { get; set; }
        public decimal NetSalary { get; set; }
        public int WorkDays { get; set; }
        public int AbsentDays { get; set; }
    }
}