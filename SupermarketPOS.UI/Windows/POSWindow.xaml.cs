using SupermarketPOS.Business;
using SupermarketPOS.UI.Services;
using SupermarketPOS.UI.ViewModels;
using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SupermarketPOS.UI.Windows
{
    public partial class POSWindow : BaseWindow
    {
        private POSViewModel _viewModel;

        public POSWindow(SalesService salesService, AuthorizationService authzService, SettingsService settingsService, ICurrentUserService currentUserService)
        {
            InitializeComponent();

            _viewModel = new POSViewModel(salesService, currentUserService);
            DataContext = _viewModel;

            BarcodeBox.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12 && _viewModel.SaveCommand.CanExecute(null))
            {
                _viewModel.SaveCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.F4 && _viewModel.NewCommand.CanExecute(null))
            {
                _viewModel.NewCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _viewModel.ClearCommand.CanExecute(null))
            {
                _viewModel.ClearCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
