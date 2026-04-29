using Microsoft.Extensions.DependencyInjection;
using SupermarketPOS.Business;
using SupermarketPOS.UI.Services;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace SupermarketPOS.UI
{
    public partial class LoginWindow : Window
    {
        private readonly AuthenticationService _authService;
        private readonly ICurrentUserService _currentUserService;

        public LoginWindow(AuthenticationService authService, ICurrentUserService currentUserService)
        {
            InitializeComponent();
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("ط£ط¯ط®ظ„ ط§ظ„ظ…ط³طھط®ط¯ظ… ظˆظƒظ„ظ…ط© ط§ظ„ظ…ط±ظˆط±");
                return;
            }

            LoginButton.Visibility = Visibility.Collapsed;

            try
            {
                var user = await Task.Run(() => _authService.Authenticate(username, password));
                if (user == null)
                {
                    ShowError("ط§ط³ظ… ط§ظ„ظ…ط³طھط®ط¯ظ… ط£ظˆ ظƒظ„ظ…ط© ط§ظ„ظ…ط±ظˆط± ط؛ظٹط± طµط­ظٹط­ط©");
                    LoginButton.Visibility = Visibility.Visible;
                    return;
                }

                _currentUserService.SetUser(user);

                var mainWindow = ((App)Application.Current).Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Login failed");
                ShowError("طھط¹ط°ط± ط§ظ„ط§طھطµط§ظ„ ط¨ظ‚ط§ط¹ط¯ط© ط§ظ„ط¨ظٹط§ظ†ط§طھ. ط­ط§ظˆظ„ ظ…ط±ط© ط£ط®ط±ظ‰.");
                LoginButton.Visibility = Visibility.Visible;
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

