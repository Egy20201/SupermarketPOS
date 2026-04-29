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
    public partial class StockCardWindow : BaseWindow
    {
        private ObservableCollection<StockCardRowDto> items;
        private Product selectedProduct;
        private System.Collections.Generic.List<Product> allProducts;
        private System.Collections.Generic.List<Warehouse> allWarehouses;
        private readonly InventoryModuleService _inventoryService;

        public StockCardWindow()
        {
            InitializeComponent();
            _inventoryService = DependencyInjection.GetRequiredService<InventoryModuleService>();
            items = new ObservableCollection<StockCardRowDto>();
            StockCardGrid.ItemsSource = items;
            RegisterReportShortcuts(() => LoadStockCard(), () => ExportButton_Click(null, null), null);
            LoadData();
            FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
            ToDatePicker.SelectedDate = DateTime.Today;
        }

        private void LoadData()
        {
            allProducts = _inventoryService.GetActiveProducts();
            allWarehouses = _inventoryService.GetActiveWarehouses();
            WarehouseFilterCombo.ItemsSource = _inventoryService.GetActiveWarehouses(true);
            WarehouseFilterCombo.DisplayMemberPath = "Name";
            WarehouseFilterCombo.SelectedValuePath = "Id";
            WarehouseFilterCombo.SelectedIndex = 0;
        }

        private void ProductSearchBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SearchProduct(); }
        private void SearchButton_Click(object sender, RoutedEventArgs e) => SearchProduct();

        private void SearchProduct()
        {
            var search = ProductSearchBox.Text?.Trim(); if (string.IsNullOrEmpty(search)) return;
            var matches = allProducts.Where(p => p.Barcode == search || p.Name.Contains(search)).ToList();
            if (matches.Count == 1) { selectedProduct = matches[0]; ProductInfoText.Text = $"{selectedProduct.Name} - {selectedProduct.Barcode}"; LoadStockCard(); }
            else if (matches.Count > 1) { selectedProduct = matches[0]; ProductInfoText.Text = $"{selectedProduct.Name} - {selectedProduct.Barcode}"; LoadStockCard(); }
            else MessageBox.Show("لم يتم العثور");
        }

        private void LoadStockCard()
        {
            if (selectedProduct == null) return;
            var from = FromDatePicker.SelectedDate ?? DateTime.Today.AddDays(-30);
            var to = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);
            int? warehouseId = (int?)WarehouseFilterCombo.SelectedValue == 0 ? null : (int?)WarehouseFilterCombo.SelectedValue;

            var result = _inventoryService.GetStockCard(selectedProduct.Id, warehouseId, from, to);
            items.Clear();
            foreach (var row in result.Rows) items.Add(row);
            TotalInText.Text = $"📥 وارد: {result.TotalIn}";
            TotalOutText.Text = $"📤 منصرف: {result.TotalOut}";
            BalanceText.Text = $"📊 الرصيد: {result.Balance}";
            BalanceValueText.Text = $"💰 قيمة الرصيد: {result.BalanceValue:N2} ج.م";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadStockCard();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(StockCardGrid, "كارت الصنف");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
