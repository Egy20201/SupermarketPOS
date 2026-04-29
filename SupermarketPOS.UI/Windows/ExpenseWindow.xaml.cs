using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI
{
    public partial class ExpenseWindow : BaseWindow
    {
        private ObservableCollection<Expense> expenses;
        private readonly AccountingService _accountingService;

        public ExpenseWindow()
        {
            InitializeComponent();
            _accountingService = DependencyInjection.GetRequiredService<AccountingService>();
            expenses = new ObservableCollection<Expense>();
            ExpensesGrid.ItemsSource = expenses;
            RegisterListShortcuts(() => ClearButton_Click(null, null), null, null, null, () => LoadExpenses(), null);

            TypeCombo.ItemsSource = new[] { "إيجار", "رواتب", "كهرباء", "صيانة", "أخرى" };
            TypeFilterCombo.ItemsSource = new[] { "الكل", "إيجار", "رواتب", "كهرباء", "صيانة", "أخرى" };
            TypeFilterCombo.SelectedIndex = 0;
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Today;

            LoadExpenses();
        }

        private void LoadExpenses()
        {
            var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
            var to = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);
            var typeFilter = TypeFilterCombo.SelectedItem?.ToString();

            var list = _accountingService.GetExpenses(from, to, typeFilter);
            expenses.Clear();
            foreach (var e in list) expenses.Add(e);
            TotalExpensesText.Text = $"💰 إجمالي المصروفات: {expenses.Sum(e => e.Amount):N2} ج.م";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DescriptionBox.Text) || !decimal.TryParse(AmountBox.Text, out var amt) || amt <= 0)
            { MessageBox.Show("أدخل البيانات صحيحة"); return; }

            _accountingService.AddExpense(ExpenseDatePicker.SelectedDate ?? DateTime.Today, DescriptionBox.Text, amt, TypeCombo.SelectedItem?.ToString() ?? "أخرى", App.CurrentUser?.Id ?? 1);

            DashboardEvents.RequestRefresh();
            LoadExpenses();
            ClearButton_Click(null, null);
            MarkAsClean();
            MessageBox.Show("تم الحفظ");
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Expense exp)
            {
                if (!ConfirmDelete(exp.Description)) return;
                _accountingService.DeleteExpense(exp.Id);
                DashboardEvents.RequestRefresh();
                LoadExpenses();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            DescriptionBox.Text = "";
            AmountBox.Text = "";
            TypeCombo.SelectedIndex = -1;
            ExpenseDatePicker.SelectedDate = DateTime.Today;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadExpenses();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
