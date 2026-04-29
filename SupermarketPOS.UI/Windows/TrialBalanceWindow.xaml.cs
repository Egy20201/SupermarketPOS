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
    public partial class TrialBalanceWindow : BaseWindow
    {
        private ObservableCollection<TrialBalanceLineDto> items;
        private readonly AccountingService _accountingService;

        public TrialBalanceWindow()
        {
            InitializeComponent();
            _accountingService = DependencyInjection.GetRequiredService<AccountingService>();
            items = new ObservableCollection<TrialBalanceLineDto>();
            TrialBalanceGrid.ItemsSource = items;
            RegisterReportShortcuts(() => LoadData(), () => ExportButton_Click(null, null), null);
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Today;
            LoadData();
        }

        private void LoadData()
        {
            var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
            var to = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);

            var result = _accountingService.GetTrialBalance(from, to);
            items.Clear();
            foreach (var item in result.Items) items.Add(item);

            TotalDebitText.Text = $"إجمالي مدين: {result.TotalDebit:N2}";
            TotalCreditText.Text = $"إجمالي دائن: {result.TotalCredit:N2}";
            DifferenceText.Text = result.Difference == 0 ? "✅ متوازن" : $"❌ الفرق: {result.Difference:N2}";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadData();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(TrialBalanceGrid, "ميزان المراجعة");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
