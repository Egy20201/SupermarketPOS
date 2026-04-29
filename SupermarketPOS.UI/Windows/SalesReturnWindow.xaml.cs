using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SupermarketPOS.UI
{
    public partial class SalesReturnWindow : BaseWindow
    {
        private ObservableCollection<ReturnItem> returnItems;
        private SalesReturnInvoiceDto originalInvoice;
        private int currentWarehouseId;
        private System.Collections.Generic.List<SaleInvoice> allInvoices;
        private readonly SalesReturnService _salesReturnService;
        private readonly InventoryModuleService _inventoryService;
        private readonly INotificationService _notificationService;

        public SalesReturnWindow(SalesReturnService salesReturnService, InventoryModuleService inventoryService, INotificationService notificationService)
        {
            InitializeComponent();
            _salesReturnService = salesReturnService ?? throw new ArgumentNullException(nameof(salesReturnService));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            returnItems = new ObservableCollection<ReturnItem>();
            ReturnGrid.ItemsSource = returnItems;
            RegisterTransactionShortcuts(() => SaveButton_Click(null, null), () => CancelButton_Click(null, null), () => PrintButton_Click(null, null));
            LoadWarehouses();
            LoadAllInvoices();
        }

        private void LoadWarehouses()
        {
            var warehouses = _inventoryService.GetActiveWarehouses();
            WarehouseCombo.ItemsSource = warehouses;
            WarehouseCombo.DisplayMemberPath = "Name";
            WarehouseCombo.SelectedValuePath = "Id";
            var def = warehouses.FirstOrDefault(w => w.IsDefault) ?? warehouses.FirstOrDefault();
            if (def != null) { WarehouseCombo.SelectedValue = def.Id; currentWarehouseId = def.Id; }
        }

        private void LoadAllInvoices()
        {
            allInvoices = _salesReturnService.GetRecentInvoices(200);
        }

        private void InvoiceSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var search = InvoiceSearchBox.Text.Trim();
                var inv = allInvoices.FirstOrDefault(i => i.InvoiceNumber.Contains(search));
                if (inv != null) LoadInvoice(inv.Id);
                else _notificationService.Show("الفاتورة غير موجودة", NotificationType.Warning);
            }
        }

        private void LoadInvoice(int id)
        {
            originalInvoice = _salesReturnService.GetInvoice(id);
            if (originalInvoice == null) return;

            OriginalInvoiceText.Text = $"📋 الفاتورة: {originalInvoice.InvoiceNumber} | {originalInvoice.Date:dd/MM/yyyy} | {originalInvoice.NetAmount:N2} ج.م";
            CustomerInfoText.Text = originalInvoice.CustomerId.HasValue ? $"👤 {originalInvoice.CustomerName} | الرصيد: {originalInvoice.CustomerBalance:N2} ج.م" : "👤 عميل نقدي";

            returnItems.Clear();
            foreach (var i in originalInvoice.Items)
                returnItems.Add(new ReturnItem { SaleItemId = i.SourceItemId, ProductId = i.ProductId, ProductName = i.ProductName, OriginalQuantity = i.OriginalQuantity, UnitPrice = i.UnitPrice });
            UpdateTotals();
        }

        private void UpdateTotals()
        {
            var items = returnItems.Where(i => i.ReturnQuantity > 0).ToList();
            var total = items.Sum(i => i.Total);
            TotalReturnText.Text = $"💰 إجمالي المرتجع: {total:N2} ج.م";

            if (originalInvoice?.CustomerId != null)
                CustomerBalanceText.Text = $"💳 رصيد العميل بعد المرتجع: {_salesReturnService.PreviewCustomerBalanceAfterReturn(originalInvoice.CustomerId, total):N2} ج.م";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var items = returnItems.Where(i => i.ReturnQuantity > 0).ToList();
            if (!items.Any() || originalInvoice == null || WarehouseCombo.SelectedValue == null) return;

            int warehouseId = (int)WarehouseCombo.SelectedValue;
            var total = items.Sum(i => i.Total);

            _salesReturnService.SaveReturn(originalInvoice.InvoiceId, warehouseId, items.Select(i => new ReturnSaveLineDto { SourceItemId = i.SaleItemId, ProductId = i.ProductId, ProductName = i.ProductName, ReturnQuantity = i.ReturnQuantity, UnitPrice = i.UnitPrice }));

            DashboardEvents.RequestRefresh(); ClearForm(); LoadAllInvoices(); MarkAsClean();
            _notificationService.Show($"تم حفظ المرتجع: {total:N2} ج.م", NotificationType.Success);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => ClearForm();
        private void ClearForm() { originalInvoice = null; returnItems.Clear(); InvoiceSearchBox.Text = OriginalInvoiceText.Text = CustomerInfoText.Text = ""; TotalReturnText.Text = "💰 إجمالي المرتجع: 0.00 ج.م"; CustomerBalanceText.Text = ""; }
        private void PrintButton_Click(object sender, RoutedEventArgs e) => _notificationService.Show("طباعة", NotificationType.Info);
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class ReturnItem : INotifyPropertyChanged
    {
        public int SaleItemId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int OriginalQuantity { get; set; }
        private int _returnQuantity;
        public int ReturnQuantity { get => _returnQuantity; set { _returnQuantity = value; OnPropertyChanged(nameof(ReturnQuantity)); OnPropertyChanged(nameof(Total)); } }
        public decimal UnitPrice { get; set; }
        public decimal Total => ReturnQuantity * UnitPrice;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
