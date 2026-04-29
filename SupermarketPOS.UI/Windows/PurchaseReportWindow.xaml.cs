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
    public partial class PurchaseReportWindow : BaseWindow
    {
        private ObservableCollection<PurchaseReportItemDto> items;
        private readonly OperationalReportService _reportService;

        public PurchaseReportWindow()
        {
            InitializeComponent();
            _reportService = DependencyInjection.GetRequiredService<OperationalReportService>();
            items = new ObservableCollection<PurchaseReportItemDto>();
            ReportGrid.ItemsSource = items;
            RegisterReportShortcuts(() => LoadData(), () => ExportButton_Click(null, null), null);
            LoadFilters();
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Today;
            LoadData();
        }

        private void LoadFilters()
        {
            SupplierFilterCombo.ItemsSource = _reportService.GetSupplierFilterItems();
            SupplierFilterCombo.DisplayMemberPath = "Name";
            SupplierFilterCombo.SelectedValuePath = "Id";
            SupplierFilterCombo.SelectedIndex = 0;
        }

        private void LoadData()
        {
            var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
            var to = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);
            int? suppId = (int?)SupplierFilterCombo.SelectedValue == 0 ? null : (int?)SupplierFilterCombo.SelectedValue;

            var result = _reportService.GetPurchaseReport(from, to, suppId);
            items.Clear();
            foreach (var item in result.Items) items.Add(item);
            TotalInvoicesText.Text = $"🧾 عدد الفواتير: {result.TotalInvoices}";
            TotalPurchasesText.Text = $"💰 إجمالي المشتريات: {result.TotalPurchases:N2} ج.م";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadData();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(ReportGrid, "تقرير المشتريات");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
