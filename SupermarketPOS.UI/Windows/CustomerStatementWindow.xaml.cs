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
    public partial class CustomerStatementWindow : BaseWindow
    {
        private ObservableCollection<StatementRowDto> items;
        private readonly PartyAccountService _partyAccountService;

        public CustomerStatementWindow()
        {
            InitializeComponent();
            _partyAccountService = DependencyInjection.GetRequiredService<PartyAccountService>();
            items = new ObservableCollection<StatementRowDto>();
            StatementGrid.ItemsSource = items;
            RegisterReportShortcuts(() => LoadStatement(), () => ExportButton_Click(null, null), null);
            LoadCustomers();
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Today;
        }

        private void LoadCustomers()
        {
            CustomerCombo.ItemsSource = _partyAccountService.GetCustomersForStatement();
        }

        private void LoadStatement()
        {
            if (CustomerCombo.SelectedValue == null) return;
            int custId = (int)CustomerCombo.SelectedValue;
            var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
            var to = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);

            var statement = _partyAccountService.GetCustomerStatement(custId, from, to);
            CustomerInfoText.Text = $"👤 {statement.PartyName} | 📞 {statement.Phone} | 💰 الرصيد: {statement.CurrentBalance:N2} ج.م";
            items.Clear();
            foreach (var row in statement.Rows) items.Add(row);
            StatementGrid.ItemsSource = items;
            OpeningBalanceText.Text = $"🔓 رصيد افتتاحي: {statement.OpeningBalance:N2} ج.م";
            TotalDebitText.Text = $"📈 إجمالي مدين: {statement.TotalDebit:N2} ج.م";
            TotalCreditText.Text = $"📉 إجمالي دائن: {statement.TotalCredit:N2} ج.م";
            ClosingBalanceText.Text = $"🔒 رصيد ختامي: {statement.ClosingBalance:N2} ج.م";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadStatement();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(StatementGrid, "كشف حساب");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
