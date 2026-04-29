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
    public partial class AttendanceWindow : Window
    {
        private readonly AttendanceService _attendanceService;
        private readonly EmployeeService _employeeService;
        private ObservableCollection<AttendanceRecord> _records;

        public AttendanceWindow()
        {
            InitializeComponent();
            try
            {
                _attendanceService = DependencyInjection.GetRequiredService<AttendanceService>();
                _employeeService = DependencyInjection.GetRequiredService<EmployeeService>();
                _records = new ObservableCollection<AttendanceRecord>();
                AttendanceGrid.ItemsSource = _records;
                LoadEmployees();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "AttendanceWindow initialization failed");
            }
        }

        private void LoadEmployees()
        {
            EmployeeCombo.ItemsSource = _employeeService.GetActive(App.CurrentUser?.BranchId);
        }

        private void CheckIn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (EmployeeCombo.SelectedValue == null) return;
                var empId = (int)EmployeeCombo.SelectedValue;
                _attendanceService.CheckIn(empId, App.CurrentUser?.BranchId, App.CurrentUser?.Id ?? 1);
                MessageBox.Show("تم تسجيل الحضور", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { Logger.Error(ex, "CheckIn failed"); MessageBox.Show(ex.Message, "خطأ"); }
        }

        private void CheckOut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (EmployeeCombo.SelectedValue == null) return;
                var empId = (int)EmployeeCombo.SelectedValue;
                _attendanceService.CheckOut(empId, App.CurrentUser?.Id ?? 1);
                MessageBox.Show("تم تسجيل الانصراف", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { Logger.Error(ex, "CheckOut failed"); MessageBox.Show(ex.Message, "خطأ"); }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}