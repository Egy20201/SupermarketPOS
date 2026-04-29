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
    public partial class SalesReportWindow : BaseWindow
    {
        private ObservableCollection<SalesReportItemDto> items;
        private readonly OperationalReportService _reportService;

        public SalesReportWindow()
        {
            InitializeComponent();
            _reportService = DependencyInjection.GetRequiredService<OperationalReportService>();
            items = new ObservableCollection<SalesReportItemDto>();
            ReportGrid.ItemsSource = items;
            RegisterReportShortcuts(() => LoadData(), () => ExportButton_Click(null, null), null);
            LoadFilters();
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Today;
            LoadData();
        }

        private void LoadFilters()
        {
            CustomerFilterCombo.ItemsSource = _reportService.GetCustomerFilterItems();
            CustomerFilterCombo.DisplayMemberPath = "Name";
            CustomerFilterCombo.SelectedValuePath = "Id";
            CustomerFilterCombo.SelectedIndex = 0;
        }

        private void LoadData()
        {
            var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
            var to = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);
            int? custId = (int?)CustomerFilterCombo.SelectedValue == 0 ? null : (int?)CustomerFilterCombo.SelectedValue;

            var result = _reportService.GetSalesReport(from, to, custId);
            items.Clear();
            foreach (var item in result.Items) items.Add(item);
            TotalInvoicesText.Text = $"🧾 عدد الفواتير: {result.TotalInvoices}";
            TotalSalesText.Text = $"💰 إجمالي المبيعات: {result.TotalSales:N2} ج.م";
            TotalProfitText.Text = $"📈 إجمالي الأرباح: {result.TotalProfit:N2} ج.م";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadData();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(ReportGrid, "تقرير المبيعات");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
