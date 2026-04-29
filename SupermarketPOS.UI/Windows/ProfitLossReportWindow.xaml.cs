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
    public partial class ProfitLossReportWindow : BaseWindow
    {
        private ObservableCollection<ProfitLossDetailDto> items;
        private readonly AccountingService _accountingService;

        public ProfitLossReportWindow()
        {
            InitializeComponent();
            _accountingService = DependencyInjection.GetRequiredService<AccountingService>();
            items = new ObservableCollection<ProfitLossDetailDto>();
            DetailsGrid.ItemsSource = items;
            RegisterReportShortcuts(() => LoadData(), () => ExportButton_Click(null, null), null);
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Today;
            LoadData();
        }

        private void LoadData()
        {
            var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
            var to = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);

            var result = _accountingService.GetProfitLoss(from, to);
            TotalSalesText.Text = $"إجمالي المبيعات: {result.TotalSales:N2} ج.م";
            SalesReturnsText.Text = $"مرتجعات المبيعات: {result.SalesReturns:N2} ج.م";
            NetSalesText.Text = $"صافي المبيعات: {result.NetSales:N2} ج.م";
            COGSText.Text = $"تكلفة المشتريات: {result.PurchasesCost:N2} ج.م";
            ExpensesText.Text = $"المصروفات: {result.Expenses:N2} ج.م";
            TotalCostText.Text = $"إجمالي التكاليف: {result.TotalCost:N2} ج.م";
            NetProfitText.Text = result.NetProfit >= 0 ? $"صافي الربح: {result.NetProfit:N2} ج.م" : $"صافي الخسارة: {Math.Abs(result.NetProfit):N2} ج.م";
            NetProfitPercentText.Text = $"نسبة الربح: {result.ProfitPercent:N2}%";

            items.Clear();
            foreach (var item in result.Details) items.Add(item);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadData();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(DetailsGrid, "الأرباح والخسائر");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
