using SupermarketPOS.Business;
using Microsoft.Win32;
using SupermarketPOS.UI.Services;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Windows;

namespace SupermarketPOS.UI
{
    public partial class BackupWindow : BaseWindow
    {
        private string connectionString;
        private string databaseName;

        public BackupWindow()
        {
            InitializeComponent();
            LoadConnectionInfo();
        }

        private void LoadConnectionInfo()
        {
            connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["SupermarketDBConnection"]?.ConnectionString;
            if (!string.IsNullOrEmpty(connectionString))
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                databaseName = builder.InitialCatalog;
                DatabaseNameText.Text = databaseName;
            }
        }

        private async void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "SQL Backup (*.bak)|*.bak", FileName = $"{databaseName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak" };
            if (dialog.ShowDialog() == true)
            {
                BackupButton.IsEnabled = false;
                StatusText.Text = "⏳ جاري النسخ...";

                try
                {
                    await System.Threading.Tasks.Task.Run(() => PerformBackup(dialog.FileName));
                    StatusText.Text = "✅ تم النسخ بنجاح";
                    MessageBox.Show("تم إنشاء النسخة الاحتياطية", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Database backup failed");
                    StatusText.Text = "❌ فشل النسخ";
                    MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally { BackupButton.IsEnabled = true; }
            }
        }

        private void PerformBackup(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var query = $"BACKUP DATABASE [{databaseName}] TO DISK = '{filePath}' WITH FORMAT, NAME = 'Full Backup'";
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(query, conn)) { cmd.CommandTimeout = 300; cmd.ExecuteNonQuery(); }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}