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
    public partial class EmployeesWindow : Window
    {
        private readonly EmployeeService _employeeService;
        private ObservableCollection<Employee> _employees;

        public EmployeesWindow()
        {
            InitializeComponent();
            try
            {
                _employeeService = DependencyInjection.GetRequiredService<EmployeeService>();
                _employees = new ObservableCollection<Employee>();
                EmployeesGrid.ItemsSource = _employees;
                LoadEmployees();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "EmployeesWindow initialization failed");
            }
        }

        private void LoadEmployees()
        {
            var list = _employeeService.GetActive(App.CurrentUser?.BranchId);
            _employees.Clear();
            foreach (var e in list) _employees.Add(e);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var window = new EmployeeEditWindow(_employeeService, null);
            if (window.ShowDialog() == true) LoadEmployees();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeesGrid.SelectedItem is Employee emp)
            {
                var window = new EmployeeEditWindow(_employeeService, emp);
                if (window.ShowDialog() == true) LoadEmployees();
            }
        }

        private void Deactivate_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeesGrid.SelectedItem is Employee emp)
            {
                emp.IsActive = false;
                _employeeService.Update(emp, App.CurrentUser?.Id ?? 1);
                LoadEmployees();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}