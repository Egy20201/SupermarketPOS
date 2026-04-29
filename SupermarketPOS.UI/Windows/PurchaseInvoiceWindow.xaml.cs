using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business;
using SupermarketPOS.UI.Services;
using SupermarketPOS.UI.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SupermarketPOS.UI
{
    public partial class PurchaseInvoiceWindow : BaseWindow
    {
        private PurchaseInvoiceViewModel _viewModel;

        public PurchaseInvoiceWindow()
        {
            InitializeComponent();
            _viewModel = new PurchaseInvoiceViewModel();
            DataContext = _viewModel;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => _viewModel.NewCommand.Execute(null);
        private void PrintButton_Click(object sender, RoutedEventArgs e) => MessageBox.Show("طباعة");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}