using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
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
    public partial class StockTransferWindow : BaseWindow
    {
        private ObservableCollection<TransferItem> transferItems;
        private Warehouse fromWarehouse, toWarehouse;
        private System.Collections.Generic.List<Warehouse> allWarehouses;
        private System.Collections.Generic.List<Product> allProducts;
        private readonly InventoryModuleService _inventoryService;

        public StockTransferWindow()
        {
            InitializeComponent();
            _inventoryService = DependencyInjection.GetRequiredService<InventoryModuleService>();
            transferItems = new ObservableCollection<TransferItem>();
            TransferGrid.ItemsSource = transferItems;
            RegisterTransactionShortcuts(() => SaveButton_Click(null, null), () => Close(), () => PrintButton_Click(null, null));
            LoadData();
            GenerateNewTransferNumber();
        }

        private void LoadData()
        {
            allWarehouses = _inventoryService.GetActiveWarehouses();
            allProducts = _inventoryService.GetActiveProducts();
            transferItems.Add(new TransferItem());
        }

        private void GenerateNewTransferNumber()
        {
            TransferNumberText.Text = _inventoryService.GetNextTransferNumber();
        }

        private void FromWarehouseCodeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && int.TryParse(FromWarehouseCodeBox.Text, out int id))
            {
                fromWarehouse = allWarehouses.FirstOrDefault(w => w.Id == id);
                if (fromWarehouse != null) FromWarehouseNameBox.Text = fromWarehouse.Name;
            }
        }

        private void ToWarehouseCodeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && int.TryParse(ToWarehouseCodeBox.Text, out int id))
            {
                toWarehouse = allWarehouses.FirstOrDefault(w => w.Id == id);
                if (toWarehouse != null) ToWarehouseNameBox.Text = toWarehouse.Name;
            }
        }

        private void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            var empty = transferItems.FirstOrDefault(i => i.ProductId == 0);
            if (empty == null) { empty = new TransferItem(); transferItems.Add(empty); }
            TransferGrid.SelectedItem = empty;
            TransferGrid.BeginEdit();
        }

        private void ProductCode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox tb)
            {
                var product = allProducts.FirstOrDefault(p => p.Barcode == tb.Text || p.Name.Contains(tb.Text));
                if (product != null && TransferGrid.SelectedItem is TransferItem item)
                {
                    item.ProductId = product.Id;
                    item.ProductCode = product.Barcode;
                    item.ProductName = product.Name;
                    item.AvailableQuantity = GetAvailableQuantity(product.Id);
                }
                UpdateTotals();
            }
        }

        private int GetAvailableQuantity(int productId)
        {
            if (fromWarehouse == null) return 0;
            return _inventoryService.GetAvailableQuantity(productId, fromWarehouse.Id);
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TransferItem item && item.ProductId > 0)
            {
                transferItems.Remove(item);
                UpdateTotals();
            }
        }

        private void UpdateTotals()
        {
            var items = transferItems.Where(i => i.ProductId > 0).ToList();
            TotalItemsText.Text = $"📦 عدد الأصناف: {items.Count}";
            TotalQuantityText.Text = $"📊 إجمالي الكميات: {items.Sum(i => i.Quantity)}";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var items = transferItems.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();
            if (!items.Any() || fromWarehouse == null || toWarehouse == null || fromWarehouse.Id == toWarehouse.Id)
            { MessageBox.Show("بيانات غير صحيحة"); return; }

            _inventoryService.SaveStockTransfer(fromWarehouse.Id, toWarehouse.Id, TransferNumberText.Text, items.Select(i => new StockTransferSaveItemDto { ProductId = i.ProductId, ProductName = i.ProductName, Quantity = i.Quantity }));

            DashboardEvents.RequestRefresh();
            transferItems.Clear(); transferItems.Add(new TransferItem());
            fromWarehouse = toWarehouse = null;
            FromWarehouseCodeBox.Text = FromWarehouseNameBox.Text = ToWarehouseCodeBox.Text = ToWarehouseNameBox.Text = "";
            GenerateNewTransferNumber(); MarkAsClean();
            MessageBox.Show("تم الحفظ");
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e) => MessageBox.Show("طباعة");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class TransferItem : INotifyPropertyChanged
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public int AvailableQuantity { get; set; }
        private int _quantity = 1;
        public int Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
