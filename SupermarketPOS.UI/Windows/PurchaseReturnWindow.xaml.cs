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
    public partial class PurchaseReturnWindow : BaseWindow
    {
        private ObservableCollection<PurchaseReturnItem> returnItems;
        private PurchaseReturnInvoiceDto originalInvoice;
        private int currentWarehouseId;
        private System.Collections.Generic.List<PurchaseInvoice> allInvoices;
        private readonly PurchaseReturnService _purchaseReturnService;
        private readonly InventoryModuleService _inventoryService;
        private readonly INotificationService _notificationService;

        public PurchaseReturnWindow(PurchaseReturnService purchaseReturnService, InventoryModuleService inventoryService, INotificationService notificationService)
        {
            InitializeComponent();
            _purchaseReturnService = purchaseReturnService ?? throw new ArgumentNullException(nameof(purchaseReturnService));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            returnItems = new ObservableCollection<PurchaseReturnItem>();
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
            allInvoices = _purchaseReturnService.GetRecentInvoices(200);
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
            originalInvoice = _purchaseReturnService.GetInvoice(id);
            if (originalInvoice == null) return;

            OriginalInvoiceText.Text = $"📋 الفاتورة: {originalInvoice.InvoiceNumber} | {originalInvoice.Date:dd/MM/yyyy} | {originalInvoice.TotalAmount:N2} ج.م";
            SupplierInfoText.Text = originalInvoice.SupplierId.HasValue ? $"🏢 {originalInvoice.SupplierName} | الرصيد: {originalInvoice.SupplierBalance:N2} ج.م" : "🏢 مورد نقدي";

            returnItems.Clear();
            foreach (var i in originalInvoice.Items)
                returnItems.Add(new PurchaseReturnItem { PurchaseItemId = i.SourceItemId, ProductId = i.ProductId, ProductName = i.ProductName, OriginalQuantity = i.OriginalQuantity, UnitPrice = i.UnitPrice });
            UpdateTotals();
        }

        private void UpdateTotals()
        {
            var items = returnItems.Where(i => i.ReturnQuantity > 0).ToList();
            var total = items.Sum(i => i.Total);
            TotalReturnText.Text = $"💰 إجمالي المرتجع: {total:N2} ج.م";

            if (originalInvoice?.SupplierId != null)
                SupplierBalanceText.Text = $"💳 رصيد المورد بعد المرتجع: {_purchaseReturnService.PreviewSupplierBalanceAfterReturn(originalInvoice.SupplierId, total):N2} ج.م";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var items = returnItems.Where(i => i.ReturnQuantity > 0).ToList();
            if (!items.Any() || originalInvoice == null || WarehouseCombo.SelectedValue == null) return;

            int warehouseId = (int)WarehouseCombo.SelectedValue;
            var total = items.Sum(i => i.Total);

            _purchaseReturnService.SaveReturn(originalInvoice.InvoiceId, warehouseId, items.Select(i => new PurchaseReturnSaveLineDto { SourceItemId = i.PurchaseItemId, ProductId = i.ProductId, ProductName = i.ProductName, ReturnQuantity = i.ReturnQuantity, UnitPrice = i.UnitPrice }));

            DashboardEvents.RequestRefresh(); ClearForm(); LoadAllInvoices(); MarkAsClean();
            _notificationService.Show($"تم حفظ المرتجع: {total:N2} ج.م", NotificationType.Success);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => ClearForm();
        private void ClearForm() { originalInvoice = null; returnItems.Clear(); InvoiceSearchBox.Text = OriginalInvoiceText.Text = SupplierInfoText.Text = ""; TotalReturnText.Text = "💰 إجمالي المرتجع: 0.00 ج.م"; SupplierBalanceText.Text = ""; }
        private void PrintButton_Click(object sender, RoutedEventArgs e) => _notificationService.Show("طباعة", NotificationType.Info);
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class PurchaseReturnItem : INotifyPropertyChanged { public int PurchaseItemId { get; set; } public int ProductId { get; set; } public string ProductName { get; set; } public int OriginalQuantity { get; set; } private int _retQty; public int ReturnQuantity { get => _retQty; set { _retQty = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); } } public decimal UnitPrice { get; set; } public decimal Total => ReturnQuantity * UnitPrice; public event PropertyChangedEventHandler PropertyChanged; protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
}
