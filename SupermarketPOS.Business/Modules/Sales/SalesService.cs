using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class SaleRequestItem { public int ProductId { get; set; } public string ProductName { get; set; } public int Quantity { get; set; } public decimal UnitPrice { get; set; } public int? ProductUnitId { get; set; } public string UnitName { get; set; } public decimal ConversionFactor { get; set; } = 1m; }
    public class SaleRequest { public string InvoiceNumber { get; set; } public DateTime Date { get; set; } public int UserId { get; set; } public int? BranchId { get; set; } public string UserRole { get; set; } public int? CustomerId { get; set; } public int WarehouseId { get; set; } public bool ApplyDiscount { get; set; } public decimal DiscountPercent { get; set; } public bool ApplyTax { get; set; } public IList<SaleRequestItem> Items { get; set; } }
    public class SaleResult { public bool Success { get; set; } public string ErrorMessage { get; set; } public int SaleId { get; set; } public decimal Subtotal { get; set; } public decimal DiscountAmount { get; set; } public decimal TaxAmount { get; set; } public decimal NetAmount { get; set; } }

    public class SalesService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly ConfigurationService _configurationService;
        private readonly IInventoryMovementService _inventoryService;
        private readonly FeatureFlagService _featureFlagService;
        private readonly AuthorizationService _authzService;
        private readonly SettingsService _settingsService;
        private readonly AuditService _auditService;
        private readonly RuleExecutor _ruleExecutor;
        private readonly TransactionExecutor _transactionExecutor;

        public SalesService(Func<AppDbContext> dbFactory, ConfigurationService configurationService, IInventoryMovementService inventoryService, FeatureFlagService featureFlagService, AuthorizationService authzService, SettingsService settingsService, AuditService auditService, TransactionExecutor transactionExecutor)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _authzService = authzService ?? throw new ArgumentNullException(nameof(authzService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _ruleExecutor = new RuleExecutor(new[] { new ValidateSaleRule() });
            _transactionExecutor = transactionExecutor ?? throw new ArgumentNullException(nameof(transactionExecutor));
        }

        public int GetDefaultWarehouseId() { using (var db = _dbFactory()) { var w = db.Warehouses.FirstOrDefault(x => x.IsDefault) ?? db.Warehouses.FirstOrDefault(); return w?.Id ?? 1; } }

        public List<SaleCustomerDto> GetActiveCustomers(int? branchId = null, string userRole = null)
        {
            using (var db = _dbFactory())
            {
                var query = db.Customers.Where(c => c.IsActive);
                if (userRole != "Admin" && branchId.HasValue)
                {
                    var branchCustomerIds = db.SaleInvoices.Where(s => s.BranchId == branchId.Value && s.CustomerId != null).Select(s => s.CustomerId.Value).Distinct();
                    query = query.Where(c => branchCustomerIds.Contains(c.Id));
                }
                return query.OrderBy(c => c.Name).Select(c => new SaleCustomerDto { Id = c.Id, Name = c.Name }).ToList();
            }
        }

        public string GetNextInvoiceNumber() { using (var db = _dbFactory()) { var prefix = _settingsService.Get("invoice.sales.prefix", "S-"); var nextVal = db.Database.SqlQuery<long>("SELECT NEXT VALUE FOR dbo.SalesInvoiceSeq").First(); return prefix + nextVal.ToString(); } }

        public List<RecentInvoiceDto> GetRecentInvoices(int count = 20, int? branchId = null, string userRole = null)
        {
            using (var db = _dbFactory())
            {
                var query = db.SaleInvoices.AsNoTracking().AsQueryable();
                if (userRole != "Admin" && branchId.HasValue) query = query.Where(i => i.BranchId == branchId.Value);
                return query.OrderByDescending(i => i.Date).Take(count).Select(i => new RecentInvoiceDto { Id = i.Id, InvoiceNumber = i.InvoiceNumber, CustomerName = i.Customer != null ? i.Customer.Name : "عميل نقدي", Date = i.Date, Total = i.NetAmount }).ToList();
            }
        }

        public SaleProductLookupResult FindProductByBarcode(string barcode, int warehouseId, int? branchId = null, string userRole = null)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return SaleProductLookupResult.Fail("المنتج غير موجود");
            using (var db = _dbFactory())
            {
                var productQuery = db.Products.Where(p => p.IsActive).AsQueryable();
                if (userRole != "Admin" && branchId.HasValue) productQuery = productQuery.Where(p => p.BranchId == null || p.BranchId == branchId.Value);
                var product = productQuery.FirstOrDefault(p => p.Barcode == barcode);
                int? matchedProductUnitId = null; string matchedUnitName = null; decimal matchedConversion = 1m; decimal matchedPrice = 0m;
                if (product == null)
                {
                    var productUnit = db.ProductUnits.Include("Product").Include("Unit").FirstOrDefault(pu => pu.Barcode == barcode && pu.Product.IsActive);
                    if (productUnit == null) return SaleProductLookupResult.Fail("المنتج غير موجود");
                    product = productUnit.Product; matchedProductUnitId = productUnit.Id; matchedUnitName = productUnit.Unit.Name; matchedConversion = productUnit.ConversionFactor; matchedPrice = productUnit.SellingPrice;
                }
                else { matchedPrice = product.SellingPrice; var baseUnit = db.ProductUnits.Include("Unit").FirstOrDefault(pu => pu.ProductId == product.Id && pu.IsBaseUnit); if (baseUnit != null) { matchedProductUnitId = baseUnit.Id; matchedUnitName = baseUnit.Unit.Name; matchedConversion = baseUnit.ConversionFactor; matchedPrice = baseUnit.SellingPrice; } }
                var stock = db.ProductStocks.FirstOrDefault(ps => ps.ProductId == product.Id && ps.WarehouseId == warehouseId);
                if (stock == null || stock.Quantity <= 0) return SaleProductLookupResult.Fail("المنتج غير متوفر");
                var availableUnits = db.ProductUnits.Include("Unit").Where(pu => pu.ProductId == product.Id).OrderByDescending(pu => pu.IsBaseUnit).Select(pu => new SaleProductUnitOption { ProductUnitId = pu.Id, UnitId = pu.UnitId, UnitName = pu.Unit.Name, ConversionFactor = pu.ConversionFactor, SellingPrice = pu.SellingPrice, Barcode = pu.Barcode, IsBaseUnit = pu.IsBaseUnit }).ToList();
                return SaleProductLookupResult.Ok(new SaleProductDto { ProductId = product.Id, ProductCode = product.Barcode, ProductName = product.Name, UnitPrice = matchedPrice, ProductUnitId = matchedProductUnitId, UnitName = matchedUnitName, ConversionFactor = matchedConversion, AvailableUnits = availableUnits });
            }
        }

        public SaleResult CreateSale(SaleRequest request)
        {
            var branchId = request.BranchId;
            if (!_authzService.HasPermission(request.UserId, "sales.create", branchId)) return Fail("غير مصرح لك بإنشاء فواتير المبيعات في هذا الفرع");
            _configurationService.RefreshConfiguration();
            if (!_featureFlagService.IsEnabled("sales")) return Fail("تم تعطيل المبيعات");
            try
            {
                var saleResult = _transactionExecutor.Execute(db => { var validation = Validate(request, db); if (!validation.IsValid) return Fail(validation.ErrorMessage); var command = Process(validation); return Commit(command, db); }, r => r != null && r.Success);
                if (saleResult == null) return Fail("فشل حفظ الفاتورة. حاول مرة أخرى.");
                if (!saleResult.Success) return saleResult;
                _auditService.Log("CREATE_SALE", "Sale", saleResult.SaleId, request.UserId);
                return saleResult;
            }
            catch (Exception ex) { Logger.Error(ex, "CreateSale failed"); return Fail("فشل حفظ الفاتورة. حاول مرة أخرى."); }
        }

        private SaleValidationResult Validate(SaleRequest request, AppDbContext db) { /* unchanged */ return SaleValidationResult.Valid(request, request.Items?.Where(i => i != null && i.ProductId > 0).ToList() ?? new List<SaleRequestItem>(), CalculateTotals(request.Items?.Where(i => i != null && i.ProductId > 0).ToList() ?? new List<SaleRequestItem>(), request.ApplyDiscount, request.DiscountPercent, request.ApplyTax)); }
        private SaleCommand Process(SaleValidationResult validation) => new SaleCommand { Request = validation.Request, Items = validation.Items, Totals = validation.Totals };

        private SaleResult Commit(SaleCommand command, AppDbContext db)
        {
            var branchId = command.Request.BranchId;
            var invoice = new SaleInvoice { InvoiceNumber = command.Request.InvoiceNumber, Date = command.Request.Date, UserId = command.Request.UserId, CustomerId = command.Request.CustomerId, Discount = command.Totals.DiscountAmount, TotalAmount = command.Totals.Subtotal, NetAmount = command.Totals.NetAmount, BranchId = branchId };
            db.SaleInvoices.Add(invoice);
            foreach (var item in command.Items) { var baseQty = (int)Math.Ceiling(item.Quantity * item.ConversionFactor); db.SaleItems.Add(new SaleItem { SaleInvoice = invoice, ProductId = item.ProductId, Quantity = item.Quantity, UnitPrice = item.UnitPrice, TotalPrice = item.Quantity * item.UnitPrice, ProductUnitId = item.ProductUnitId, UnitName = item.UnitName, ConversionFactor = item.ConversionFactor, BaseQuantity = baseQty }); string err; if (!_inventoryService.DecreaseStockAndRecordMovement(db, item.ProductId, command.Request.WarehouseId, baseQty, item.UnitPrice, invoice.InvoiceNumber, "بيع", out err)) throw new InvalidOperationException(err); }
            AddSalesJournalEntry(db, command.Request, command.Totals.NetAmount, invoice.InvoiceNumber);
            db.SaveChanges();
            return new SaleResult { Success = true, SaleId = invoice.Id, Subtotal = command.Totals.Subtotal, DiscountAmount = command.Totals.DiscountAmount, TaxAmount = command.Totals.TaxAmount, NetAmount = command.Totals.NetAmount };
        }

        private SaleTotals CalculateTotals(IList<SaleRequestItem> items, bool applyDiscount, decimal discountPercent, bool applyTax) { var subtotal = items.Sum(i => i.Quantity * i.UnitPrice); var discount = applyDiscount ? subtotal * discountPercent / 100 : 0; var afterDiscount = subtotal - discount; var taxRate = _settingsService.GetDecimal("tax.rate", 14m); var tax = applyTax ? afterDiscount * taxRate / 100 : 0; var net = afterDiscount + tax; return new SaleTotals { Subtotal = subtotal, DiscountAmount = discount, TaxAmount = tax, NetAmount = net }; }
        private static SaleResult Fail(string msg) { Logger.Info("Sale failed: " + msg); return new SaleResult { Success = false, ErrorMessage = msg }; }
        private static void AddSalesJournalEntry(AppDbContext db, SaleRequest request, decimal netAmount, string invoiceNumber)
        {
            var debitAccount = request.CustomerId.HasValue
                ? GetOrCreateAccount(db, "1.1.2", "Accounts Receivable", AccountingConstants.Asset)
                : GetOrCreateAccount(db, "1.1.1", "Cash", AccountingConstants.Asset);
            var revenueAccount = GetOrCreateAccount(db, "4.1.1", "Sales Revenue", AccountingConstants.Revenue);

            var journal = new JournalEntry
            {
                Date = request.Date,
                EntryNumber = BuildEntryNumber("SAL", invoiceNumber),
                Description = "Sales invoice " + invoiceNumber,
                SourceType = "Sale",
                UserId = request.UserId
            };
            db.JournalEntries.Add(journal);

            db.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntry = journal,
                Account = debitAccount,
                Description = request.CustomerId.HasValue ? "Customer receivable" : "Cash sale",
                Debit = netAmount,
                Credit = 0m
            });

            db.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntry = journal,
                Account = revenueAccount,
                Description = "Sales revenue",
                Debit = 0m,
                Credit = netAmount
            });
        }

        private static Account GetOrCreateAccount(AppDbContext db, string code, string name, string accountType)
        {
            var account = db.Accounts.FirstOrDefault(a => a.Code == code);
            if (account != null)
            {
                account.AccountType = accountType;
                account.BalanceType = accountType == AccountingConstants.Revenue || accountType == AccountingConstants.Liability
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
                BalanceType = accountType == AccountingConstants.Revenue || accountType == AccountingConstants.Liability
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

        private class SaleTotals { public decimal Subtotal { get; set; } public decimal DiscountAmount { get; set; } public decimal TaxAmount { get; set; } public decimal NetAmount { get; set; } }
        private class SaleValidationResult { public bool IsValid { get; set; } public string ErrorMessage { get; set; } public SaleRequest Request { get; set; } public IList<SaleRequestItem> Items { get; set; } public SaleTotals Totals { get; set; } public static SaleValidationResult Valid(SaleRequest r, IList<SaleRequestItem> i, SaleTotals t) => new SaleValidationResult { IsValid = true, Request = r, Items = i, Totals = t }; public static SaleValidationResult Fail(string e) => new SaleValidationResult { IsValid = false, ErrorMessage = e }; }
        private class SaleCommand { public SaleRequest Request { get; set; } public IList<SaleRequestItem> Items { get; set; } public SaleTotals Totals { get; set; } }
    }

    public class SaleCustomerDto { public int Id { get; set; } public string Name { get; set; } }
    public class SaleProductDto { public int ProductId { get; set; } public string ProductCode { get; set; } public string ProductName { get; set; } public decimal UnitPrice { get; set; } public int? ProductUnitId { get; set; } public string UnitName { get; set; } public decimal ConversionFactor { get; set; } = 1m; public List<SaleProductUnitOption> AvailableUnits { get; set; } }
    public class SaleProductUnitOption { public int ProductUnitId { get; set; } public int UnitId { get; set; } public string UnitName { get; set; } public decimal ConversionFactor { get; set; } public decimal SellingPrice { get; set; } public string Barcode { get; set; } public bool IsBaseUnit { get; set; } }
    public class SaleProductLookupResult { public bool Success { get; set; } public string ErrorMessage { get; set; } public SaleProductDto Product { get; set; } public static SaleProductLookupResult Ok(SaleProductDto p) => new SaleProductLookupResult { Success = true, Product = p }; public static SaleProductLookupResult Fail(string e) => new SaleProductLookupResult { Success = false, ErrorMessage = e }; }
    public class RecentInvoiceDto { public int Id { get; set; } public string InvoiceNumber { get; set; } public string CustomerName { get; set; } public DateTime Date { get; set; } public decimal Total { get; set; } }
}
