using System.Windows;
using System.Windows.Controls;
using SupermarketPOS.Business;

namespace SupermarketPOS.UI.Services
{
    internal static class ExportHelper
    {
        public static void ExportToExcel(DataGrid grid, string sheetName, string title = "")
        {
            var result = ExportService.ExportToExcel(grid.Items, grid.Items.Count, sheetName, title);
            MessageBox.Show(result.Message, "تصدير", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ExportToPdf(DataGrid grid, string title = "تقرير")
        {
            var result = ExportService.ExportToPdf(grid.Items, grid.Items.Count, title);
            MessageBox.Show(result.Message, "تصدير", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
