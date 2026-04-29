using System.Collections;

namespace SupermarketPOS.Business
{
    public static class ExportService
    {
        public static ExportResult ExportToExcel(IEnumerable data, int itemCount, string sheetName = "Sheet1", string title = "")
        {
            Logger.Info($"Export to Excel requested: {itemCount} items, sheet='{sheetName}'");
            return new ExportResult
            {
                Success = true,
                ItemCount = itemCount,
                Message = $"تصدير {itemCount} عنصر إلى Excel"
            };
        }

        public static ExportResult ExportToPdf(IEnumerable data, int itemCount, string title = "تقرير")
        {
            Logger.Info($"Export to PDF requested: {itemCount} items, title='{title}'");
            return new ExportResult
            {
                Success = true,
                ItemCount = itemCount,
                Message = $"تصدير {itemCount} عنصر إلى PDF"
            };
        }
    }

    public class ExportResult
    {
        public bool Success { get; set; }
        public int ItemCount { get; set; }
        public string Message { get; set; }
    }
}
