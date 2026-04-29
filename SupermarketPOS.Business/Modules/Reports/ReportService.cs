using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class ReportService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly AuthorizationService _authzService;

        public ReportService(Func<AppDbContext> dbFactory, AuthorizationService authzService)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _authzService = authzService ?? throw new ArgumentNullException(nameof(authzService));
        }

        public DailySalesReportDto GetDailySales(DateTime date, int? branchId = null, string userRole = null)
        {
            var from = date.Date; var to = from.AddDays(1);
            using (var db = _dbFactory())
            {
                var query = db.SaleInvoices.Where(i => i.Date >= from && i.Date < to);
                if (userRole != "Admin" && branchId.HasValue) query = query.Where(i => i.BranchId == branchId.Value);
                var invoices = query.ToList();
                if (!invoices.Any()) return new DailySalesReportDto { Date = date };
                var itemCount = db.SaleItems.Where(si => si.SaleInvoice.Date >= from && si.SaleInvoice.Date < to).Sum(si => (int?)si.Quantity) ?? 0;
                var costOfGoods = db.SaleItems.Where(si => si.SaleInvoice.Date >= from && si.SaleInvoice.Date < to).Sum(si => (decimal?)(si.Quantity * si.Product.PurchasePrice)) ?? 0;
                return new DailySalesReportDto { Date = date, InvoiceCount = invoices.Count, ItemCount = itemCount, GrossSales = invoices.Sum(i => i.TotalAmount), Discounts = invoices.Sum(i => i.Discount), NetSales = invoices.Sum(i => i.NetAmount), CashSales = invoices.Where(i => i.CustomerId == null).Sum(i => i.NetAmount), CreditSales = invoices.Where(i => i.CustomerId != null).Sum(i => i.NetAmount), Profit = invoices.Sum(i => i.NetAmount) - costOfGoods };
            }
        }

        public List<TopProductDto> GetTopProducts(int limit = 10, DateTime? dateFrom = null, DateTime? dateTo = null, int? branchId = null, string userRole = null)
        {
            var from = dateFrom ?? DateTime.Today.AddDays(-30); var to = (dateTo ?? DateTime.Today).AddDays(1);
            using (var db = _dbFactory())
            {
                var query = db.SaleItems.Where(si => si.SaleInvoice.Date >= from && si.SaleInvoice.Date < to);
                if (userRole != "Admin" && branchId.HasValue) query = query.Where(si => si.SaleInvoice.BranchId == branchId.Value);
                return query.GroupBy(si => new { si.ProductId, si.Product.Name, si.Product.Barcode, CategoryName = si.Product.Category.Name })
                    .Select(g => new TopProductDto { ProductId = g.Key.ProductId, ProductName = g.Key.Name, Barcode = g.Key.Barcode, CategoryName = g.Key.CategoryName, TotalQuantity = g.Sum(x => x.Quantity), TotalRevenue = g.Sum(x => x.TotalPrice), TotalProfit = g.Sum(x => x.TotalPrice) - g.Sum(x => x.Quantity * x.Product.PurchasePrice) })
                    .OrderByDescending(x => x.TotalQuantity).Take(limit).ToList();
            }
        }

        public List<InventoryStatusDto> GetInventoryReport(int? warehouseId = null, int? branchId = null, string userRole = null)
        {
            using (var db = _dbFactory())
            {
                var products = db.Products.Where(p => p.IsActive).ToList();
                var result = new List<InventoryStatusDto>();
                foreach (var p in products)
                {
                    var stocks = db.ProductStocks.Where(ps => ps.ProductId == p.Id).Where(ps => warehouseId == null || ps.WarehouseId == warehouseId).ToList();
                    if (userRole != "Admin" && branchId.HasValue && p.BranchId != null && p.BranchId != branchId.Value) continue;
                    if (stocks.Any())
                    {
                        foreach (var ps in stocks)
                            result.Add(new InventoryStatusDto { ProductId = p.Id, ProductName = p.Name, Barcode = p.Barcode, CategoryName = p.Category?.Name ?? "", WarehouseName = ps.Warehouse?.Name ?? "-", Quantity = ps.Quantity, AverageCost = ps.AverageCost, TotalValue = ps.Quantity * ps.AverageCost, SellingPrice = p.SellingPrice, ReorderLevel = p.ReorderLevel, NeedsReorder = ps.Quantity <= p.ReorderLevel, PotentialRevenue = ps.Quantity * p.SellingPrice });
                    }
                    else if (warehouseId == null)
                        result.Add(new InventoryStatusDto { ProductId = p.Id, ProductName = p.Name, Barcode = p.Barcode, CategoryName = p.Category?.Name ?? "", WarehouseName = "-", Quantity = 0, AverageCost = p.PurchasePrice, TotalValue = 0m, SellingPrice = p.SellingPrice, ReorderLevel = p.ReorderLevel, NeedsReorder = true, PotentialRevenue = 0m });
                }
                return result.OrderBy(x => x.CategoryName).ThenBy(x => x.ProductName).ToList();
            }
        }

        public ProfitReportDto GetProfitReport(DateTime dateFrom, DateTime dateTo, int? branchId = null, string userRole = null)
        {
            var from = dateFrom.Date; var to = dateTo.Date.AddDays(1);
            using (var db = _dbFactory())
            {
                var query = db.SaleInvoices.Where(i => i.Date >= from && i.Date < to);
                if (userRole != "Admin" && branchId.HasValue) query = query.Where(i => i.BranchId == branchId.Value);
                var invoices = query.ToList();
                var itemCount = invoices.Any() ? db.SaleItems.Count(si => si.SaleInvoice.Date >= from && si.SaleInvoice.Date < to) : 0;
                var costOfGoods = invoices.Any() ? db.SaleItems.Where(si => si.SaleInvoice.Date >= from && si.SaleInvoice.Date < to).Sum(si => (decimal?)(si.Quantity * si.Product.PurchasePrice)) ?? 0 : 0;
                var salesReturns = db.StockMovements.Where(sm => sm.MovementType == "مرتجع مبيعات" && sm.Date >= from && sm.Date < to).Sum(sm => (decimal?)(sm.QuantityIn * sm.UnitPrice)) ?? 0;
                var expenses = db.Expenses.Where(e => e.ExpenseDate >= from && e.ExpenseDate < to).Sum(e => (decimal?)e.Amount) ?? 0;
                var grossSales = invoices.Sum(i => i.TotalAmount); var netSales = invoices.Sum(i => i.NetAmount); var grossProfit = netSales - costOfGoods; var netProfit = grossProfit - expenses;
                return new ProfitReportDto { DateFrom = dateFrom, DateTo = dateTo, InvoiceCount = invoices.Count, ItemCount = itemCount, GrossSales = grossSales, SalesReturns = salesReturns, NetSales = netSales - salesReturns, CashSales = invoices.Where(i => i.CustomerId == null).Sum(i => i.NetAmount), CreditSales = invoices.Where(i => i.CustomerId != null).Sum(i => i.NetAmount), CostOfGoodsSold = costOfGoods, Expenses = expenses, TotalCosts = costOfGoods + expenses, GrossProfit = grossProfit, NetProfit = netProfit, ProfitMargin = grossSales > 0 ? (netProfit / grossSales) * 100 : 0 };
            }
        }

        public List<CategorySalesDto> GetCategorySales(DateTime? dateFrom = null, DateTime? dateTo = null, int? branchId = null, string userRole = null)
        {
            var from = dateFrom ?? DateTime.Today.AddDays(-30); var to = (dateTo ?? DateTime.Today).AddDays(1);
            using (var db = _dbFactory())
            {
                var query = db.SaleItems.Where(si => si.SaleInvoice.Date >= from && si.SaleInvoice.Date < to);
                if (userRole != "Admin" && branchId.HasValue) query = query.Where(si => si.SaleInvoice.BranchId == branchId.Value);
                return query.GroupBy(si => si.Product.Category.Name).Select(g => new CategorySalesDto { CategoryName = g.Key, ItemCount = g.Count(), TotalQuantity = g.Sum(x => x.Quantity), TotalRevenue = g.Sum(x => x.TotalPrice), TotalProfit = g.Sum(x => x.TotalPrice) - g.Sum(x => x.Quantity * x.Product.PurchasePrice) }).OrderByDescending(x => x.TotalRevenue).ToList();
            }
        }

        public List<LowStockAlertDto> GetLowStockAlerts(int? warehouseId = null, int? branchId = null, string userRole = null)
        {
            using (var db = _dbFactory())
            {
                var query = db.ProductStocks.Where(ps => ps.Product.IsActive).AsQueryable();
                if (userRole != "Admin" && branchId.HasValue) query = query.Where(ps => ps.Product.BranchId == null || ps.Product.BranchId == branchId.Value);
                var data = query.Where(ps => warehouseId == null || ps.WarehouseId == warehouseId).Where(ps => ps.Quantity <= ps.Product.ReorderLevel)
                    .Select(ps => new { ps.ProductId, ProductName = ps.Product.Name, Barcode = ps.Product.Barcode, CurrentStock = ps.Quantity, ReorderLevelVal = ps.Product.ReorderLevel, ShortageQuantity = (int)(ps.Product.ReorderLevel - ps.Quantity) }).ToList();
                return data.Select(x => new LowStockAlertDto { ProductId = x.ProductId, ProductName = x.ProductName, Barcode = x.Barcode, CurrentStock = x.CurrentStock, ReorderLevel = x.ReorderLevelVal, ShortageQuantity = x.ShortageQuantity }).OrderByDescending(x => x.ShortageQuantity).ToList();
            }
        }

        public List<SupplierPurchaseSummaryDto> GetSupplierSummary(DateTime? dateFrom = null, DateTime? dateTo = null, int? branchId = null, string userRole = null)
        {
            var from = dateFrom ?? DateTime.Today.AddDays(-30); var to = (dateTo ?? DateTime.Today).AddDays(1);
            using (var db = _dbFactory())
            {
                var query = db.PurchaseInvoices.Where(pi => pi.Date >= from && pi.Date < to);
                if (userRole != "Admin" && branchId.HasValue) query = query.Where(pi => pi.BranchId == branchId.Value);
                return query.GroupBy(pi => new { pi.SupplierId, pi.Supplier.Name }).Select(g => new SupplierPurchaseSummaryDto { SupplierId = g.Key.SupplierId ?? 0, SupplierName = g.Key.Name ?? "-", InvoiceCount = g.Count(), TotalPurchases = g.Sum(x => x.TotalAmount), TotalPaid = g.Sum(x => x.PaidAmount), TotalOutstanding = g.Sum(x => x.TotalAmount - x.PaidAmount) }).OrderByDescending(x => x.TotalPurchases).ToList();
            }
        }

        // ── Phase 5: HQ Dashboard Methods (Optimized) ──

        public HqDashboardDto GetHqDashboard(int userId, DateTime? date = null)
        {
            _authzService.DemandPermission(userId, "reports.view");
            var reportDate = date ?? DateTime.Today; var from = reportDate.Date; var to = from.AddDays(1);
            using (var db = _dbFactory())
            {
                var branchData = db.SaleInvoices.Where(i => i.Date >= from && i.Date < to)
                    .GroupBy(i => new { i.BranchId, BranchName = i.Branch.Name })
                    .Select(g => new {
                        g.Key.BranchId,
                        g.Key.BranchName,
                        InvoiceCount = g.Count(),
                        TotalSales = g.Sum(i => i.NetAmount),
                        TotalDiscounts = g.Sum(i => i.Discount),
                        CashSales = g.Sum(i => i.CustomerId == null ? i.NetAmount : 0),
                        CreditSales = g.Sum(i => i.CustomerId != null ? i.NetAmount : 0),
                        CustomerCount = g.Where(i => i.CustomerId != null).Select(i => i.CustomerId.Value).Distinct().Count(),
                        TotalProfit = g.Sum(i => i.NetAmount) - db.SaleItems.Where(si => si.SaleInvoice.BranchId == g.Key.BranchId && si.SaleInvoice.Date >= from && si.SaleInvoice.Date < to).Sum(si => (decimal?)(si.Quantity * si.Product.PurchasePrice)) ?? 0
                    }).ToList();
                var allBranches = db.Branches.Where(b => b.IsActive).ToList();
                var branchBreakdown = allBranches.Select(b => { var d = branchData.FirstOrDefault(bd => bd.BranchId == b.Id); return d != null ? new BranchSalesDto { BranchId = d.BranchId ?? b.Id, BranchName = d.BranchName ?? b.Name, InvoiceCount = d.InvoiceCount, TotalSales = d.TotalSales, TotalProfit = d.TotalProfit, TotalDiscounts = d.TotalDiscounts, CashSales = d.CashSales, CreditSales = d.CreditSales, CustomerCount = d.CustomerCount } : new BranchSalesDto { BranchId = b.Id, BranchName = b.Name }; }).ToList();
                var topBranches = branchBreakdown.Where(b => b.InvoiceCount > 0).OrderByDescending(b => b.TotalSales).Take(5).Select(b => new TopBranchDto { BranchId = b.BranchId, BranchName = b.BranchName, TotalRevenue = b.TotalSales, TotalProfit = b.TotalProfit, ProfitMargin = b.TotalSales > 0 ? (b.TotalProfit / b.TotalSales) * 100 : 0, InvoiceCount = b.InvoiceCount, AverageInvoiceValue = b.InvoiceCount > 0 ? b.TotalSales / b.InvoiceCount : 0 }).ToList();
                var totalExpenses = db.Expenses.Where(e => e.ExpenseDate >= from && e.ExpenseDate < to).Sum(e => (decimal?)e.Amount) ?? 0;
                return new HqDashboardDto { ReportDate = reportDate, TotalBranches = allBranches.Count, ActiveBranches = allBranches.Count(b => b.IsActive), TotalSales = branchBreakdown.Sum(b => b.TotalSales), TotalProfit = branchBreakdown.Sum(b => b.TotalProfit) - totalExpenses, TotalCosts = branchBreakdown.Sum(b => b.TotalSales - b.TotalProfit) + totalExpenses, TotalInvoices = branchBreakdown.Sum(b => b.InvoiceCount), TotalCustomers = branchBreakdown.Sum(b => b.CustomerCount), BranchBreakdown = branchBreakdown, TopBranches = topBranches };
            }
        }

        public List<BranchComparisonDto> GetBranchComparison(int userId, DateTime currentFrom, DateTime currentTo)
        {
            _authzService.DemandPermission(userId, "reports.view");
            var days = (currentTo - currentFrom).Days; var prevFrom = currentFrom.AddDays(-days); var prevTo = currentFrom;
            using (var db = _dbFactory())
            {
                var curr = db.SaleInvoices.Where(i => i.Date >= currentFrom && i.Date < currentTo).GroupBy(i => i.BranchId).Select(g => new { BranchId = g.Key, Sales = g.Sum(i => i.NetAmount), Profit = g.Sum(i => i.NetAmount) - db.SaleItems.Where(si => si.SaleInvoice.BranchId == g.Key && si.SaleInvoice.Date >= currentFrom && si.SaleInvoice.Date < currentTo).Sum(si => (decimal?)(si.Quantity * si.Product.PurchasePrice)) ?? 0 }).ToList();
                var prev = db.SaleInvoices.Where(i => i.Date >= prevFrom && i.Date < prevTo).GroupBy(i => i.BranchId).Select(g => new { BranchId = g.Key, Sales = g.Sum(i => i.NetAmount), Profit = g.Sum(i => i.NetAmount) - db.SaleItems.Where(si => si.SaleInvoice.BranchId == g.Key && si.SaleInvoice.Date >= prevFrom && si.SaleInvoice.Date < prevTo).Sum(si => (decimal?)(si.Quantity * si.Product.PurchasePrice)) ?? 0 }).ToList();
                return db.Branches.Where(b => b.IsActive).ToList().Select(b => { var c = curr.FirstOrDefault(x => x.BranchId == b.Id); var p = prev.FirstOrDefault(x => x.BranchId == b.Id); var cs = c?.Sales ?? 0; var ps2 = p?.Sales ?? 0; var cp = c?.Profit ?? 0; var pp = p?.Profit ?? 0; return new BranchComparisonDto { BranchId = b.Id, BranchName = b.Name, CurrentSales = cs, PreviousSales = ps2, GrowthPercent = ps2 > 0 ? Math.Round(((cs - ps2) / ps2) * 100, 2) : 0, CurrentProfit = cp, PreviousProfit = pp, ProfitGrowthPercent = pp > 0 ? Math.Round(((cp - pp) / pp) * 100, 2) : 0 }; }).OrderByDescending(x => x.CurrentSales).ToList();
            }
        }

        public ProfitReportDto GetGlobalProfitReport(int userId, DateTime dateFrom, DateTime dateTo)
        {
            _authzService.DemandPermission(userId, "reports.view");
            var from = dateFrom.Date; var to = dateTo.Date.AddDays(1);
            using (var db = _dbFactory())
            {
                var metrics = db.SaleInvoices.Where(i => i.Date >= from && i.Date < to).GroupBy(i => 1).Select(g => new { InvoiceCount = g.Count(), GrossSales = g.Sum(i => i.TotalAmount), NetSales = g.Sum(i => i.NetAmount), Discounts = g.Sum(i => i.Discount), CashSales = g.Sum(i => i.CustomerId == null ? i.NetAmount : 0), CreditSales = g.Sum(i => i.CustomerId != null ? i.NetAmount : 0) }).FirstOrDefault();
                if (metrics == null) return new ProfitReportDto { DateFrom = dateFrom, DateTo = dateTo };
                var costOfGoods = db.SaleItems.Where(si => si.SaleInvoice.Date >= from && si.SaleInvoice.Date < to).Sum(si => (decimal?)(si.Quantity * si.Product.PurchasePrice)) ?? 0;
                var salesReturns = db.StockMovements.Where(sm => sm.MovementType == "مرتجع مبيعات" && sm.Date >= from && sm.Date < to).Sum(sm => (decimal?)(sm.QuantityIn * sm.UnitPrice)) ?? 0;
                var expenses = db.Expenses.Where(e => e.ExpenseDate >= from && e.ExpenseDate < to).Sum(e => (decimal?)e.Amount) ?? 0;
                var itemCount = db.SaleItems.Count(si => si.SaleInvoice.Date >= from && si.SaleInvoice.Date < to);
                var netAfterReturns = metrics.NetSales - salesReturns; var grossProfit = netAfterReturns - costOfGoods; var netProfit = grossProfit - expenses;
                return new ProfitReportDto { DateFrom = dateFrom, DateTo = dateTo, InvoiceCount = metrics.InvoiceCount, ItemCount = itemCount, GrossSales = metrics.GrossSales, SalesReturns = salesReturns, NetSales = netAfterReturns, CashSales = metrics.CashSales, CreditSales = metrics.CreditSales, CostOfGoodsSold = costOfGoods, Expenses = expenses, TotalCosts = costOfGoods + expenses, GrossProfit = grossProfit, NetProfit = netProfit, ProfitMargin = metrics.GrossSales > 0 ? Math.Round((netProfit / metrics.GrossSales) * 100, 2) : 0 };
            }
        }
    }
}