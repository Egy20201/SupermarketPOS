using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using SupermarketPOS.Business.Posting;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business.Modules
{
    public class SalesReturnService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly IInventoryMovementService _inventoryService;
        private readonly TransactionExecutor _transactionExecutor;
        private readonly IFiscalPeriodLockChecker _periodLock;

        public SalesReturnService(Func<AppDbContext> dbFactory, IInventoryMovementService inventoryService, TransactionExecutor transactionExecutor, IFiscalPeriodLockChecker periodLock = null)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _transactionExecutor = transactionExecutor ?? throw new ArgumentNullException(nameof(transactionExecutor));
            _periodLock = periodLock;
        }

        public List<SaleInvoice> GetRecentInvoices(int limit)
        {
            using (var db = _dbFactory())
            {
                return db.SaleInvoices.AsNoTracking().OrderByDescending(i => i.Date).Take(limit).ToList();
            }
        }

        public SalesReturnInvoiceDto GetInvoice(int invoiceId)
        {
            using (var db = _dbFactory())
            {
                var invoice = db.SaleInvoices.AsNoTracking().FirstOrDefault(i => i.Id == invoiceId);
                if (invoice == null) return null;
                var customer = invoice.CustomerId.HasValue ? db.Customers.AsNoTracking().FirstOrDefault(c => c.Id == invoice.CustomerId.Value) : null;
                var items = db.SaleItems.AsNoTracking().Where(si => si.SaleInvoiceId == invoiceId).ToList();
                var productNames = db.Products.AsNoTracking().ToDictionary(p => p.Id, p => p.Name);

                return new SalesReturnInvoiceDto
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    Date = invoice.Date,
                    NetAmount = invoice.NetAmount,
                    CustomerId = invoice.CustomerId,
                    CustomerName = customer == null ? null : customer.Name,
                    CustomerBalance = customer == null ? 0 : customer.Balance,
                    Items = items.Select(i =>
                    {
                        string name;
                        if (!productNames.TryGetValue(i.ProductId, out name)) name = "-";
                        return new ReturnLineDto { SourceItemId = i.Id, ProductId = i.ProductId, ProductName = name, OriginalQuantity = i.Quantity, UnitPrice = i.UnitPrice };
                    }).ToList()
                };
            }
        }

        public decimal PreviewCustomerBalanceAfterReturn(int? customerId, decimal returnTotal)
        {
            if (!customerId.HasValue) return 0m;
            using (var db = _dbFactory())
            {
                var customer = db.Customers.AsNoTracking().FirstOrDefault(c => c.Id == customerId.Value);
                return customer == null ? 0 : customer.Balance - returnTotal;
            }
        }

        public void SaveReturn(int invoiceId, int warehouseId, IEnumerable<ReturnSaveLineDto> items)
        {
            var returnItems = (items ?? Enumerable.Empty<ReturnSaveLineDto>()).Where(i => i.ReturnQuantity > 0).ToList();
            if (!returnItems.Any()) throw new InvalidOperationException("لا توجد أصناف مرتجعة");

            _transactionExecutor.Execute(db =>
            {
                var invoice = db.SaleInvoices.Find(invoiceId);
                if (invoice == null) throw new InvalidOperationException("الفاتورة غير موجودة");
                if (_periodLock != null) _periodLock.EnsureUnlocked(DateTime.Now);
                var reference = string.Format("RET-{0}-{1:yyyyMMddHHmmss}", invoice.InvoiceNumber, DateTime.Now);
                var total = returnItems.Sum(i => i.ReturnQuantity * i.UnitPrice);
                ValidateReturnQuantities(db, invoice, returnItems);

                foreach (var item in returnItems)
                {
                    string error;
                    if (!_inventoryService.IncreaseStockAndRecordMovement(db, item.ProductId, warehouseId, item.ReturnQuantity, item.UnitPrice, reference, "مرتجع مبيعات", out error))
                        throw new InvalidOperationException(error + ": " + item.ProductName);
                }

                if (invoice.CustomerId.HasValue)
                {
                    var customer = db.Customers.Find(invoice.CustomerId.Value);
                    if (customer != null) customer.Balance -= total;
                }

                AddSalesReturnJournalEntry(db, invoice, total, reference);
                db.SaveChanges();
                return true;
            }, committed => committed);
        }

        private static void ValidateReturnQuantities(AppDbContext db, SaleInvoice invoice, IEnumerable<ReturnSaveLineDto> returnItems)
        {
            var sourceItems = db.SaleItems.Where(i => i.SaleInvoiceId == invoice.Id).ToList();
            foreach (var item in returnItems)
            {
                var soldQuantity = sourceItems.Where(i => i.Id == item.SourceItemId && i.ProductId == item.ProductId).Sum(i => i.Quantity);
                if (soldQuantity <= 0) throw new InvalidOperationException("الصنف غير موجود في الفاتورة: " + item.ProductName);

                var priorReturned = db.StockMovements
                    .Where(m => m.ProductId == item.ProductId && m.MovementType == "مرتجع مبيعات" && m.Reference.StartsWith("RET-" + invoice.InvoiceNumber + "-"))
                    .Sum(m => (int?)m.QuantityIn) ?? 0;

                ReturnQuantityGuard.EnsureAllowed(soldQuantity, priorReturned, item.ReturnQuantity, item.ProductName);
            }
        }

        private static void AddSalesReturnJournalEntry(AppDbContext db, SaleInvoice invoice, decimal total, string reference)
        {
            var revenueAccount = GetOrCreateAccount(db, "4.1.1", "Sales Revenue", AccountingConstants.Revenue);
            var debitAccount = invoice.CustomerId.HasValue
                ? GetOrCreateAccount(db, "1.1.2", "Accounts Receivable", AccountingConstants.Asset)
                : GetOrCreateAccount(db, "1.1.1", "Cash", AccountingConstants.Asset);

            var journal = new JournalEntry
            {
                Date = DateTime.Now,
                EntryNumber = BuildEntryNumber("SRET", reference),
                Description = "Sales return " + invoice.InvoiceNumber,
                SourceType = "SalesReturn",
                UserId = invoice.UserId
            };
            db.JournalEntries.Add(journal);
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, Account = revenueAccount, Description = "Reverse sales revenue", Debit = total, Credit = 0m });
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, Account = debitAccount, Description = invoice.CustomerId.HasValue ? "Reverse customer receivable" : "Reverse cash sale", Debit = 0m, Credit = total });
            JournalBalance.EnsureBalanced(new[] { new JournalBalance.Line(total, 0m), new JournalBalance.Line(0m, total) });
        }

        private static Account GetOrCreateAccount(AppDbContext db, string code, string name, string accountType)
        {
            var account = db.Accounts.FirstOrDefault(a => a.Code == code);
            if (account != null) return account;
            account = new Account { Code = code, Name = name, AccountType = accountType, IsActive = true, IsParent = false, BalanceType = accountType == AccountingConstants.Revenue || accountType == AccountingConstants.Liability ? AccountingConstants.Credit : AccountingConstants.Debit };
            db.Accounts.Add(account);
            return account;
        }

        private static string BuildEntryNumber(string prefix, string reference)
        {
            var entryNumber = prefix + "-" + reference.Replace(" ", string.Empty);
            return entryNumber.Length <= 50 ? entryNumber : entryNumber.Substring(0, 50);
        }
    }

    public class SalesReturnInvoiceDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime Date { get; set; }
        public decimal NetAmount { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal CustomerBalance { get; set; }
        public List<ReturnLineDto> Items { get; set; } = new List<ReturnLineDto>();
    }

    public class ReturnLineDto
    {
        public int SourceItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int OriginalQuantity { get; set; }
        public int ReturnQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get { return ReturnQuantity * UnitPrice; } }
    }

    public class ReturnSaveLineDto
    {
        public int SourceItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int ReturnQuantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
