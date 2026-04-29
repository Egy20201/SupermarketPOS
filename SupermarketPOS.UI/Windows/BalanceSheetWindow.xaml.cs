using SupermarketPOS.Core.Entities;
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
    public partial class BalanceSheetWindow : BaseWindow
    {
        private ObservableCollection<BalanceSheetLineDto> assetsItems, liabilitiesItems, equityItems;
        private readonly AccountingService _accountingService;

        public BalanceSheetWindow()
        {
            InitializeComponent();
            _accountingService = DependencyInjection.GetRequiredService<AccountingService>();
            assetsItems = new ObservableCollection<BalanceSheetLineDto>();
            liabilitiesItems = new ObservableCollection<BalanceSheetLineDto>();
            equityItems = new ObservableCollection<BalanceSheetLineDto>();
            AssetsGrid.ItemsSource = assetsItems;
            LiabilitiesGrid.ItemsSource = liabilitiesItems;
            EquityGrid.ItemsSource = equityItems;
            RegisterReportShortcuts(() => LoadData(), () => ExportButton_Click(null, null), null);
            LoadData();
        }

        private void LoadData()
        {
            var asOf = AsOfDatePicker.SelectedDate ?? DateTime.Today;
            var result = _accountingService.GetBalanceSheet(asOf);

            assetsItems.Clear();
            liabilitiesItems.Clear();
            equityItems.Clear();
            foreach (var item in result.Assets) assetsItems.Add(item);
            foreach (var item in result.Liabilities) liabilitiesItems.Add(item);
            foreach (var item in result.Equity) equityItems.Add(item);

            TotalAssetsText.Text = $"💰 إجمالي الأصول: {result.TotalAssets:N2} ج.م";
            TotalLiabilitiesText.Text = $"📋 إجمالي الالتزامات: {result.TotalLiabilities:N2} ج.م";
            TotalEquityText.Text = $"🏛️ إجمالي حقوق الملكية: {result.TotalEquity:N2} ج.م";
            BalanceCheckText.Text = result.Difference == 0 ? "✅ الميزانية متوازنة" : $"❌ الفرق: {result.Difference:N2} ج.م";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadData();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => MessageBox.Show("تصدير");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
