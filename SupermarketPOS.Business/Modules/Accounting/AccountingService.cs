using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business.Modules
{
    public class AccountingService
    {
        private readonly Func<AppDbContext> _dbFactory;

        public AccountingService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public List<Account> GetAccounts()
        {
            using (var db = _dbFactory())
            {
                return db.Accounts.AsNoTracking().OrderBy(a => a.Code).ToList();
            }
        }

        public List<Account> GetPostingAccounts()
        {
            using (var db = _dbFactory())
            {
                return db.Accounts.AsNoTracking().Where(a => a.IsActive && !a.IsParent).OrderBy(a => a.Code).ToList();
            }
        }

        public void SaveAccount(AccountSaveRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using (var db = _dbFactory())
            {
                Account account;
                if (request.Id.HasValue)
                {
                    account = db.Accounts.Find(request.Id.Value);
                    if (account == null) return;
                }
                else
                {
                    account = new Account();
                    db.Accounts.Add(account);
                }

                account.Code = request.Code;
                account.Name = request.Name;
                account.AccountType = request.AccountType;
                account.ParentId = request.ParentId;
                account.OpeningBalance = request.OpeningBalance;
                account.BalanceType = request.BalanceType;
                account.IsActive = request.IsActive;
                db.SaveChanges();
            }
        }

        public void DeleteAccount(int accountId)
        {
            using (var db = _dbFactory())
            {
                var account = db.Accounts.Find(accountId);
                if (account == null) return;
                db.Accounts.Remove(account);
                db.SaveChanges();
            }
        }

        public BalanceSheetResult GetBalanceSheet(DateTime asOfInclusive)
        {
            var to = asOfInclusive.Date.AddDays(1);
            using (var db = _dbFactory())
            {
                var accounts = db.Accounts.AsNoTracking().Where(a => a.IsActive).ToList();
                var lines = db.JournalEntryLines.AsNoTracking().Where(l => l.JournalEntry.Date < to).ToList();
                var result = new BalanceSheetResult();

                foreach (var account in accounts)
                {
                    var accountLines = lines.Where(l => l.AccountId == account.Id).ToList();
                    var debit = accountLines.Sum(l => l.Debit);
                    var credit = accountLines.Sum(l => l.Credit);
                    var balance = IsDebitBalance(account.BalanceType)
                        ? account.OpeningBalance + debit - credit
                        : account.OpeningBalance + credit - debit;

                    if (balance == 0) continue;

                    var item = new BalanceSheetLineDto { AccountName = account.Name, Balance = balance };
                    var accountType = NormalizeAccountType(account.AccountType);
                    if (accountType == AccountingConstants.Asset)
                    {
                        result.Assets.Add(item);
                        result.TotalAssets += balance;
                    }
                    else if (accountType == AccountingConstants.Liability)
                    {
                        result.Liabilities.Add(item);
                        result.TotalLiabilities += balance;
                    }
                    else if (accountType == AccountingConstants.Equity)
                    {
                        result.Equity.Add(item);
                        result.TotalEquity += balance;
                    }
                }

                result.Difference = result.TotalAssets - (result.TotalLiabilities + result.TotalEquity);
                return result;
            }
        }

        public TrialBalanceResult GetTrialBalance(DateTime from, DateTime toExclusive)
        {
            using (var db = _dbFactory())
            {
                var accounts = db.Accounts.AsNoTracking().Where(a => a.IsActive && !a.IsParent).OrderBy(a => a.Code).ToList();
                var lines = db.JournalEntryLines.AsNoTracking().Where(l => l.JournalEntry.Date >= from && l.JournalEntry.Date < toExclusive).ToList();
                var result = new TrialBalanceResult();

                foreach (var account in accounts)
                {
                    var accountLines = lines.Where(l => l.AccountId == account.Id).ToList();
                    var debit = accountLines.Sum(l => l.Debit);
                    var credit = accountLines.Sum(l => l.Credit);
                    var balance = IsDebitBalance(account.BalanceType)
                        ? account.OpeningBalance + debit - credit
                        : account.OpeningBalance + credit - debit;

                    if (debit <= 0 && credit <= 0 && account.OpeningBalance == 0) continue;

                    result.Items.Add(new TrialBalanceLineDto
                    {
                        AccountCode = account.Code,
                        AccountName = account.Name,
                        Debit = debit,
                        Credit = credit,
                        Balance = Math.Abs(balance)
                    });
                    result.TotalDebit += debit;
                    result.TotalCredit += credit;
                }

                result.Difference = result.TotalDebit - result.TotalCredit;
                return result;
            }
        }

        public List<LedgerLineDto> GetLedgerEntries(int accountId, DateTime from, DateTime toExclusive)
        {
            using (var db = _dbFactory())
            {
                var account = db.Accounts.AsNoTracking().FirstOrDefault(a => a.Id == accountId);
                var lines = db.JournalEntryLines.AsNoTracking()
                    .Where(l => l.AccountId == accountId && l.JournalEntry.Date >= from && l.JournalEntry.Date < toExclusive)
                    .OrderBy(l => l.JournalEntry.Date)
                    .Select(l => new
                    {
                        l.JournalEntry.Date,
                        l.JournalEntry.EntryNumber,
                        l.JournalEntry.Description,
                        l.Debit,
                        l.Credit
                    })
                    .ToList();

                var result = new List<LedgerLineDto>();
                var balance = account == null ? 0 : account.OpeningBalance;
                foreach (var line in lines)
                {
                    balance += line.Debit - line.Credit;
                    result.Add(new LedgerLineDto
                    {
                        Date = line.Date,
                        EntryNumber = line.EntryNumber,
                        Description = line.Description,
                        Debit = line.Debit,
                        Credit = line.Credit,
                        Balance = balance
                    });
                }

                return result;
            }
        }

        public string GetNextJournalVoucherNumber()
        {
            using (var db = _dbFactory())
            {
                var lastId = db.JournalEntries.OrderByDescending(j => j.Id).Select(j => (int?)j.Id).FirstOrDefault() ?? 0;
                return string.Format("J-{0:yyyyMMdd}-{1:D4}", DateTime.Now, lastId + 1);
            }
        }

        public void SaveJournalVoucher(string entryNumber, DateTime date, string description, int? userId, IEnumerable<JournalVoucherLineDto> lines)
        {
            var validLines = (lines ?? Enumerable.Empty<JournalVoucherLineDto>())
                .Where(l => l.AccountId > 0 && (l.Debit > 0 || l.Credit > 0))
                .ToList();

            if (!validLines.Any()) throw new InvalidOperationException("لا توجد سطور صالحة");
            if (validLines.Sum(l => l.Debit) != validLines.Sum(l => l.Credit)) throw new InvalidOperationException("القيد غير متوازن");

            using (var db = _dbFactory())
            {
                var entry = new JournalEntry
                {
                    EntryNumber = entryNumber,
                    Date = date,
                    Description = description,
                    UserId = userId,
                    SourceType = "Manual"
                };
                db.JournalEntries.Add(entry);
                db.SaveChanges();

                foreach (var line in validLines)
                {
                    db.JournalEntryLines.Add(new JournalEntryLine
                    {
                        JournalEntryId = entry.Id,
                        AccountId = line.AccountId,
                        Debit = line.Debit,
                        Credit = line.Credit,
                        Description = line.Notes
                    });
                }

                db.SaveChanges();
            }
        }

        public DailyClosingSummaryDto GetDailyClosingSummary(DateTime date)
        {
            var from = date.Date;
            var to = from.AddDays(1);
            using (var db = _dbFactory())
            {
                var isClosed = db.DailyClosings.Any(c => c.ClosingDate == from && c.IsClosed);
                var invoices = db.SaleInvoices.AsNoTracking().Where(i => i.Date >= from && i.Date < to).ToList();
                var total = invoices.Sum(i => i.NetAmount);
                var cash = invoices.Where(i => i.CustomerId == null).Sum(i => i.NetAmount);

                return new DailyClosingSummaryDto
                {
                    Date = from,
                    IsClosed = isClosed,
                    TotalInvoices = invoices.Count,
                    TotalSales = total,
                    TotalCashSales = cash,
                    TotalCreditSales = total - cash,
                    ExpectedCash = cash
                };
            }
        }

        public void CloseDay(DateTime date, int userId, decimal actualCash, string notes)
        {
            var summary = GetDailyClosingSummary(date);
            using (var db = _dbFactory())
            {
                if (db.DailyClosings.Any(c => c.ClosingDate == summary.Date && c.IsClosed))
                    throw new InvalidOperationException("تم إغلاق هذا اليوم بالفعل");

                db.DailyClosings.Add(new DailyClosing
                {
                    ClosingDate = summary.Date,
                    ClosedById = userId,
                    TotalInvoices = summary.TotalInvoices,
                    TotalSales = summary.TotalSales,
                    TotalCashSales = summary.TotalCashSales,
                    TotalCreditSales = summary.TotalCreditSales,
                    ExpectedCashInDrawer = summary.ExpectedCash,
                    ActualCashInDrawer = actualCash,
                    CashDifference = actualCash - summary.ExpectedCash,
                    Notes = notes,
                    IsClosed = true
                });
                db.SaveChanges();
            }
        }

        public List<Expense> GetExpenses(DateTime from, DateTime toExclusive, string expenseType)
        {
            using (var db = _dbFactory())
            {
                var query = db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= from && e.ExpenseDate < toExclusive);
                if (!string.IsNullOrWhiteSpace(expenseType) && expenseType != "الكل")
                    query = query.Where(e => e.ExpenseType == expenseType);
                return query.OrderByDescending(e => e.ExpenseDate).Take(200).ToList();
            }
        }

        public void AddExpense(DateTime date, string description, decimal amount, string expenseType, int userId)
        {
            using (var db = _dbFactory())
            {
                db.Expenses.Add(new Expense
                {
                    ExpenseDate = date,
                    Description = description,
                    Amount = amount,
                    ExpenseType = string.IsNullOrWhiteSpace(expenseType) ? "أخرى" : expenseType,
                    CreatedById = userId
                });
                db.SaveChanges();
            }
        }

        public void DeleteExpense(int expenseId)
        {
            using (var db = _dbFactory())
            {
                var expense = db.Expenses.Find(expenseId);
                if (expense == null) return;
                db.Expenses.Remove(expense);
                db.SaveChanges();
            }
        }

        public List<FiscalPeriod> GetLockedPeriods()
        {
            using (var db = _dbFactory())
            {
                var periods = db.FiscalPeriods.AsNoTracking().Where(p => p.IsLocked)
                    .OrderByDescending(p => p.Year)
                    .ThenByDescending(p => p.Month)
                    .ToList();
                foreach (var period in periods)
                    period.PeriodDisplay = period.Month == 0 ? string.Format("السنة كاملة {0}", period.Year) : string.Format("{0:D2}/{1}", period.Month, period.Year);
                return periods;
            }
        }

        public void LockPeriod(int year, int month, int? userId, string reason)
        {
            using (var db = _dbFactory())
            {
                var start = month == -1 ? new DateTime(year, 1, 1) : new DateTime(year, month + 1, 1);
                var end = month == -1 ? new DateTime(year, 12, 31) : start.AddMonths(1).AddDays(-1);
                var existing = db.FiscalPeriods.FirstOrDefault(p => p.Year == year && p.Month == month);
                if (existing == null)
                {
                    existing = new FiscalPeriod { Year = year, Month = month, StartDate = start, EndDate = end };
                    db.FiscalPeriods.Add(existing);
                }

                existing.IsLocked = true;
                existing.LockedAt = DateTime.Now;
                existing.LockedById = userId;
                existing.LockReason = reason;
                db.SaveChanges();
            }
        }

        public void UnlockPeriod(int periodId)
        {
            using (var db = _dbFactory())
            {
                var period = db.FiscalPeriods.Find(periodId);
                if (period == null) return;
                period.IsLocked = false;
                db.SaveChanges();
            }
        }

        public ProfitLossResult GetProfitLoss(DateTime from, DateTime toExclusive)
        {
            using (var db = _dbFactory())
            {
                var sales = db.SaleInvoices.AsNoTracking().Where(i => i.Date >= from && i.Date < toExclusive).ToList();
                var salesReturns = db.StockMovements.AsNoTracking().Where(s => s.MovementType == "مرتجع مبيعات" && s.Date >= from && s.Date < toExclusive).ToList();
                var purchases = db.PurchaseInvoices.AsNoTracking().Where(i => i.Date >= from && i.Date < toExclusive).ToList();
                var expenses = db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= from && e.ExpenseDate < toExclusive).ToList();

                var result = new ProfitLossResult
                {
                    TotalSales = sales.Sum(i => i.NetAmount),
                    SalesReturns = salesReturns.Sum(s => s.QuantityIn * s.UnitPrice),
                    PurchasesCost = purchases.Sum(i => i.TotalAmount),
                    Expenses = expenses.Sum(e => e.Amount)
                };
                result.NetSales = result.TotalSales - result.SalesReturns;
                result.TotalCost = result.PurchasesCost + result.Expenses;
                result.NetProfit = result.NetSales - result.TotalCost;
                result.ProfitPercent = result.NetSales > 0 ? result.NetProfit / result.NetSales * 100 : 0;

                result.Details.AddRange(sales.Take(50).Select(s => new ProfitLossDetailDto { Date = s.Date, Type = "بيع", Reference = s.InvoiceNumber, Revenue = s.NetAmount }));
                result.Details.AddRange(expenses.Take(50).Select(e => new ProfitLossDetailDto { Date = e.ExpenseDate, Type = "مصروف", Reference = e.ExpenseType, Expense = e.Amount }));
                return result;
            }
        }

        public VATReportResult GetVATReport(DateTime from, DateTime toExclusive)
        {
            using (var db = _dbFactory())
            {
                var dailyData = db.SaleInvoices.AsNoTracking()
                    .Where(i => i.Date >= from && i.Date < toExclusive)
                    .GroupBy(i => DbFunctions.TruncateTime(i.Date))
                    .Select(g => new { Date = g.Key.Value, TotalSales = g.Sum(i => i.TotalAmount), InvoiceCount = g.Count() })
                    .OrderBy(x => x.Date)
                    .ToList();

                var result = new VATReportResult();
                foreach (var day in dailyData)
                {
                    var vat = day.TotalSales * 14m / 114m;
                    var net = day.TotalSales - vat;
                    result.Items.Add(new VATReportLineDto { Date = day.Date, TotalSales = day.TotalSales, VATAmount = vat, NetSales = net, InvoiceCount = day.InvoiceCount });
                    result.TotalSales += day.TotalSales;
                    result.TotalInvoices += day.InvoiceCount;
                }

                result.TotalVAT = result.TotalSales * 14m / 114m;
                result.NetSales = result.TotalSales - result.TotalVAT;
                return result;
            }
        }

        private static bool IsDebitBalance(string balanceType)
        {
            return string.Equals(balanceType, AccountingConstants.Debit, StringComparison.OrdinalIgnoreCase)
                || string.Equals(balanceType, "Debit", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAccountType(string accountType)
        {
            if (string.Equals(accountType, AccountingConstants.Asset, StringComparison.OrdinalIgnoreCase) || string.Equals(accountType, "Asset", StringComparison.OrdinalIgnoreCase)) return AccountingConstants.Asset;
            if (string.Equals(accountType, AccountingConstants.Liability, StringComparison.OrdinalIgnoreCase) || string.Equals(accountType, "Liability", StringComparison.OrdinalIgnoreCase)) return AccountingConstants.Liability;
            if (string.Equals(accountType, AccountingConstants.Equity, StringComparison.OrdinalIgnoreCase) || string.Equals(accountType, "Equity", StringComparison.OrdinalIgnoreCase)) return AccountingConstants.Equity;
            if (string.Equals(accountType, AccountingConstants.Revenue, StringComparison.OrdinalIgnoreCase) || string.Equals(accountType, "Revenue", StringComparison.OrdinalIgnoreCase)) return AccountingConstants.Revenue;
            if (string.Equals(accountType, AccountingConstants.Expense, StringComparison.OrdinalIgnoreCase) || string.Equals(accountType, "Expense", StringComparison.OrdinalIgnoreCase)) return AccountingConstants.Expense;
            return accountType;
        }
    }

    public class AccountSaveRequest
    {
        public int? Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string AccountType { get; set; }
        public int? ParentId { get; set; }
        public decimal OpeningBalance { get; set; }
        public string BalanceType { get; set; }
        public bool IsActive { get; set; }
    }

    public class BalanceSheetResult
    {
        public List<BalanceSheetLineDto> Assets { get; set; } = new List<BalanceSheetLineDto>();
        public List<BalanceSheetLineDto> Liabilities { get; set; } = new List<BalanceSheetLineDto>();
        public List<BalanceSheetLineDto> Equity { get; set; } = new List<BalanceSheetLineDto>();
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public decimal Difference { get; set; }
    }

    public class BalanceSheetLineDto { public string AccountName { get; set; } public decimal Balance { get; set; } }

    public class TrialBalanceResult
    {
        public List<TrialBalanceLineDto> Items { get; set; } = new List<TrialBalanceLineDto>();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Difference { get; set; }
    }

    public class TrialBalanceLineDto { public string AccountCode { get; set; } public string AccountName { get; set; } public decimal Debit { get; set; } public decimal Credit { get; set; } public decimal Balance { get; set; } }

    public class LedgerLineDto { public DateTime Date { get; set; } public string EntryNumber { get; set; } public string Description { get; set; } public decimal Debit { get; set; } public decimal Credit { get; set; } public decimal Balance { get; set; } }

    public class JournalVoucherLineDto { public int AccountId { get; set; } public decimal Debit { get; set; } public decimal Credit { get; set; } public string Notes { get; set; } }

    public class DailyClosingSummaryDto
    {
        public DateTime Date { get; set; }
        public bool IsClosed { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalCashSales { get; set; }
        public decimal TotalCreditSales { get; set; }
        public decimal ExpectedCash { get; set; }
    }

    public class ProfitLossResult
    {
        public decimal TotalSales { get; set; }
        public decimal SalesReturns { get; set; }
        public decimal NetSales { get; set; }
        public decimal PurchasesCost { get; set; }
        public decimal Expenses { get; set; }
        public decimal TotalCost { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitPercent { get; set; }
        public List<ProfitLossDetailDto> Details { get; set; } = new List<ProfitLossDetailDto>();
    }

    public class ProfitLossDetailDto { public DateTime Date { get; set; } public string Type { get; set; } public string Reference { get; set; } public decimal Revenue { get; set; } public decimal Expense { get; set; } }

    public class VATReportResult
    {
        public List<VATReportLineDto> Items { get; set; } = new List<VATReportLineDto>();
        public decimal TotalSales { get; set; }
        public decimal TotalVAT { get; set; }
        public decimal NetSales { get; set; }
        public int TotalInvoices { get; set; }
    }

    public class VATReportLineDto { public DateTime Date { get; set; } public decimal TotalSales { get; set; } public decimal VATAmount { get; set; } public decimal NetSales { get; set; } public int InvoiceCount { get; set; } }
}
