using Microsoft.Extensions.DependencyInjection;
using SupermarketPOS.Business;
using System;
using System.Windows;

namespace SupermarketPOS.UI.Windows
{
    public partial class SetupWizardWindow : Window
    {
        private readonly ApplicationStartupService _startupService;

        public SetupWizardWindow(ApplicationStartupService startupService)
        {
            InitializeComponent();
            _startupService = startupService ?? throw new ArgumentNullException(nameof(startupService));
        }

        private void SupermarketButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyBusinessTypeAndContinue("supermarket");
        }

        private void PharmacyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyBusinessTypeAndContinue("pharmacy");
        }

        private void ApplyBusinessTypeAndContinue(string businessType)
        {
            try
            {
                _startupService.ApplyBusinessType(businessType);

                var loginWindow = ((App)Application.Current).Services.GetRequiredService<LoginWindow>();
                loginWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Setup wizard failed");
                MessageBox.Show("تعذر حفظ الإعدادات.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
