using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SupermarketPOS.UI
{
    public partial class ChartOfAccountsWindow : BaseWindow
    {
        private ObservableCollection<Account> allAccounts;
        private Account selectedAccount;
        private readonly AccountingService _accountingService;

        public ChartOfAccountsWindow()
        {
            InitializeComponent();
            _accountingService = DependencyInjection.GetRequiredService<AccountingService>();
            RegisterFormShortcuts(() => SaveButton_Click(null, null), () => Close());
            LoadAccounts();
            ClearForm();
        }

        private void LoadAccounts()
        {
            allAccounts = new ObservableCollection<Account>(_accountingService.GetAccounts());
            BuildTreeView();
            LoadParentCombo();
        }

        private void BuildTreeView()
        {
            AccountsTreeView.Items.Clear();
            foreach (var a in allAccounts.Where(a => a.ParentId == null).OrderBy(a => a.Code))
                AccountsTreeView.Items.Add(CreateTreeItem(a));
        }

        private TreeViewItem CreateTreeItem(Account a)
        {
            var item = new TreeViewItem
            {
                Header = $"{a.Code} - {a.Name}",
                Tag = a,
                FontWeight = a.IsParent ? FontWeights.Bold : FontWeights.Normal,
                Foreground = a.IsActive ? Brushes.White : Brushes.Gray
            };
            foreach (var c in allAccounts.Where(x => x.ParentId == a.Id).OrderBy(x => x.Code))
                item.Items.Add(CreateTreeItem(c));
            return item;
        }

        private void LoadParentCombo()
        {
            var parents = allAccounts.Where(a => a.IsParent || a.ParentId == null).OrderBy(a => a.Code).ToList();
            parents.Insert(0, new Account { Id = 0, Name = "بدون أب" });
            ParentCombo.ItemsSource = parents;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (AccountsTreeView.SelectedItem is TreeViewItem ti && ti.Tag is Account a)
            {
                selectedAccount = a;
                LoadAccountToForm();
            }
        }

        private void LoadAccountToForm()
        {
            if (selectedAccount == null) return;
            CodeBox.Text = selectedAccount.Code;
            NameBox.Text = selectedAccount.Name;
            TypeCombo.SelectedItem = TypeCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == selectedAccount.AccountType);
            ParentCombo.SelectedValue = selectedAccount.ParentId ?? 0;
            OpeningBalanceBox.Text = Math.Abs(selectedAccount.OpeningBalance).ToString();
            BalanceTypeCombo.SelectedIndex = selectedAccount.BalanceType == "مدين" ? 0 : 1;
            IsActiveCheck.IsChecked = selectedAccount.IsActive;
        }

        private void ClearForm()
        {
            selectedAccount = null;
            CodeBox.Text = NameBox.Text = "";
            TypeCombo.SelectedIndex = -1;
            ParentCombo.SelectedIndex = 0;
            OpeningBalanceBox.Text = "0";
            BalanceTypeCombo.SelectedIndex = 0;
            IsActiveCheck.IsChecked = true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CodeBox.Text) || string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("أدخل الكود والاسم");
                return;
            }

            decimal.TryParse(OpeningBalanceBox.Text, out var bal);
            if (BalanceTypeCombo.SelectedIndex == 1) bal = -bal;

            var parentId = ParentCombo.SelectedValue is int value && value > 0 ? (int?)value : null;
            _accountingService.SaveAccount(new AccountSaveRequest
            {
                Id = selectedAccount?.Id,
                Code = CodeBox.Text,
                Name = NameBox.Text,
                AccountType = (TypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString(),
                ParentId = parentId,
                OpeningBalance = bal,
                BalanceType = BalanceTypeCombo.SelectedIndex == 0 ? "مدين" : "دائن",
                IsActive = IsActiveCheck.IsChecked ?? true
            });

            DashboardEvents.RequestRefresh();
            LoadAccounts();
            ClearForm();
            MarkAsClean();
            MessageBox.Show("تم الحفظ");
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedAccount == null) return;
            if (!ConfirmDelete(selectedAccount.Name)) return;

            _accountingService.DeleteAccount(selectedAccount.Id);

            DashboardEvents.RequestRefresh();
            LoadAccounts();
            ClearForm();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e) => ClearForm();
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadAccounts();
    }
}
