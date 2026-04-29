using SupermarketPOS.Business;
using Microsoft.Win32;
using System;
using System.Data.SqlClient;
using System.Windows;

namespace SupermarketPOS.UI
{
    public partial class RestoreWindow : BaseWindow
    {
        private string connectionString;
        private string databaseName;

        public RestoreWindow()
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

        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "SQL Backup (*.bak)|*.bak", Title = "اختر ملف النسخة الاحتياطية" };
            if (dialog.ShowDialog() == true)
            {
                if (MessageBox.Show("سيتم فقدان جميع البيانات الحالية. هل أنت متأكد؟", "تأكيد", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                RestoreButton.IsEnabled = false;
                StatusText.Text = "⏳ جاري الاستعادة...";

                try
                {
                    await System.Threading.Tasks.Task.Run(() => PerformRestore(dialog.FileName));
                    StatusText.Text = "✅ تمت الاستعادة بنجاح";
                    MessageBox.Show("تمت استعادة قاعدة البيانات. سيتم إعادة تشغيل البرنامج.", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Database restore failed");
                    StatusText.Text = "❌ فشلت الاستعادة";
                    MessageBox.Show($"خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    RestoreButton.IsEnabled = true;
                }
            }
        }

        private void PerformRestore(string filePath)
        {
            var masterConn = connectionString.Replace($"Initial Catalog={databaseName}", "Initial Catalog=master");
            using (var conn = new SqlConnection(masterConn))
            {
                conn.Open();
                var setSingle = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                using (var cmd = new SqlCommand(setSingle, conn)) { cmd.ExecuteNonQuery(); }
                var restore = $"RESTORE DATABASE [{databaseName}] FROM DISK = '{filePath}' WITH REPLACE";
                using (var cmd = new SqlCommand(restore, conn)) { cmd.CommandTimeout = 300; cmd.ExecuteNonQuery(); }
                var setMulti = $"ALTER DATABASE [{databaseName}] SET MULTI_USER";
                using (var cmd = new SqlCommand(setMulti, conn)) { cmd.ExecuteNonQuery(); }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}