using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using SupermarketPOS.Business.Posting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class PurchaseRequestItem { public int ProductId { get; set; } public int Quantity { get; set; } public decimal UnitPrice { get; set; } public decimal Discount { get; set; } }
    public class PurchaseRequest { public string InvoiceNumber { get; set; } public DateTime Date { get; set; } public int UserId { get; set; } public int? BranchId { get; set; } public string UserRole { get; set; } public int SupplierId { get; set; } public int WarehouseId { get; set; } public decimal PaidAmount { get; set; } public string PaymentType { get; set; } public IList<PurchaseRequestItem> Items { get; set; } }
    public class PurchaseResult { public bool Success { get; set; } public string ErrorMessage { get; set; } public decimal Subtotal { get; set; } public decimal DiscountAmount { get; set; } public decimal TaxAmount { get; set; } public decimal NetAmount { get; set; } }

    public class PurchaseService
    {
        private const decimal TaxRate = 0.14m;
        private readonly Func<AppDbContext> _dbFactory;
        private readonly IInventoryMovementService _inventoryService;
        private readonly AuthorizationService _authzService;
        private readonly AuditService _auditService;
        private readonly SettingsService _settingsService;
        private readonly TransactionExecutor _transactionExecutor;
        private readonly IFiscalPeriodLockChecker _periodLock;

        public PurchaseService(Func<AppDbContext> dbFactory, IInventoryMovementService inventoryService, AuthorizationService authzService, AuditService auditService, SettingsService settingsService, TransactionExecutor transactionExecutor, IFiscalPeriodLockChecker periodLock = null)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _authzService = authzService ?? throw new ArgumentNullException(nameof(authzService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _transactionExecutor = transactionExecutor ?? throw new ArgumentNullException(nameof(transactionExecutor));
            _periodLock = periodLock;
        }

        public List<PurchaseProductDto> GetActiveProducts(int? branchId = null, string userRole = null) { using (var db = _dbFactory()) { var query = db.Products.Where(p => p.IsActive).AsQueryable(); if (userRole != "Admin" && branchId.HasValue) query = query.Where(p => p.BranchId == null || p.BranchId == branchId.Value); return query.OrderBy(p => p.Name).Select(p => new PurchaseProductDto { Id = p.Id, Barcode = p.Barcode, Name = p.Name, PurchasePrice = p.PurchasePrice }).ToList(); } }
        public List<PurchaseSupplierDto> GetSuppliers(int? branchId = null, string userRole = null) { using (var db = _dbFactory()) { var query = db.Suppliers.AsQueryable(); if (userRole != "Admin" && branchId.HasValue) { var ids = db.PurchaseInvoices.Where(p => p.BranchId == branchId.Value).Select(p => p.SupplierId).Distinct(); query = query.Where(s => ids.Contains(s.Id)); } return query.OrderBy(s => s.Name).Select(s => new PurchaseSupplierDto { Id = s.Id, Name = s.Name }).ToList(); } }
        public List<PurchaseWarehouseDto> GetActiveWarehouses() { using (var db = _dbFactory()) { return db.Warehouses.Where(w => w.IsActive).OrderBy(w => w.Name).Select(w => new PurchaseWarehouseDto { Id = w.Id, Name = w.Name, IsDefault = w.IsDefault }).ToList(); } }

        public string GetNextInvoiceNumber() { using (var db = _dbFactory()) { var prefix = _settingsService.Get("invoice.purchase.prefix", "P-"); var nextVal = db.Database.SqlQuery<long>("SELECT NEXT VALUE FOR dbo.PurchaseInvoiceSeq").First(); return prefix + nextVal.ToString(); } }

        public PurchaseResult CreatePurchase(PurchaseRequest request)
        {
            var branchId = request.BranchId;
            if (!_authzService.HasPermission(request.UserId, "purchases.create", branchId)) return Fail("غير مصرح لك بإنشاء فواتير الشراء في هذا الفرع");
            try
            {
                if (_periodLock != null && _periodLock.IsLocked(request.Date)) return Fail("لا يمكن الترحيل إلى فترة محاسبية مُقفلة");
                var priceError = ValidatePricing(request);
                if (priceError != null) return Fail(priceError);
                var result = _transactionExecutor.Execute(db => { var v = Validate(request, db); if (!v.IsValid) return Fail(v.ErrorMessage); var c = Process(v); return Commit(c, db); }, r => r != null && r.Success);
                if (result == null) return Fail("فشل حفظ فاتورة الشراء. حاول مرة أخرى.");
                if (result.Success) _auditService.Log("CREATE_PURCHASE", "Purchase", 0, request.UserId);
                return result;
            }
            catch (Exception ex) { Logger.Error(ex, "CreatePurchase failed"); return Fail("فشل حفظ فاتورة الشراء. حاول مرة أخرى."); }
        }

        private PurchaseValidationResult Validate(PurchaseRequest request, AppDbContext db) { return PurchaseValidationResult.Valid(request, request.Items?.Where(i => i != null && i.ProductId > 0).ToList() ?? new List<PurchaseRequestItem>(), CalculateTotals(request.Items?.Where(i => i != null && i.ProductId > 0).ToList() ?? new List<PurchaseRequestItem>())); }

        private string ValidatePricing(PurchaseRequest request)
        {
            var items = (request.Items ?? new List<PurchaseRequestItem>())
                .Where(i => i != null && i.ProductId > 0)
                .Select(i => new PriceIntegrity.Line(i.Quantity, i.UnitPrice, i.Discount));
            var maxLine = _settingsService.GetDecimal("price.max_line_discount_pct", 100m);
            var maxDoc = _settingsService.GetDecimal("price.max_document_discount_pct", 100m);
            var limits = new PriceIntegrity.Limits(maxLine, maxDoc);
            return PriceIntegrity.Validate(items, 0m, limits);
        }
        private PurchaseCommand Process(PurchaseValidationResult v) { return new PurchaseCommand { Request = v.Request, Items = v.Items, Totals = v.Totals, StockCommands = v.Items.GroupBy(i => i.ProductId).Select(g => new PurchaseStockCommand { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity), UnitPrice = g.OrderByDescending(x => x.Quantity).First().UnitPrice }).ToList() }; }

        private PurchaseResult Commit(PurchaseCommand command, AppDbContext db)
        {
            var branchId = command.Request.BranchId;
            var invoice = new PurchaseInvoice { InvoiceNumber = command.Request.InvoiceNumber, Date = command.Request.Date, SupplierId = command.Request.SupplierId, WarehouseId = command.Request.WarehouseId, TotalAmount = command.Totals.NetAmount, Discount = command.Totals.DiscountAmount, PaidAmount = command.Request.PaidAmount, PaymentType = command.Request.PaymentType, BranchId = branchId };
            db.PurchaseInvoices.Add(invoice);
            foreach (var item in command.Items) db.PurchaseItems.Add(new PurchaseItem { PurchaseInvoice = invoice, ProductId = item.ProductId, Quantity = item.Quantity, UnitPrice = item.UnitPrice, TotalPrice = (item.Quantity * item.UnitPrice) - item.Discount });
            foreach (var sc in command.StockCommands) { string err; if (!_inventoryService.IncreaseStockAndRecordMovement(db, sc.ProductId, command.Request.WarehouseId, sc.Quantity, sc.UnitPrice, invoice.InvoiceNumber, "شراء", out err)) throw new InvalidOperationException(err); }
            var supplier = db.Suppliers.Find(command.Request.SupplierId); supplier.Balance += command.Totals.NetAmount - command.Request.PaidAmount;
            AddPurchaseJournalEntry(db, command.Request, command.Totals.NetAmount, invoice.InvoiceNumber);
            db.SaveChanges();
            return new PurchaseResult { Success = true, Subtotal = command.Totals.Subtotal, DiscountAmount = command.Totals.DiscountAmount, TaxAmount = command.Totals.TaxAmount, NetAmount = command.Totals.NetAmount };
        }

        private static PurchaseTotals CalculateTotals(IList<PurchaseRequestItem> items) { var s = items.Sum(i => i.Quantity * i.UnitPrice); var d = items.Sum(i => i.Discount); var a = s - d; var t = a * TaxRate; return new PurchaseTotals { Subtotal = s, DiscountAmount = d, TaxAmount = t, NetAmount = a + t }; }
        private static PurchaseResult Fail(string e) => new PurchaseResult { Success = false, ErrorMessage = e };
        private static void AddPurchaseJournalEntry(AppDbContext db, PurchaseRequest request, decimal netAmount, string invoiceNumber)
        {
            var inventoryAccount = GetOrCreateAccount(db, "1.2.1", "Inventory", AccountingConstants.Asset);
            var cashAccount = GetOrCreateAccount(db, "1.1.1", "Cash", AccountingConstants.Asset);
            var payableAccount = GetOrCreateAccount(db, "2.1.1", "Accounts Payable", AccountingConstants.Liability);

            var paidAmount = request.PaidAmount < 0m ? 0m : request.PaidAmount;
            if (paidAmount > netAmount) paidAmount = netAmount;
            var remainingAmount = netAmount - paidAmount;

            var journal = new JournalEntry
            {
                Date = request.Date,
                EntryNumber = BuildEntryNumber("PUR", invoiceNumber),
                Description = "Purchase invoice " + invoiceNumber,
                SourceType = "Purchase",
                UserId = request.UserId
            };
            db.JournalEntries.Add(journal);

            db.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntry = journal,
                Account = inventoryAccount,
                Description = "Inventory purchase",
                Debit = netAmount,
                Credit = 0m
            });

            if (paidAmount > 0m)
            {
                db.JournalEntryLines.Add(new JournalEntryLine
                {
                    JournalEntry = journal,
                    Account = cashAccount,
                    Description = "Cash paid",
                    Debit = 0m,
                    Credit = paidAmount
                });
            }

            if (remainingAmount > 0m)
            {
                db.JournalEntryLines.Add(new JournalEntryLine
                {
                    JournalEntry = journal,
                    Account = payableAccount,
                    Description = "Supplier payable",
                    Debit = 0m,
                    Credit = remainingAmount
                });
            }

            JournalBalance.EnsureBalanced(new[]
            {
                new JournalBalance.Line(netAmount, 0m),
                new JournalBalance.Line(0m, paidAmount),
                new JournalBalance.Line(0m, remainingAmount)
            });
        }

        private static Account GetOrCreateAccount(AppDbContext db, string code, string name, string accountType)
        {
            var account = db.Accounts.FirstOrDefault(a => a.Code == code);
            if (account != null)
            {
                account.AccountType = accountType;
                account.BalanceType = accountType == AccountingConstants.Liability || accountType == AccountingConstants.Revenue
                    ? AccountingConstants.Credit
                    : AccountingConstants.Debit;
                return account;
            }

            account = new Account
            {
                Code = code,
                Name = name,
                AccountType = accountType,
                IsActive = true,
                IsParent = false,
                BalanceType = accountType == AccountingConstants.Liability || accountType == AccountingConstants.Revenue
                    ? AccountingConstants.Credit
                    : AccountingConstants.Debit
            };
            db.Accounts.Add(account);
            return account;
        }

        private static string BuildEntryNumber(string prefix, string invoiceNumber)
        {
            var safeInvoiceNumber = string.IsNullOrWhiteSpace(invoiceNumber)
                ? DateTime.Now.ToString("yyyyMMddHHmmssfff")
                : invoiceNumber.Replace(" ", string.Empty);

            var entryNumber = prefix + "-" + safeInvoiceNumber;
            return entryNumber.Length <= 50 ? entryNumber : entryNumber.Substring(0, 50);
        }

        private class PurchaseTotals { public decimal Subtotal { get; set; } public decimal DiscountAmount { get; set; } public decimal TaxAmount { get; set; } public decimal NetAmount { get; set; } }
        private class PurchaseValidationResult { public bool IsValid { get; set; } public string ErrorMessage { get; set; } public PurchaseRequest Request { get; set; } public IList<PurchaseRequestItem> Items { get; set; } public PurchaseTotals Totals { get; set; } public static PurchaseValidationResult Valid(PurchaseRequest r, IList<PurchaseRequestItem> i, PurchaseTotals t) => new PurchaseValidationResult { IsValid = true, Request = r, Items = i, Totals = t }; public static PurchaseValidationResult Fail(string e) => new PurchaseValidationResult { IsValid = false, ErrorMessage = e }; }
        private class PurchaseCommand { public PurchaseRequest Request { get; set; } public IList<PurchaseRequestItem> Items { get; set; } public PurchaseTotals Totals { get; set; } public IList<PurchaseStockCommand> StockCommands { get; set; } }
        private class PurchaseStockCommand { public int ProductId { get; set; } public int Quantity { get; set; } public decimal UnitPrice { get; set; } }
    }

    public class PurchaseProductDto { public int Id { get; set; } public string Barcode { get; set; } public string Name { get; set; } public decimal PurchasePrice { get; set; } }
    public class PurchaseSupplierDto { public int Id { get; set; } public string Name { get; set; } }
    public class PurchaseWarehouseDto { public int Id { get; set; } public string Name { get; set; } public bool IsDefault { get; set; } }
}
