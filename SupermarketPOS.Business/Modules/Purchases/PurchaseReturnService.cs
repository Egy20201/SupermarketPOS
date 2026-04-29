using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business.Modules
{
    public class PurchaseReturnService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly IInventoryMovementService _inventoryService;
        private readonly TransactionExecutor _transactionExecutor;

        public PurchaseReturnService(Func<AppDbContext> dbFactory, IInventoryMovementService inventoryService, TransactionExecutor transactionExecutor)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _transactionExecutor = transactionExecutor ?? throw new ArgumentNullException(nameof(transactionExecutor));
        }

        public List<PurchaseInvoice> GetRecentInvoices(int limit)
        {
            using (var db = _dbFactory())
            {
                return db.PurchaseInvoices.AsNoTracking().OrderByDescending(i => i.Date).Take(limit).ToList();
            }
        }

        public PurchaseReturnInvoiceDto GetInvoice(int invoiceId)
        {
            using (var db = _dbFactory())
            {
                var invoice = db.PurchaseInvoices.AsNoTracking().FirstOrDefault(i => i.Id == invoiceId);
                if (invoice == null) return null;
                var supplier = invoice.SupplierId.HasValue ? db.Suppliers.AsNoTracking().FirstOrDefault(s => s.Id == invoice.SupplierId.Value) : null;
                var items = db.PurchaseItems.AsNoTracking().Where(pi => pi.PurchaseInvoiceId == invoiceId).ToList();
                var productNames = db.Products.AsNoTracking().ToDictionary(p => p.Id, p => p.Name);

                return new PurchaseReturnInvoiceDto
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    Date = invoice.Date,
                    TotalAmount = invoice.TotalAmount,
                    SupplierId = invoice.SupplierId,
                    SupplierName = supplier == null ? null : supplier.Name,
                    SupplierBalance = supplier == null ? 0 : supplier.Balance,
                    Items = items.Select(i =>
                    {
                        string name;
                        if (!productNames.TryGetValue(i.ProductId, out name)) name = "-";
                        return new PurchaseReturnLineDto { SourceItemId = i.Id, ProductId = i.ProductId, ProductName = name, OriginalQuantity = i.Quantity, UnitPrice = i.UnitPrice };
                    }).ToList()
                };
            }
        }

        public decimal PreviewSupplierBalanceAfterReturn(int? supplierId, decimal returnTotal)
        {
            if (!supplierId.HasValue) return 0m;
            using (var db = _dbFactory())
            {
                var supplier = db.Suppliers.AsNoTracking().FirstOrDefault(s => s.Id == supplierId.Value);
                return supplier == null ? 0 : supplier.Balance - returnTotal;
            }
        }

        public void SaveReturn(int invoiceId, int warehouseId, IEnumerable<PurchaseReturnSaveLineDto> items)
        {
            var returnItems = (items ?? Enumerable.Empty<PurchaseReturnSaveLineDto>()).Where(i => i.ReturnQuantity > 0).ToList();
            if (!returnItems.Any()) throw new InvalidOperationException("لا توجد أصناف مرتجعة");

            _transactionExecutor.Execute(db =>
            {
                var invoice = db.PurchaseInvoices.Find(invoiceId);
                if (invoice == null) throw new InvalidOperationException("الفاتورة غير موجودة");
                var reference = string.Format("PRET-{0}-{1:yyyyMMddHHmmss}", invoice.InvoiceNumber, DateTime.Now);
                var total = returnItems.Sum(i => i.ReturnQuantity * i.UnitPrice);
                ValidateReturnQuantities(db, invoice, returnItems);

                foreach (var item in returnItems)
                {
                    string error;
                    if (!_inventoryService.DecreaseStockAndRecordMovement(db, item.ProductId, warehouseId, item.ReturnQuantity, item.UnitPrice, reference, "مرتجع مشتريات", out error))
                        throw new InvalidOperationException(error + ": " + item.ProductName);
                }

                if (invoice.SupplierId.HasValue)
                {
                    var supplier = db.Suppliers.Find(invoice.SupplierId.Value);
                    if (supplier != null) supplier.Balance -= total;
                }

                AddPurchaseReturnJournalEntry(db, invoice, total, reference);
                db.SaveChanges();
                return true;
            }, committed => committed);
        }

        private static void ValidateReturnQuantities(AppDbContext db, PurchaseInvoice invoice, IEnumerable<PurchaseReturnSaveLineDto> returnItems)
        {
            var sourceItems = db.PurchaseItems.Where(i => i.PurchaseInvoiceId == invoice.Id).ToList();
            foreach (var item in returnItems)
            {
                var purchasedQuantity = sourceItems.Where(i => i.Id == item.SourceItemId && i.ProductId == item.ProductId).Sum(i => i.Quantity);
                if (purchasedQuantity <= 0) throw new InvalidOperationException("الصنف غير موجود في الفاتورة: " + item.ProductName);

                var priorReturned = db.StockMovements
                    .Where(m => m.ProductId == item.ProductId && m.MovementType == "مرتجع مشتريات" && m.Reference.StartsWith("PRET-" + invoice.InvoiceNumber + "-"))
                    .Sum(m => (int?)m.QuantityOut) ?? 0;

                if (item.ReturnQuantity + priorReturned > purchasedQuantity)
                    throw new InvalidOperationException("كمية المرتجع أكبر من الكمية المشتراة: " + item.ProductName);
            }
        }

        private static void AddPurchaseReturnJournalEntry(AppDbContext db, PurchaseInvoice invoice, decimal total, string reference)
        {
            var inventoryAccount = GetOrCreateAccount(db, "1.2.1", "Inventory", AccountingConstants.Asset);
            var creditAccount = invoice.SupplierId.HasValue
                ? GetOrCreateAccount(db, "2.1.1", "Accounts Payable", AccountingConstants.Liability)
                : GetOrCreateAccount(db, "1.1.1", "Cash", AccountingConstants.Asset);

            var journal = new JournalEntry
            {
                Date = DateTime.Now,
                EntryNumber = BuildEntryNumber("PRET", reference),
                Description = "Purchase return " + invoice.InvoiceNumber,
                SourceType = "PurchaseReturn",
                UserId = 0
            };
            db.JournalEntries.Add(journal);
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, Account = creditAccount, Description = invoice.SupplierId.HasValue ? "Reverse supplier payable" : "Reverse cash purchase", Debit = total, Credit = 0m });
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, Account = inventoryAccount, Description = "Reverse inventory purchase", Debit = 0m, Credit = total });
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

    public class PurchaseReturnInvoiceDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; }
        public decimal SupplierBalance { get; set; }
        public List<PurchaseReturnLineDto> Items { get; set; } = new List<PurchaseReturnLineDto>();
    }

    public class PurchaseReturnLineDto
    {
        public int SourceItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int OriginalQuantity { get; set; }
        public int ReturnQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get { return ReturnQuantity * UnitPrice; } }
    }

    public class PurchaseReturnSaveLineDto
    {
        public int SourceItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int ReturnQuantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
