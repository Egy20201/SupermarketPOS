using SupermarketPOS.Business;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Windows
{
    public partial class SystemSettingsWindow : Window
    {
        private readonly AdminService _adminService;

        public SystemSettingsWindow()
        {
            InitializeComponent();
            _adminService = DependencyInjection.GetRequiredService<AdminService>();
            LoadCurrentBusinessType();
        }

        private void LoadCurrentBusinessType()
        {
            try
            {
                var currentValue = _adminService.GetBusinessType();
                BusinessTypeCombo.SelectedIndex = string.Equals(currentValue, "pharmacy", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load business type");
                BusinessTypeCombo.SelectedIndex = 0;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = BusinessTypeCombo.SelectedItem as ComboBoxItem;
            var selectedValue = selectedItem?.Tag as string;

            if (string.IsNullOrWhiteSpace(selectedValue))
            {
                MessageBox.Show("ظٹط±ط¬ظ‰ ط§ط®طھظٹط§ط± ظ†ظˆط¹ ط§ظ„ظ†ط´ط§ط·.", "طھظ†ط¨ظٹظ‡", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                "طھط؛ظٹظٹط± ط§ظ„ظ†ط´ط§ط· ظ‚ط¯ ظٹط¤ط«ط± ط¹ظ„ظ‰ ط§ظ„ط¨ظٹط§ظ†ط§طھطŒ ظ‡ظ„ طھط±ظٹط¯ ط§ظ„ظ…طھط§ط¨ط¹ط©طں",
                "طھط£ظƒظٹط¯",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                _adminService.SaveBusinessType(selectedValue);
                MessageBox.Show("طھظ… ط­ظپط¸ ط§ظ„ط¥ط¹ط¯ط§ط¯ط§طھ ط¨ظ†ط¬ط§ط­.", "ظ†ط¬ط§ط­", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save business type");
                MessageBox.Show("طھط¹ط°ط± ط­ظپط¸ ط§ظ„ط¥ط¹ط¯ط§ط¯ط§طھ.", "ط®ط·ط£", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
