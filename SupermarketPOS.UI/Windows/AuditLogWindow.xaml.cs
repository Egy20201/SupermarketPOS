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
    public partial class AuditLogWindow : BaseWindow
    {
        private ObservableCollection<AuditLog> logs;
        private readonly AdminService _adminService;

        public AuditLogWindow()
        {
            InitializeComponent();
            _adminService = DependencyInjection.GetRequiredService<AdminService>();
            logs = new ObservableCollection<AuditLog>();
            AuditGrid.ItemsSource = logs;
            RegisterReportShortcuts(() => LoadLogs(), () => ExportButton_Click(null, null), null);
            LoadFilters();
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-7);
            ToDatePicker.SelectedDate = DateTime.Today;
            LoadLogs();
        }

        private void LoadFilters()
        {
            ActionTypeFilter.ItemsSource = new[] { "الكل", "LOGIN", "LOGOUT", "CREATE", "UPDATE", "DELETE", "PRINT" };
            ActionTypeFilter.SelectedIndex = 0;
        }

        private void LoadLogs()
        {
            var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-7);
            var to = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);
            var action = ActionTypeFilter.SelectedItem?.ToString();
            var search = SearchBox.Text?.Trim().ToLower() ?? "";

            var list = _adminService.GetAuditLogs(from, to, action, search);
            logs.Clear();
            foreach (var l in list) logs.Add(l);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadLogs();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(AuditGrid, "سجل التدقيق");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
