using SupermarketPOS.Data;
using SupermarketPOS.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;

namespace SupermarketPOS.Business
{
    public class ReportsEngine
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly AuthorizationService _authz;

        private static readonly Dictionary<string, (object Data, DateTime Timestamp)> _cache = 
            new Dictionary<string, (object, DateTime)>();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public ReportsEngine(Func<AppDbContext> dbFactory, AuthorizationService authz)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        }

        // ── Financial Reports ──

        public ProfitLossReport GetProfitLoss(int userId, DateTime from, DateTime to)
        {
            _authz.DemandPermission(userId, "reports.view");

            var cacheKey = $"PL_{from:yyyyMMdd}_{to:yyyyMMdd}_{userId}";
            if (TryGetCache<ProfitLossReport>(cacheKey, out var cached))
                return cached;

            using (var db = _dbFactory())
            {
                var invoices = db.SaleInvoices.Where(i => i.Date >= from && i.Date < to).ToList();
                var purchases = db.PurchaseInvoices.Where(i => i.Date >= from && i.Date < to).ToList();
                var expenses = db.Expenses.Where(e => e.ExpenseDate >= from && e.ExpenseDate < to).ToList();

                var revenue = invoices.Sum(i => i.NetAmount);
                var cogs = purchases.Sum(i => i.TotalAmount);
                var totalExpenses = expenses.Sum(e => e.Amount);
                var grossProfit = revenue - cogs;
                var netProfit = grossProfit - totalExpenses;

                var result = new ProfitLossReport
                {
                    From = from, To = to,
                    Revenue = revenue,
                    COGS = cogs,
                    GrossProfit = grossProfit,
                    Expenses = totalExpenses,
                    NetProfit = netProfit,
                    InvoiceCount = invoices.Count,
                    PurchaseCount = purchases.Count
                };

                SetCache(cacheKey, result);
                return result;
            }
        }

        public CashFlowReport GetCashFlow(int userId, DateTime from, DateTime to)
        {
            _authz.DemandPermission(userId, "reports.view");

            using (var db = _dbFactory())
            {
                var cashIn = db.CustomerPayments.Where(p => p.Date >= from && p.Date < to).Sum(p => (decimal?)p.Amount) ?? 0;
                var cashOut = db.SupplierPayments.Where(p => p.Date >= from && p.Date < to).Sum(p => (decimal?)p.Amount) ?? 0;
                var expenses = db.Expenses.Where(e => e.ExpenseDate >= from && e.ExpenseDate < to).Sum(e => (decimal?)e.Amount) ?? 0;

                return new CashFlowReport
                {
                    From = from, To = to,
                    CashIn = cashIn,
                    CashOut = cashOut + expenses,
                    NetCashFlow = cashIn - cashOut - expenses
                };
            }
        }

        // ── CRM Analytics ──

        public CrmAnalytics GetCrmAnalytics(int userId, int? branchId = null)
        {
            _authz.DemandPermission(userId, "reports.view");

            using (var db = _dbFactory())
            {
                var query = db.Leads.AsQueryable();
                if (branchId.HasValue) query = query.Where(l => l.BranchId == branchId);

                var leads = query.ToList();
                var opportunities = db.Opportunities
                    .Where(o => branchId == null || o.BranchId == branchId)
                    .ToList();

                return new CrmAnalytics
                {
                    TotalLeads = leads.Count,
                    NewLeads = leads.Count(l => l.Status == LeadStatus.New),
                    QualifiedLeads = leads.Count(l => l.Status == LeadStatus.Qualified),
                    WonLeads = leads.Count(l => l.Status == LeadStatus.Won),
                    ConversionRate = leads.Count > 0 ? (decimal)leads.Count(l => l.Status == LeadStatus.Won) / leads.Count * 100 : 0,
                    PipelineValue = opportunities.Where(o => o.Stage != OpportunityStage.ClosedLost && o.Stage != OpportunityStage.ClosedWon).Sum(o => o.Amount),
                    WonValue = opportunities.Where(o => o.Stage == OpportunityStage.ClosedWon).Sum(o => o.Amount)
                };
            }
        }

        // ── Inventory Reports ──

        public List<InventoryAlertDto> GetLowStockAlerts(int userId, int? branchId = null)
        {
            _authz.DemandPermission(userId, "reports.view");

            using (var db = _dbFactory())
            {
                return db.ProductStocks
                    .Where(ps => ps.Product.IsActive)
                    .Where(ps => ps.Quantity <= ps.Product.ReorderLevel)
                    .Select(ps => new InventoryAlertDto
                    {
                        ProductId = ps.ProductId,
                        ProductName = ps.Product.Name,
                        Barcode = ps.Product.Barcode,
                        CurrentStock = ps.Quantity,
                        ReorderLevel = ps.Product.ReorderLevel
                    })
                    .OrderBy(x => x.CurrentStock)
                    .Take(20)
                    .ToList();
            }
        }

        // ── Caching Helpers ──

        private bool TryGetCache<T>(string key, out T value)
        {
            if (_cache.TryGetValue(key, out var entry) && DateTime.UtcNow - entry.Timestamp < CacheDuration)
            {
                value = (T)entry.Data;
                return true;
            }
            value = default;
            return false;
        }

        private void SetCache(string key, object data)
        {
            _cache[key] = (data, DateTime.UtcNow);
            // Cleanup old entries
            var expired = _cache.Where(kvp => DateTime.UtcNow - kvp.Value.Timestamp > CacheDuration).Select(kvp => kvp.Key).ToList();
            foreach (var k in expired) _cache.Remove(k);
        }

        // ── Export Helpers ──

        public byte[] ExportToExcel<T>(List<T> data, string sheetName)
        {
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add(sheetName);
                sheet.Cell(1, 1).InsertTable(data);
                sheet.Columns().AdjustToContents();

                using (var stream = new System.IO.MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public byte[] ExportToPdf<T>(List<T> data, string title, string[] headers, Func<T, string[]> rowSelector)
        {
            using (var stream = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 30, 30, 40, 40);
                var writer = PdfWriter.GetInstance(document, stream);
                document.Open();

                // Title
                var titleFont = FontFactory.GetFont("Arial", 16, Font.BOLD);
                document.Add(new Paragraph(title, titleFont) { Alignment = Element.ALIGN_CENTER });
                document.Add(new Paragraph("\n"));

                // Table
                var table = new PdfPTable(headers.Length) { WidthPercentage = 100 };
                var headerFont = FontFactory.GetFont("Arial", 10, Font.BOLD);

                foreach (var header in headers)
                    table.AddCell(new PdfPCell(new Phrase(header, headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY });

                var cellFont = FontFactory.GetFont("Arial", 9, Font.NORMAL);
                foreach (var item in data)
                {
                    var row = rowSelector(item);
                    foreach (var cell in row)
                        table.AddCell(new PdfPCell(new Phrase(cell, cellFont)));
                }

                document.Add(table);
                document.Close();

                return stream.ToArray();
            }
        }

        public byte[] ExportProfitLossPdf(int userId, DateTime from, DateTime to)
        {
            var report = GetProfitLoss(userId, from, to);
            var data = new List<ProfitLossReport> { report };
            var headers = new[] { "من", "إلى", "الإيرادات", "تكلفة البضاعة", "الربح الإجمالي", "المصروفات", "صافي الربح" };
            Func<ProfitLossReport, string[]> selector = r => new[]
            {
                r.From.ToString("dd/MM/yyyy"), r.To.ToString("dd/MM/yyyy"),
                r.Revenue.ToString("N2"), r.COGS.ToString("N2"),
                r.GrossProfit.ToString("N2"), r.Expenses.ToString("N2"),
                r.NetProfit.ToString("N2")
            };
            return ExportToPdf(data, "تقرير الأرباح والخسائر", headers, selector);
        }
    }

    // ── Report DTOs ──

    public class ProfitLossReport
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public decimal Revenue { get; set; }
        public decimal COGS { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal Expenses { get; set; }
        public decimal NetProfit { get; set; }
        public int InvoiceCount { get; set; }
        public int PurchaseCount { get; set; }
    }

    public class CashFlowReport
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public decimal CashIn { get; set; }
        public decimal CashOut { get; set; }
        public decimal NetCashFlow { get; set; }
    }

    public class CrmAnalytics
    {
        public int TotalLeads { get; set; }
        public int NewLeads { get; set; }
        public int QualifiedLeads { get; set; }
        public int WonLeads { get; set; }
        public decimal ConversionRate { get; set; }
        public decimal PipelineValue { get; set; }
        public decimal WonValue { get; set; }
    }

    public class InventoryAlertDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public int CurrentStock { get; set; }
        public int ReorderLevel { get; set; }
    }
}
