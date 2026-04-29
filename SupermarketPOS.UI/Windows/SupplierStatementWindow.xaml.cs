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
    public partial class SupplierStatementWindow : BaseWindow
    {
        private ObservableCollection<StatementRowDto> items;
        private readonly PartyAccountService _partyAccountService;

        public SupplierStatementWindow()
        {
            InitializeComponent();
            _partyAccountService = DependencyInjection.GetRequiredService<PartyAccountService>();
            items = new ObservableCollection<StatementRowDto>();
            StatementGrid.ItemsSource = items;
            RegisterReportShortcuts(() => LoadStatement(), () => ExportButton_Click(null, null), null);
            LoadSuppliers();
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Today;
        }

        private void LoadSuppliers()
        {
            SupplierCombo.ItemsSource = _partyAccountService.GetSuppliersForStatement();
        }

        private void LoadStatement()
        {
            if (SupplierCombo.SelectedValue == null) return;
            int suppId = (int)SupplierCombo.SelectedValue;
            var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
            var to = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);

            var statement = _partyAccountService.GetSupplierStatement(suppId, from, to);
            SupplierInfoText.Text = $"🏢 {statement.PartyName} | 📞 {statement.Phone} | 💰 الرصيد: {statement.CurrentBalance:N2} ج.م";
            items.Clear();
            foreach (var row in statement.Rows) items.Add(row);
            StatementGrid.ItemsSource = items;
            OpeningBalanceText.Text = $"🔓 رصيد افتتاحي: {statement.OpeningBalance:N2} ج.م";
            TotalDebitText.Text = $"📈 إجمالي مدين: {statement.TotalDebit:N2} ج.م";
            TotalCreditText.Text = $"📉 إجمالي دائن: {statement.TotalCredit:N2} ج.م";
            ClosingBalanceText.Text = $"🔒 رصيد ختامي: {statement.ClosingBalance:N2} ج.م";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadStatement();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(StatementGrid, "كشف حساب مورد");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
