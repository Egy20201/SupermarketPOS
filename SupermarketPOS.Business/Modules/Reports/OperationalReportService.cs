using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;

namespace SupermarketPOS.Business.Modules
{
    public class OperationalReportService
    {
        private readonly Func<AppDbContext> _dbFactory;

        public OperationalReportService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public List<Customer> GetCustomerFilterItems()
        {
            using (var db = _dbFactory())
            {
                var customers = db.Customers.AsNoTracking().OrderBy(c => c.Name).ToList();
                customers.Insert(0, new Customer { Id = 0, Name = "جميع العملاء" });
                return customers;
            }
        }

        public List<Supplier> GetSupplierFilterItems()
        {
            using (var db = _dbFactory())
            {
                var suppliers = db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToList();
                suppliers.Insert(0, new Supplier { Id = 0, Name = "جميع الموردين" });
                return suppliers;
            }
        }

        public SalesReportResult GetSalesReport(DateTime from, DateTime toExclusive, int? customerId)
        {
            using (var db = _dbFactory())
            {
                var query = db.SaleInvoices.AsNoTracking().Where(i => i.Date >= from && i.Date < toExclusive);
                if (customerId.HasValue) query = query.Where(i => i.CustomerId == customerId.Value);
                var invoices = query.OrderByDescending(i => i.Date).ToList();
                var customerNames = db.Customers.AsNoTracking().ToDictionary(c => c.Id, c => c.Name);
                var productPrices = db.Products.AsNoTracking().ToDictionary(p => p.Id, p => p.PurchasePrice);
                var result = new SalesReportResult();

                foreach (var invoice in invoices)
                {
                    var invoiceItems = db.SaleItems.AsNoTracking().Where(si => si.SaleInvoiceId == invoice.Id).ToList();
                    var profit = invoiceItems.Sum(item =>
                    {
                        decimal purchasePrice;
                        productPrices.TryGetValue(item.ProductId, out purchasePrice);
                        return item.TotalPrice - item.Quantity * purchasePrice;
                    });
                    string customerName;
                    if (!invoice.CustomerId.HasValue || !customerNames.TryGetValue(invoice.CustomerId.Value, out customerName))
                        customerName = invoice.CustomerId.HasValue ? "-" : "عميل نقدي";

                    result.Items.Add(new SalesReportItemDto
                    {
                        InvoiceNumber = invoice.InvoiceNumber,
                        Date = invoice.Date,
                        CustomerName = customerName,
                        ItemCount = invoiceItems.Count,
                        TotalAmount = invoice.TotalAmount,
                        Discount = invoice.Discount,
                        NetAmount = invoice.NetAmount,
                        Profit = profit
                    });
                    result.TotalSales += invoice.NetAmount;
                    result.TotalProfit += profit;
                    result.TotalItems += invoiceItems.Count;
                }

                result.TotalInvoices = invoices.Count;
                return result;
            }
        }

        public PurchaseReportResult GetPurchaseReport(DateTime from, DateTime toExclusive, int? supplierId)
        {
            using (var db = _dbFactory())
            {
                var query = db.PurchaseInvoices.AsNoTracking().Where(i => i.Date >= from && i.Date < toExclusive);
                if (supplierId.HasValue) query = query.Where(i => i.SupplierId == supplierId.Value);
                var invoices = query.OrderByDescending(i => i.Date).ToList();
                var supplierNames = db.Suppliers.AsNoTracking().ToDictionary(s => s.Id, s => s.Name);
                var result = new PurchaseReportResult();

                foreach (var invoice in invoices)
                {
                    string supplierName;
                    if (!invoice.SupplierId.HasValue || !supplierNames.TryGetValue(invoice.SupplierId.Value, out supplierName))
                        supplierName = "-";
                    result.Items.Add(new PurchaseReportItemDto
                    {
                        InvoiceNumber = invoice.InvoiceNumber,
                        Date = invoice.Date,
                        SupplierName = supplierName,
                        PaymentType = invoice.PaymentType,
                        TotalAmount = invoice.TotalAmount,
                        PaidAmount = invoice.PaidAmount,
                        RemainingAmount = invoice.TotalAmount - invoice.PaidAmount
                    });
                    result.TotalPurchases += invoice.TotalAmount;
                }

                result.TotalInvoices = invoices.Count;
                return result;
            }
        }

