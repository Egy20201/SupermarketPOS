using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI
{
    public partial class JournalVoucherWindow : BaseWindow
    {
        private ObservableCollection<JournalLineItem> lineItems;
        private System.Collections.Generic.List<Account> allAccounts;
        private readonly AccountingService _accountingService;

        public JournalVoucherWindow()
        {
            InitializeComponent();
            _accountingService = DependencyInjection.GetRequiredService<AccountingService>();
            lineItems = new ObservableCollection<JournalLineItem>();
            LinesGrid.ItemsSource = lineItems;
            RegisterTransactionShortcuts(() => SaveButton_Click(null, null), () => Close(), null);
            LoadAccounts();
            GenerateNewVoucherNumber();
            lineItems.Add(new JournalLineItem());
        }

        private void LoadAccounts()
        {
            allAccounts = _accountingService.GetPostingAccounts();
        }

        private void GenerateNewVoucherNumber()
        {
            VoucherNumberBox.Text = _accountingService.GetNextJournalVoucherNumber();
        }

        private void AddLine_Click(object sender, RoutedEventArgs e) => lineItems.Add(new JournalLineItem());
        private void DeleteLine_Click(object sender, RoutedEventArgs e) { if (sender is Button btn && btn.DataContext is JournalLineItem item) lineItems.Remove(item); UpdateTotals(); }
        private void UpdateTotals()
        {
            var items = lineItems.Where(i => i.AccountId > 0).ToList();
            var totalDebit = items.Sum(i => i.Debit);
            var totalCredit = items.Sum(i => i.Credit);
            var diff = totalDebit - totalCredit;

            TotalDebitText.Text = $"إجمالي مدين: {totalDebit:N2}";
            TotalCreditText.Text = $"إجمالي دائن: {totalCredit:N2}";
            DifferenceText.Text = diff == 0 ? "✅ متوازن" : $"❌ الفرق: {diff:N2}";
            DifferenceText.Foreground = diff == 0 ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
        }

        private void ClearForm() { lineItems.Clear(); DescriptionBox.Text = ""; GenerateNewVoucherNumber(); lineItems.Add(new JournalLineItem()); UpdateTotals(); }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var lines = lineItems.Where(i => i.AccountId > 0 && (i.Debit > 0 || i.Credit > 0)).ToList();
            if (!lines.Any()) { MessageBox.Show("لا توجد سطور صالحة"); return; }
            if (lines.Sum(l => l.Debit) != lines.Sum(l => l.Credit)) { MessageBox.Show("القيد غير متوازن"); return; }

            _accountingService.SaveJournalVoucher(
                VoucherNumberBox.Text,
                DatePicker.SelectedDate ?? DateTime.Today,
                DescriptionBox.Text,
                App.CurrentUser?.Id,
                lines.Select(line => new JournalVoucherLineDto { AccountId = line.AccountId, Debit = line.Debit, Credit = line.Credit, Notes = line.Notes }));

            DashboardEvents.RequestRefresh();
            ClearForm();
            MarkAsClean();
            MessageBox.Show("تم حفظ القيد");
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e) => ClearForm();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class JournalLineItem : INotifyPropertyChanged
    {
        private int _accountId;
        private string _accountCode, _accountName, _notes;
        private decimal _debit, _credit;

        public event PropertyChangedEventHandler PropertyChanged;

        public int AccountId { get => _accountId; set { _accountId = value; OnPropertyChanged(); } }
        public string AccountCode { get => _accountCode; set { _accountCode = value; OnPropertyChanged(); } }
        public string AccountName { get => _accountName; set { _accountName = value; OnPropertyChanged(); } }
        public decimal Debit { get => _debit; set { _debit = value; OnPropertyChanged(); } }
        public decimal Credit { get => _credit; set { _credit = value; OnPropertyChanged(); } }
        public string Notes { get => _notes; set { _notes = value; OnPropertyChanged(); } }
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
