using SupermarketPOS.Business;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.UI.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Windows
{
    public partial class AuditLogViewerWindow : Window
    {
        private readonly AuditService _auditService;
        private ObservableCollection<AuditLogEntry> _logs;

        public AuditLogViewerWindow()
        {
            InitializeComponent();
            _auditService = DependencyInjection.GetRequiredService<AuditService>();
            _logs = new ObservableCollection<AuditLogEntry>();
            AuditGrid.ItemsSource = _logs;
            LoadLogs();
        }

        private void LoadLogs()
        {
            try
            {
                var logs = _auditService.GetAllLogs(maxResults: 1000);
                _logs.Clear();
                foreach (var log in logs)
                    _logs.Add(log);
                TotalText.Text = $"ط¹ط¯ط¯ ط§ظ„ط³ط¬ظ„ط§طھ: {logs.Count}";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Audit log load failed");
                TotalText.Text = "ظپط´ظ„ طھط­ظ…ظٹظ„ ط§ظ„ط³ط¬ظ„ط§طھ";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadLogs();
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var action = string.IsNullOrWhiteSpace(ActionFilter.Text) ? null : ActionFilter.Text.Trim();
                var entityName = string.IsNullOrWhiteSpace(EntityFilter.Text) ? null : EntityFilter.Text.Trim();

                var logs = _auditService.GetAllLogs(action: action, entityName: entityName, maxResults: 1000);
                _logs.Clear();
                foreach (var log in logs)
                    _logs.Add(log);
                TotalText.Text = $"ط¹ط¯ط¯ ط§ظ„ط³ط¬ظ„ط§طھ: {logs.Count}";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Audit log filter failed");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
