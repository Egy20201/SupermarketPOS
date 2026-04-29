using SupermarketPOS.Business;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Windows
{
    public partial class GeneralLedgerWindow : Window
    {
        private readonly AccountingService _accountingService;
        private ObservableCollection<LedgerLineDto> _entries;

        public GeneralLedgerWindow()
        {
            InitializeComponent();
            _accountingService = DependencyInjection.GetRequiredService<AccountingService>();
            _entries = new ObservableCollection<LedgerLineDto>();
            LedgerGrid.ItemsSource = _entries;
            LoadAccounts();
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Today;
        }

        private void LoadAccounts() { AccountCombo.ItemsSource = _accountingService.GetPostingAccounts(); AccountCombo.SelectedIndex = 0; }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (AccountCombo.SelectedValue == null) return;
            var accountId = (int)AccountCombo.SelectedValue;
            var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
            var to = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);
            var lines = _accountingService.GetLedgerEntries(accountId, from, to);
            _entries.Clear();
            foreach (var line in lines) _entries.Add(line);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
