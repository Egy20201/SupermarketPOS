using SupermarketPOS.Business;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.UI.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Windows
{
    public partial class PayrollWindow : Window
    {
        private readonly PayrollService _payrollService;
        private ObservableCollection<PayrollItem> _items;
        private int? _payrollId;

        public PayrollWindow()
        {
            InitializeComponent();
            try
            {
                _payrollService = DependencyInjection.GetRequiredService<PayrollService>();
                _items = new ObservableCollection<PayrollItem>();
                PayrollGrid.ItemsSource = _items;
                YearBox.Text = DateTime.Now.Year.ToString();
                for (int m = 1; m <= 12; m++) MonthCombo.Items.Add(m.ToString("D2"));
                MonthCombo.SelectedIndex = DateTime.Now.Month - 1;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "PayrollWindow initialization failed");
            }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int.TryParse(YearBox.Text, out int year);
                int month = MonthCombo.SelectedIndex + 1;
                var payroll = _payrollService.Generate(year, month, App.CurrentUser?.BranchId, App.CurrentUser?.Id ?? 1);
                _payrollId = payroll.Id;
                _items.Clear();
                foreach (var item in payroll.Items) _items.Add(item);
                TotalEmployeesText.Text = $"عدد الموظفين: {payroll.EmployeeCount}";
                TotalNetText.Text = $"إجمالي الصافي: {payroll.TotalNetSalary:N2} ج.م";
                MessageBox.Show("تم إنشاء الرواتب", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { Logger.Error(ex, "Payroll generate failed"); MessageBox.Show(ex.Message, "خطأ"); }
        }

        private void Approve_Click(object sender, RoutedEventArgs e)
        {
            if (!_payrollId.HasValue) return;
            try
            {
                _payrollService.ApprovePayroll(_payrollId.Value, App.CurrentUser?.Id ?? 1);
                MessageBox.Show("تم الاعتماد", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { Logger.Error(ex, "Payroll approve failed"); MessageBox.Show(ex.Message, "خطأ"); }
        }

        private void Post_Click(object sender, RoutedEventArgs e)
        {
            if (!_payrollId.HasValue) return;
            try
            {
                _payrollService.PostPayroll(_payrollId.Value, App.CurrentUser?.Id ?? 1);
                MessageBox.Show("تم الترحيل", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { Logger.Error(ex, "Payroll post failed"); MessageBox.Show(ex.Message, "خطأ"); }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}