        public DashboardSummaryDto GetDashboardSummary(DateTime date)
        {
            var today = date.Date;
            var tomorrow = today.AddDays(1);
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
            using (var db = _dbFactory())
            {
                var lowStockCount = db.ProductStocks.Count(ps => ps.Product.IsActive && ps.Quantity <= ps.Product.ReorderLevel);
                var purchases = db.PurchaseInvoices.Where(p => p.Date >= firstDayOfMonth).Sum(p => (decimal?)p.TotalAmount) ?? 0;
                return new DashboardSummaryDto
                {
                    LowStockCount = lowStockCount,
                    MonthPurchases = purchases,
                    EmployeeCount = db.Employees.Count(),
                    LeadCount = db.Leads.Count()
                };
            }
        }

        public List<CategoryQuantityDto> GetTopCategoryQuantities(DateTime from, int limit)
        {
            using (var db = _dbFactory())
            {
                return db.SaleItems.AsNoTracking()
                    .Where(si => si.SaleInvoice.Date >= from)
                    .GroupBy(si => si.Product.Category.Name)
                    .Select(g => new CategoryQuantityDto { CategoryName = g.Key, TotalQuantity = g.Sum(si => si.Quantity) })
                    .OrderByDescending(x => x.TotalQuantity)
                    .Take(limit)
                    .ToList();
            }
        }

        public List<RecentInvoiceDto> GetRecentInvoices(int limit)
        {
            using (var db = _dbFactory())
            {
                var invoices = db.SaleInvoices.AsNoTracking().OrderByDescending(i => i.Date).Take(limit).ToList();
                var customerNames = db.Customers.AsNoTracking().ToDictionary(c => c.Id, c => c.Name);
                return invoices.Select(inv =>
                {
                    string customerName;
                    if (!inv.CustomerId.HasValue || !customerNames.TryGetValue(inv.CustomerId.Value, out customerName))
                        customerName = inv.CustomerId.HasValue ? "-" : "عميل نقدي";
                    return new RecentInvoiceDto
                    {
                        InvoiceNumber = inv.InvoiceNumber,
                        CustomerName = customerName,
                        Total = inv.NetAmount,
                        Status = inv.PaidAmount >= inv.NetAmount ? "✅ مدفوع" : "⌛ آجل",
                        Date = inv.Date
                    };
                }).ToList();
            }
        }

        public List<User> GetShiftReportUsers()
        {
            using (var db = _dbFactory())
            {
                var users = db.Users.AsNoTracking().Where(u => u.Role == "Cashier" || u.Role == "Admin").OrderBy(u => u.FullName).ToList();
                users.Insert(0, new User { Id = 0, FullName = "جميع الكاشيرز" });
                return users;
            }
        }

        public ShiftReportResult GetShiftReport(DateTime from, DateTime toExclusive, int? userId)
        {
            using (var db = _dbFactory())
            {
                var query = db.ShiftClosings.AsNoTracking().Where(s => s.ShiftDate >= from && s.ShiftDate < toExclusive);
                if (userId.HasValue) query = query.Where(s => s.UserId == userId.Value);
                var shifts = query.OrderByDescending(s => s.ShiftDate).ThenByDescending(s => s.EndTime).ToList();
                var userNames = db.Users.AsNoTracking().ToDictionary(u => u.Id, u => u.FullName);
                var result = new ShiftReportResult();

                foreach (var shift in shifts)
                {
                    string userName;
                    if (!userNames.TryGetValue(shift.UserId, out userName)) userName = "-";
                    result.Items.Add(new ShiftReportItemDto
                    {
                        Id = shift.Id,
                        ShiftDate = shift.ShiftDate,
                        UserName = userName,
                        StartTime = shift.StartTime,
                        EndTime = shift.EndTime,
                        TotalInvoices = shift.TotalInvoices,
                        TotalSales = shift.TotalSales,
                        ExpectedCash = shift.ExpectedCashInDrawer,
                        ActualCash = shift.ActualCashInDrawer,
                        Difference = shift.CashDifference
                    });
                    result.TotalSales += shift.TotalSales;
                    result.TotalCash += shift.ActualCashInDrawer;
                    result.TotalDifference += shift.CashDifference;
                }

                result.TotalShifts = shifts.Count;
                return result;
            }
        }

