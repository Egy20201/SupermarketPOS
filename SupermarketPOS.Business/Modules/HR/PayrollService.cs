using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class PayrollService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly DocumentNumberingService _numbering;
        private readonly SettingsService _settings;
        private readonly AuditService _auditService;

        public PayrollService(Func<AppDbContext> dbFactory, DocumentNumberingService numbering, SettingsService settings, AuditService auditService)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _numbering = numbering ?? throw new ArgumentNullException(nameof(numbering));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public Payroll Generate(int year, int month, int? branchId, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    // Idempotency check
                    var existing = db.Payrolls.FirstOrDefault(p =>
                        p.Year == year && p.Month == month &&
                        (branchId == null || p.BranchId == branchId) &&
                        p.Status != DocumentStatus.Cancelled);

                    if (existing != null)
                        throw new InvalidOperationException(
                            $"Payroll already exists for {month:D2}/{year}");

                    var from = new DateTime(year, month, 1);
                    var to = from.AddMonths(1);
                    var workingDays = Enumerable.Range(0, (to - from).Days)
                        .Select(d => from.AddDays(d))
                        .Count(d => d.DayOfWeek != DayOfWeek.Friday && d.DayOfWeek != DayOfWeek.Saturday);

                    var employees = db.Employees.Where(e => e.IsActive)
                        .Where(e => branchId == null || e.BranchId == branchId)
                        .Include("Shift").ToList();

                    var attendanceLookup = db.AttendanceRecords
                        .Where(a => a.Date >= from && a.Date < to)
                        .ToList();

                    var leaveLookup = db.LeaveRequests
                        .Where(l => l.LeaveFrom < to && l.LeaveTo >= from)
                        .Where(l => l.Status == DocumentStatus.Approved || l.Status == DocumentStatus.Posted)
                        .ToList();

                    var penaltyLookup = db.Penalties
                        .Where(p => p.Date >= from && p.Date < to)
                        .Where(p => p.PayrollItemId == null)
                        .ToList();

                    var overtimeRate = _settings.GetDecimal("hr.overtimeRate", 1.5m);
                    var latePenaltyRate = _settings.GetDecimal("hr.latePenalty", 10m);
                    var leaveDayDeduction = _settings.GetDecimal("hr.leaveDayDeduction", 1.0m);

                    var payroll = new Payroll
                    {
                        Number = _numbering.GenerateNumber("PAY-", "dbo.PayrollSeq"),
                        Date = DateTime.UtcNow,
                        Year = year, Month = month,
                        PeriodFrom = from, PeriodTo = to.AddDays(-1),
                        UserId = userId, BranchId = branchId,
                        Status = DocumentStatus.Draft
                    };

                    var items = new List<PayrollItem>();
                    decimal totalBasic = 0, totalAllow = 0, totalDeduct = 0;
                    decimal totalOvertime = 0, totalLate = 0, totalNet = 0;

                    foreach (var emp in employees)
                    {
                        var attendance = attendanceLookup.Where(a => a.EmployeeId == emp.Id).ToList();
                        var leaves = leaveLookup.Where(l => l.EmployeeId == emp.Id).ToList();
                        var penalties = penaltyLookup.Where(p => p.EmployeeId == emp.Id).ToList();

                        var presentDays = attendance.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late);
                        var lateDays = attendance.Count(a => a.Status == AttendanceStatus.Late);
                        var absentDays = workingDays - presentDays;
                        var leaveDays = leaves.Sum(l => l.TotalDays);

                        var overtimeMinutes = attendance.Sum(a => a.OvertimeMinutes);
                        var lateMinutes = attendance.Sum(a => a.LateMinutes);

                        var dailyRate = workingDays > 0 ? emp.BasicSalary / workingDays : 0;
                        var hourlyRate = dailyRate / 8;
                        var salaryForDays = dailyRate * presentDays;
                        var overtimePay = hourlyRate * overtimeRate * overtimeMinutes / 60;
                        var latePenalty = lateMinutes * latePenaltyRate / 60;
                        var leaveDeduction = dailyRate * leaveDayDeduction * leaveDays;
                        var penaltyAmount = penalties.Sum(p => p.Amount);

                        var netSalary = salaryForDays + emp.Allowances - emp.Deductions + overtimePay - latePenalty - leaveDeduction - penaltyAmount;

                        if (netSalary < 0) netSalary = 0;

                        var item = new PayrollItem
                        {
                            EmployeeId = emp.Id,
                            BasicSalary = salaryForDays,
                            Allowances = emp.Allowances,
                            Deductions = emp.Deductions + penaltyAmount,
                            OvertimeAmount = overtimePay,
                            LateDeduction = latePenalty,
                            NetSalary = netSalary,
                            WorkDays = presentDays,
                            AbsentDays = absentDays
                        };

                        items.Add(item);
                        totalBasic += salaryForDays; totalAllow += emp.Allowances;
                        totalDeduct += emp.Deductions + penaltyAmount;
                        totalOvertime += overtimePay; totalLate += latePenalty;
                        totalNet += netSalary;
                    }

                    payroll.Items = items;
                    payroll.EmployeeCount = employees.Count;
                    payroll.TotalBasicSalary = totalBasic;
                    payroll.TotalAllowances = totalAllow;
                    payroll.TotalDeductions = totalDeduct;
                    payroll.TotalOvertime = totalOvertime;
                    payroll.TotalLateDeductions = totalLate;
                    payroll.TotalNetSalary = totalNet;

                    payroll.Validate();

                    // First save Payroll with items to get generated IDs
                    db.Payrolls.Add(payroll);
                    db.SaveChanges();

                    // Now item.Id has real values — update penalties
                    foreach (var penalty in penaltyLookup)
                    {
                        var matchingItem = payroll.Items.FirstOrDefault(i => i.EmployeeId == penalty.EmployeeId);
                        if (matchingItem != null)
                        {
                            penalty.PayrollItemId = matchingItem.Id;
                        }
                    }

                    // Save penalty updates
                    db.SaveChanges();

                    _auditService.Log("GENERATE_PAYROLL", "Payroll", payroll.Id, userId);
                    tx.Commit();

                    return payroll;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public void PostPayroll(int payrollId, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var payroll = db.Payrolls.Find(payrollId);
                    if (payroll == null) throw new InvalidOperationException("Payroll not found");
                    if (payroll.Status == DocumentStatus.Posted) { tx.Commit(); return; }
                    if (payroll.Status != DocumentStatus.Approved) throw new InvalidOperationException("Must be Approved before posting");

                    WorkflowEngine.Transition(payroll, DocumentStatus.Posted);

                    var expenseId = GetAccountId(db, "account.expenses", "5.2.1");
                    var cashId = GetAccountId(db, "account.cash", "1.1.1");

                    var journal = new JournalEntry
                    {
                        Date = DateTime.UtcNow,
                        EntryNumber = $"PAY-{payroll.Number}",
                        Description = $"رواتب {payroll.Month:D2}/{payroll.Year}",
                        SourceType = "Payroll",
                        SourceId = payroll.Id,
                        UserId = userId
                    };
                    db.JournalEntries.Add(journal);

                    db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, AccountId = expenseId, Description = "مصروف رواتب", Debit = payroll.TotalNetSalary, Credit = 0 });
                    db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, AccountId = cashId, Description = "خزينة", Debit = 0, Credit = payroll.TotalNetSalary });

                    _auditService.Log("POST_PAYROLL", "Payroll", payrollId, userId);
                    db.SaveChanges();
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
        }

        private int GetAccountId(AppDbContext db, string settingKey, string defaultCode)
        {
            var code = _settings.Get(settingKey, defaultCode);
            var id = db.Accounts.Where(a => a.Code == code).Select(a => (int?)a.Id).FirstOrDefault();
            if (!id.HasValue) throw new InvalidOperationException($"Account '{code}' not found");
            return id.Value;
        }

        public void ApprovePayroll(int payrollId, int userId)
        {
            using (var db = _dbFactory())
            {
                var payroll = db.Payrolls.Find(payrollId);
                if (payroll == null) throw new InvalidOperationException("Payroll not found");
                if (payroll.Status != DocumentStatus.Draft)
                    throw new InvalidOperationException("Only Draft payrolls can be approved");

                WorkflowEngine.Transition(payroll, DocumentStatus.Approved);
                db.SaveChanges();
                _auditService.Log("APPROVE_PAYROLL", "Payroll", payrollId, userId);
            }
        }
    }
}