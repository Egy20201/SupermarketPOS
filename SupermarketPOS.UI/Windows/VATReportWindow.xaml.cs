using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI
{
    public partial class VATReportWindow : BaseWindow
    {
        private ObservableCollection<VATReportLineDto> reportItems;
        private readonly AccountingService _accountingService;

        public VATReportWindow()
        {
            InitializeComponent();
            _accountingService = DependencyInjection.GetRequiredService<AccountingService>();
            reportItems = new ObservableCollection<VATReportLineDto>();
            ReportGrid.ItemsSource = reportItems;
            RegisterReportShortcuts(() => LoadReport(), () => ExportButton_Click(null, null), null);
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Today;
            LoadReport();
        }

        private void LoadReport()
        {
            var fromDate = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
            var toDate = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);

            var result = _accountingService.GetVATReport(fromDate, toDate);
            reportItems.Clear();
            foreach (var item in result.Items) reportItems.Add(item);

            TotalSalesText.Text = $"💰 إجمالي المبيعات: {result.TotalSales:N2} ج.م";
            TotalVATText.Text = $"📊 إجمالي الضريبة (14%): {result.TotalVAT:N2} ج.م";
            NetSalesText.Text = $"✅ صافي المبيعات: {result.NetSales:N2} ج.م";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadReport();

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (reportItems.Count == 0)
            {
                MessageBox.Show("لا توجد بيانات للتصدير", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ExportHelper.ExportToExcel(ReportGrid, "تقرير VAT",
                $"تقرير ضريبة القيمة المضافة - {FromDatePicker.SelectedDate:dd/MM/yyyy} إلى {ToDatePicker.SelectedDate:dd/MM/yyyy}");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
