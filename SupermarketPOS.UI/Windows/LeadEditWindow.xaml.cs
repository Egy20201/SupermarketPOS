using SupermarketPOS.Business;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.UI.Infrastructure;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Windows
{
    public partial class LeadEditWindow : Window
    {
        private readonly CrmService _crmService;

        public LeadEditWindow(CrmService crmService)
        {
            InitializeComponent();
            _crmService = crmService ?? DependencyInjection.GetRequiredService<CrmService>();
            SourceCombo.SelectedIndex = 0;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(FullNameBox.Text))
                {
                    MessageBox.Show("يرجى إدخال الاسم", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                decimal.TryParse(ValueBox.Text, out decimal value);

                var lead = new Lead
                {
                    FullName = FullNameBox.Text.Trim(),
                    CompanyName = CompanyBox.Text?.Trim(),
                    Phone = PhoneBox.Text?.Trim(),
                    Email = EmailBox.Text?.Trim(),
                    Source = (SourceCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                    PotentialValue = value,
                    BranchId = App.CurrentUser?.BranchId,
                    AssignedToUserId = App.CurrentUser?.Id
                };

                _crmService.CreateLead(lead, App.CurrentUser?.Id ?? 1);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Lead save failed");
                MessageBox.Show(ex.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}