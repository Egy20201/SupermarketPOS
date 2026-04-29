using SupermarketPOS.Business;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SupermarketPOS.UI.Windows
{
    public partial class SalesQuotationWindow : Window
    {
        private readonly SalesWorkflowService _workflowService;
        private readonly SalesService _salesService;
        private readonly DocumentViewModel _viewModel;
        private ObservableCollection<QuotationItemDto> _items;
        private int? _quotationId;

        public SalesQuotationWindow()
        {
            InitializeComponent();
            try
            {
                _workflowService = DependencyInjection.GetRequiredService<SalesWorkflowService>();
                _salesService = DependencyInjection.GetRequiredService<SalesService>();
                _viewModel = new DocumentViewModel();
                _items = new ObservableCollection<QuotationItemDto>();
                ItemsGrid.ItemsSource = _items;
                DataContext = _viewModel;
                _viewModel.PropertyChanged += (s, e) => RefreshButtons();
                _viewModel.Status = DocumentStatus.Draft;
                LoadCustomers();
                GenerateNewQuotation();
                _viewModel.IsEditing = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "SalesQuotationWindow initialization failed");
            }
        }

        private void LoadCustomers() { var c = _salesService.GetActiveCustomers(); c.Insert(0, new SaleCustomerDto { Id = 0, Name = "-- اختر عميل --" }); CustomerCombo.ItemsSource = c; CustomerCombo.SelectedIndex = 0; }
        private void GenerateNewQuotation() { QuotationNumberBox.Text = "(جديد)"; _viewModel.Status = DocumentStatus.Draft; _viewModel.IsEditing = true; _viewModel.ClearError(); }

        private void SetStatusBadge(string text, string hexColor) { StatusText.Text = text; var color = (Color)ColorConverter.ConvertFromString(hexColor); StatusBadge.Background = new SolidColorBrush(color); }

        private void RefreshButtons()
        {
            var vm = _viewModel;
            BtnSaveDraft.IsEnabled = vm.CanEdit; BtnApprove.IsEnabled = vm.CanApprove && _quotationId.HasValue; BtnPost.IsEnabled = vm.CanPost; BtnCancel.IsEnabled = vm.CanCancel && _quotationId.HasValue;
            CustomerCombo.IsEnabled = vm.CanEdit; ItemsGrid.IsReadOnly = !vm.CanEdit; DatePicker.IsEnabled = vm.CanEdit;
            SetStatusBadge(vm.StatusText, vm.StatusColor);
            ErrorStrip.Visibility = vm.HasError ? Visibility.Visible : Visibility.Collapsed;
            if (vm.HasError) ErrorText.Text = vm.ErrorMessage;
        }

        private void SaveDraft_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.ClearError();
                if (CustomerCombo.SelectedValue == null || (int)CustomerCombo.SelectedValue == 0) { _viewModel.ShowError("يرجى اختيار عميل"); return; }
                var itemsList = _items.Where(i => i.ProductId > 0 && i.Quantity > 0).Select(i => (i.ProductId, i.Quantity, i.UnitPrice, i.Discount)).ToList();
                if (!itemsList.Any()) { _viewModel.ShowError("يرجى إضافة صنف واحد على الأقل"); return; }
                var q = _workflowService.CreateQuotation(App.CurrentUser?.Id ?? 1, (int)CustomerCombo.SelectedValue, App.CurrentUser?.BranchId, itemsList, null);
                _quotationId = q.Id; QuotationNumberBox.Text = q.Number; _viewModel.Status = DocumentStatus.Draft;
                MessageBox.Show("تم حفظ عرض السعر بنجاح", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { Logger.Error(ex, "Quotation save draft failed"); _viewModel.ShowError(ex.Message); }
        }

        private void Approve_Click(object sender, RoutedEventArgs e) { if (!_quotationId.HasValue || !_viewModel.ConfirmAction("اعتماد عرض السعر")) return; try { _viewModel.ClearError(); _workflowService.UpdateQuotationStatus(_quotationId.Value, DocumentStatus.Approved, App.CurrentUser?.Id ?? 1); _viewModel.Status = DocumentStatus.Approved; } catch (Exception ex) { Logger.Error(ex, "Quotation approve failed"); _viewModel.ShowError(ex.Message); } }
        private void Post_Click(object sender, RoutedEventArgs e) { if (!_quotationId.HasValue || !_viewModel.ConfirmAction("إرسال عرض السعر")) return; try { _viewModel.ClearError(); _workflowService.UpdateQuotationStatus(_quotationId.Value, DocumentStatus.Sent, App.CurrentUser?.Id ?? 1); _viewModel.Status = DocumentStatus.Sent; } catch (Exception ex) { Logger.Error(ex, "Quotation post failed"); _viewModel.ShowError(ex.Message); } }
        private void Cancel_Click(object sender, RoutedEventArgs e) { if (!_quotationId.HasValue || !_viewModel.ConfirmAction("إلغاء عرض السعر")) return; try { _viewModel.ClearError(); _workflowService.UpdateQuotationStatus(_quotationId.Value, DocumentStatus.Cancelled, App.CurrentUser?.Id ?? 1); _viewModel.Status = DocumentStatus.Cancelled; } catch (Exception ex) { Logger.Error(ex, "Quotation cancel failed"); _viewModel.ShowError(ex.Message); } }
        private void AddItem_Click(object sender, RoutedEventArgs e) { if (!_viewModel.CanEdit) return; _items.Add(new QuotationItemDto()); }
        private void RemoveItem_Click(object sender, RoutedEventArgs e) { if (!_viewModel.CanEdit) return; if (ItemsGrid.SelectedItem is QuotationItemDto item) _items.Remove(item); else if (_items.Count > 0) _items.RemoveAt(_items.Count - 1); }
        private void ItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) => UpdateTotals();
        private void UpdateTotals() { var subtotal = _items.Sum(i => i.TotalPrice); SubtotalText.Text = $"المجموع: {subtotal:N2} ج.م"; TotalText.Text = $"الإجمالي: {subtotal:N2} ج.م"; }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class QuotationItemDto : INotifyPropertyChanged { public int ProductId { get; set; } public string ProductName { get; set; } public int Quantity { get; set; } = 1; public decimal UnitPrice { get; set; } public decimal Discount { get; set; } public decimal TotalPrice => (Quantity * UnitPrice) - Discount; public event PropertyChangedEventHandler PropertyChanged; }
}