        public string GetShiftDetailsText(int shiftId)
        {
            using (var db = _dbFactory())
            {
                var shift = db.ShiftClosings.AsNoTracking().FirstOrDefault(s => s.Id == shiftId);
                if (shift == null) return string.Empty;
                var invoices = db.SaleInvoices.AsNoTracking().Where(i => i.ShiftClosingId == shiftId).OrderBy(i => i.Date).ToList();
                var user = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == shift.UserId);

                var sb = new StringBuilder();
                sb.AppendLine("═══════════════════════════════════════");
                sb.AppendLine(string.Format("       تفاصيل الشيفت - {0}", user == null ? "-" : user.FullName));
                sb.AppendLine("═══════════════════════════════════════");
                sb.AppendLine(string.Format("التاريخ: {0:yyyy/MM/dd}", shift.ShiftDate));
                sb.AppendLine(string.Format("الوقت: {0:HH:mm} - {1:HH:mm}", shift.StartTime, shift.EndTime));
                sb.AppendLine("───────────────────────────────────────");
                sb.AppendLine("رقم الفاتورة   الوقت   المبلغ   النوع");
                sb.AppendLine("───────────────────────────────────────");
                foreach (var invoice in invoices)
                {
                    var type = invoice.CustomerId == null ? "نقدي" : "آجل";
                    sb.AppendLine(string.Format("{0}   {1:HH:mm}   {2}   {3}", invoice.InvoiceNumber.PadLeft(8), invoice.Date, invoice.NetAmount.ToString("N2").PadLeft(8), type));
                }
                sb.AppendLine("───────────────────────────────────────");
                sb.AppendLine(string.Format("عدد الفواتير: {0}", invoices.Count));
                sb.AppendLine(string.Format("إجمالي المبيعات: {0:N2} ج.م", shift.TotalSales));
                sb.AppendLine(string.Format("النقدية المتوقعة: {0:N2} ج.م", shift.ExpectedCashInDrawer));
                sb.AppendLine(string.Format("النقدية الفعلية: {0:N2} ج.م", shift.ActualCashInDrawer));
                sb.AppendLine(string.Format("الفرق: {0:N2} ج.م", shift.CashDifference));
                sb.AppendLine("═══════════════════════════════════════");
                return sb.ToString();
            }
        }
    }

    public class SalesReportResult { public List<SalesReportItemDto> Items { get; set; } = new List<SalesReportItemDto>(); public int TotalInvoices { get; set; } public int TotalItems { get; set; } public decimal TotalSales { get; set; } public decimal TotalProfit { get; set; } }
    public class SalesReportItemDto { public string InvoiceNumber { get; set; } public DateTime Date { get; set; } public string CustomerName { get; set; } public int ItemCount { get; set; } public decimal TotalAmount { get; set; } public decimal Discount { get; set; } public decimal NetAmount { get; set; } public decimal Profit { get; set; } }

    public class PurchaseReportResult { public List<PurchaseReportItemDto> Items { get; set; } = new List<PurchaseReportItemDto>(); public int TotalInvoices { get; set; } public decimal TotalPurchases { get; set; } }
    public class PurchaseReportItemDto { public string InvoiceNumber { get; set; } public DateTime Date { get; set; } public string SupplierName { get; set; } public string PaymentType { get; set; } public decimal TotalAmount { get; set; } public decimal PaidAmount { get; set; } public decimal RemainingAmount { get; set; } }

    public class DashboardSummaryDto { public int LowStockCount { get; set; } public decimal MonthPurchases { get; set; } public int EmployeeCount { get; set; } public int LeadCount { get; set; } }
    public class CategoryQuantityDto { public string CategoryName { get; set; } public int TotalQuantity { get; set; } }
    public class RecentInvoiceDto { public string InvoiceNumber { get; set; } public string CustomerName { get; set; } public decimal Total { get; set; } public string Status { get; set; } public DateTime Date { get; set; } }

    public class ShiftReportResult { public List<ShiftReportItemDto> Items { get; set; } = new List<ShiftReportItemDto>(); public int TotalShifts { get; set; } public decimal TotalSales { get; set; } public decimal TotalCash { get; set; } public decimal TotalDifference { get; set; } }
    public class ShiftReportItemDto { public int Id { get; set; } public DateTime ShiftDate { get; set; } public string UserName { get; set; } public DateTime StartTime { get; set; } public DateTime EndTime { get; set; } public int TotalInvoices { get; set; } public decimal TotalSales { get; set; } public decimal ExpectedCash { get; set; } public decimal ActualCash { get; set; } public decimal Difference { get; set; } }
}
