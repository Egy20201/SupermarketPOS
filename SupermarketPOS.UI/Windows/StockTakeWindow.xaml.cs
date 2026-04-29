using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI
{
    public partial class StockTakeWindow : BaseWindow
    {
        private List<StockTakeRowDto> stockTakeRows;
        private readonly InventoryModuleService _inventoryService;

        public StockTakeWindow()
        {
            InitializeComponent();
            _inventoryService = DependencyInjection.GetRequiredService<InventoryModuleService>();
            RegisterListShortcuts(null, null, null, () => SearchBox.Focus(), LoadStockTake, null);
            LoadWarehouses();
        }

        private void LoadWarehouses()
        {
            var warehouses = _inventoryService.GetActiveWarehouses();
            WarehouseCombo.ItemsSource = warehouses;
            WarehouseCombo.DisplayMemberPath = "Name";
            WarehouseCombo.SelectedValuePath = "Id";
            if (warehouses.Any()) WarehouseCombo.SelectedIndex = 0;
        }

        private void LoadStockTake()
        {
            if (WarehouseCombo.SelectedValue == null) return;
            int warehouseId = (int)WarehouseCombo.SelectedValue;

            stockTakeRows = _inventoryService.GetStockTakeRows(warehouseId);
            StockTakeGrid.ItemsSource = stockTakeRows;
            UpdateStats();
        }

        private void UpdateStats()
        {
            if (stockTakeRows == null) return;
            TotalItemsText.Text = $"📦 عدد الأصناف: {stockTakeRows.Count}";
            TotalQuantityText.Text = $"📊 إجمالي الكميات: {stockTakeRows.Sum(r => r.SystemQuantity)}";
            TotalValueText.Text = $"💰 قيمة المخزون: {stockTakeRows.Sum(r => r.SystemQuantity * r.PurchasePrice):N2} ج.م";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (WarehouseCombo.SelectedValue == null) return;
            int warehouseId = (int)WarehouseCombo.SelectedValue;
            var differences = stockTakeRows.Where(r => r.ActualQuantity != r.SystemQuantity).ToList();
            if (!differences.Any()) { MessageBox.Show("لا توجد فروقات"); return; }
            if (MessageBox.Show($"سيتم تطبيق التسوية على {differences.Count} منتج", "تأكيد", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            _inventoryService.SaveStockTake(warehouseId, differences.Select(r => new StockTakeSaveItemDto { ProductId = r.ProductId, SystemQuantity = r.SystemQuantity, ActualQuantity = r.ActualQuantity, PurchasePrice = r.PurchasePrice }));

            DashboardEvents.RequestRefresh(); LoadStockTake(); MarkAsClean();
            MessageBox.Show("تم الحفظ");
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e) => LoadStockTake();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
