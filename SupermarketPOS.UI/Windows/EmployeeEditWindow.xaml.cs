using SupermarketPOS.Business;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.UI.Infrastructure;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Windows
{
    public partial class EmployeeEditWindow : Window
    {
        private readonly EmployeeService _employeeService;
        private readonly Employee _existingEmployee;
        private readonly bool _isNew;
        private int _currentUserId;

        public EmployeeEditWindow(EmployeeService employeeService, Employee employee)
        {
            InitializeComponent();
            _employeeService = employeeService ?? DependencyInjection.GetRequiredService<EmployeeService>();
            _existingEmployee = employee;
            _isNew = employee == null;

            if (App.CurrentUser == null || App.CurrentUser.Id <= 0)
                throw new InvalidOperationException("يجب تسجيل الدخول أولاً");

            _currentUserId = App.CurrentUser.Id;

            LoadDepartments();
            PopulateForm();
        }

        private void LoadDepartments()
        {
            var depts = _employeeService.GetDepartments();
            DepartmentCombo.ItemsSource = depts;
            if (depts.Any()) DepartmentCombo.SelectedIndex = 0;
        }

        private void PopulateForm()
        {
            if (!_isNew && _existingEmployee != null)
            {
                CodeBox.Text = _existingEmployee.Code ?? "";
                FullNameBox.Text = _existingEmployee.FullName ?? "";
                PhoneBox.Text = _existingEmployee.Phone ?? "";
                EmailBox.Text = _existingEmployee.Email ?? "";
                PositionBox.Text = _existingEmployee.Position ?? "";
                BasicSalaryBox.Text = _existingEmployee.BasicSalary.ToString("F2");
                AllowancesBox.Text = _existingEmployee.Allowances.ToString("F2");
                DeductionsBox.Text = _existingEmployee.Deductions.ToString("F2");

                if (_existingEmployee.DepartmentId.HasValue)
                    DepartmentCombo.SelectedValue = _existingEmployee.DepartmentId.Value;
            }
            else
            {
                CodeBox.Text = "تلقائي";
                CodeBox.IsEnabled = false;
                BasicSalaryBox.Text = "0.00";
                AllowancesBox.Text = "0.00";
                DeductionsBox.Text = "0.00";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var saveButton = sender as Button;
            if (saveButton != null) saveButton.IsEnabled = false;

            try
            {
                if (string.IsNullOrWhiteSpace(FullNameBox.Text))
                {
                    ShowError("يرجى إدخال اسم الموظف");
                    return;
                }

                if (!TryParseDecimal(BasicSalaryBox.Text, out decimal basicSalary, "الراتب الأساسي")) return;
                if (!TryParseDecimal(AllowancesBox.Text, out decimal allowances, "البدلات")) return;
                if (!TryParseDecimal(DeductionsBox.Text, out decimal deductions, "الخصومات")) return;

                if (basicSalary < 0) { ShowError("الراتب الأساسي لا يمكن أن يكون سالباً"); return; }
                if (allowances < 0) { ShowError("البدلات لا يمكن أن تكون سالبة"); return; }
                if (deductions < 0) { ShowError("الخصومات لا يمكن أن تكون سالبة"); return; }

                int? departmentId = null;
                if (DepartmentCombo.SelectedValue != null)
                {
                    if (DepartmentCombo.SelectedValue is int deptId) departmentId = deptId;
                    else if (int.TryParse(DepartmentCombo.SelectedValue.ToString(), out int parsedDeptId)) departmentId = parsedDeptId;
                }

                if (_isNew)
                {
                    var emp = new Employee
                    {
                        FullName = FullNameBox.Text.Trim(),
                        Phone = string.IsNullOrWhiteSpace(PhoneBox.Text) ? null : PhoneBox.Text.Trim(),
                        Email = string.IsNullOrWhiteSpace(EmailBox.Text) ? null : EmailBox.Text.Trim(),
                        Position = string.IsNullOrWhiteSpace(PositionBox.Text) ? null : PositionBox.Text.Trim(),
                        BasicSalary = basicSalary,
                        Allowances = allowances,
                        Deductions = deductions,
                        DepartmentId = departmentId,
                        BranchId = App.CurrentUser?.BranchId,
                        IsActive = true,
                        HireDate = DateTime.UtcNow
                    };
                    _employeeService.Create(emp, _currentUserId);
                }
                else
                {
                    _existingEmployee.FullName = FullNameBox.Text.Trim();
                    _existingEmployee.Phone = string.IsNullOrWhiteSpace(PhoneBox.Text) ? null : PhoneBox.Text.Trim();
                    _existingEmployee.Email = string.IsNullOrWhiteSpace(EmailBox.Text) ? null : EmailBox.Text.Trim();
                    _existingEmployee.Position = string.IsNullOrWhiteSpace(PositionBox.Text) ? null : PositionBox.Text.Trim();
                    _existingEmployee.BasicSalary = basicSalary;
                    _existingEmployee.Allowances = allowances;
                    _existingEmployee.Deductions = deductions;
                    _existingEmployee.DepartmentId = departmentId;
                    _employeeService.Update(_existingEmployee, _currentUserId);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex) { Logger.Error(ex, "Employee save failed"); ShowError(ex.Message); }
            finally { if (saveButton != null) saveButton.IsEnabled = true; }
        }

        private bool TryParseDecimal(string input, out decimal value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(input)) { value = 0; return true; }
            if (!decimal.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                ShowError($"قيمة '{fieldName}' غير صالحة. أدخل رقماً صحيحاً");
                return false;
            }
            return true;
        }

        private void ShowError(string message) => MessageBox.Show(message, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        private void CancelButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}