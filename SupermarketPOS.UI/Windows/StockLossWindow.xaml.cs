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
    public partial class StockLossWindow : BaseWindow
    {
        private ObservableCollection<StockLossItem> lossItems;
        private System.Collections.Generic.List<Product> allProducts;
        private System.Collections.Generic.List<Warehouse> allWarehouses;
        private readonly InventoryModuleService _inventoryService;

        public StockLossWindow()
        {
            InitializeComponent();
            _inventoryService = DependencyInjection.GetRequiredService<InventoryModuleService>();
            lossItems = new ObservableCollection<StockLossItem>();
            LossGrid.ItemsSource = lossItems;
            RegisterTransactionShortcuts(() => SaveButton_Click(null, null), () => Close(), null);
            LoadData();
        }

        private void LoadData()
        {
            allProducts = _inventoryService.GetActiveProducts();
            allWarehouses = _inventoryService.GetActiveWarehouses();
            WarehouseCombo.ItemsSource = allWarehouses;
            WarehouseCombo.DisplayMemberPath = "Name";
            WarehouseCombo.SelectedValuePath = "Id";
            if (allWarehouses.Any()) WarehouseCombo.SelectedIndex = 0;
        }

        private void ProductSearchBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SearchAndAddProduct(); }
        private void SearchButton_Click(object sender, RoutedEventArgs e) => SearchAndAddProduct();

        private void SearchAndAddProduct()
        {
            var search = ProductSearchBox.Text?.Trim(); if (string.IsNullOrEmpty(search)) return;
            var product = allProducts.FirstOrDefault(p => p.Barcode == search) ?? allProducts.FirstOrDefault(p => p.Name.Contains(search));
            if (product == null) { MessageBox.Show("لم يتم العثور"); return; }
            if (lossItems.Any(i => i.ProductId == product.Id)) { MessageBox.Show("موجود"); return; }

            int warehouseId = (int)WarehouseCombo.SelectedValue;
            var stock = _inventoryService.GetProductStockInfo(product.Id, warehouseId);
            lossItems.Add(new StockLossItem { ProductId = product.Id, Barcode = product.Barcode, ProductName = product.Name, CurrentStock = stock.CurrentStock, Quantity = 0, Cost = stock.Cost });
            ProductSearchBox.Text = ""; UpdateTotal();
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e) { if (sender is Button btn && btn.DataContext is StockLossItem item) { lossItems.Remove(item); UpdateTotal(); } }
        private void ClearButton_Click(object sender, RoutedEventArgs e) { lossItems.Clear(); UpdateTotal(); }
        private void UpdateTotal() { TotalLossText.Text = $"💰 إجمالي قيمة التالف: {lossItems.Sum(i => i.Total):N2} ج.م"; }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var items = lossItems.Where(i => i.Quantity > 0).ToList();
            if (!items.Any() || WarehouseCombo.SelectedValue == null) return;
            int warehouseId = (int)WarehouseCombo.SelectedValue;

            _inventoryService.SaveStockLoss(warehouseId, items.Select(i => new StockLossSaveItemDto { ProductId = i.ProductId, ProductName = i.ProductName, Quantity = i.Quantity, Cost = i.Cost }), NotesBox.Text);

            DashboardEvents.RequestRefresh(); lossItems.Clear(); NotesBox.Text = ""; UpdateTotal(); MarkAsClean();
            MessageBox.Show("تم الحفظ");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class StockLossItem : INotifyPropertyChanged { public int ProductId { get; set; } public string Barcode { get; set; } public string ProductName { get; set; } public int CurrentStock { get; set; } private int _qty; public int Quantity { get => _qty; set { _qty = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); } } public decimal Cost { get; set; } public decimal Total => Quantity * Cost; public event PropertyChangedEventHandler PropertyChanged; protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
}
