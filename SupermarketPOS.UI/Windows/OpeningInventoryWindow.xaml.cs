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
using System.Windows.Data;

namespace SupermarketPOS.UI
{
    public partial class OpeningInventoryWindow : BaseWindow
    {
        private ObservableCollection<OpeningInventoryItemDto> inventoryItems;
        private int currentWarehouseId;
        private readonly InventoryModuleService _inventoryService;

        public OpeningInventoryWindow()
        {
            InitializeComponent();
            _inventoryService = DependencyInjection.GetRequiredService<InventoryModuleService>();
            inventoryItems = new ObservableCollection<OpeningInventoryItemDto>();
            InventoryGrid.ItemsSource = inventoryItems;
            RegisterListShortcuts(null, null, null, () => SearchBox.Focus(), LoadInventory, null);
            LoadWarehouses();
        }

        private void LoadWarehouses()
        {
            var warehouses = _inventoryService.GetActiveWarehouses();
            WarehouseCombo.ItemsSource = warehouses;
            WarehouseCombo.DisplayMemberPath = "Name";
            WarehouseCombo.SelectedValuePath = "Id";
            if (warehouses.Any()) { var def = warehouses.FirstOrDefault(w => w.IsDefault) ?? warehouses.First(); WarehouseCombo.SelectedValue = def.Id; currentWarehouseId = def.Id; }
            WarehouseCombo.SelectionChanged += (s, e) => { if (WarehouseCombo.SelectedValue != null) { currentWarehouseId = (int)WarehouseCombo.SelectedValue; LoadInventory(); } };
            SearchBox.TextChanged += (s, e) => FilterInventory();
        }

        private void LoadInventory()
        {
            inventoryItems.Clear();
            foreach (var item in _inventoryService.GetOpeningInventory(currentWarehouseId)) inventoryItems.Add(item);
            UpdateTotals();
        }

        private void FilterInventory()
        {
            var search = SearchBox.Text?.Trim().ToLower() ?? "";
            var view = CollectionViewSource.GetDefaultView(InventoryGrid.ItemsSource);
            view.Filter = o => o is OpeningInventoryItemDto i && (string.IsNullOrEmpty(search) || i.ProductName.ToLower().Contains(search) || i.Barcode.ToLower().Contains(search));
            UpdateTotals();
        }

        private void UpdateTotals()
        {
            var items = inventoryItems.Where(i => i.Quantity > 0).ToList();
            TotalItemsText.Text = $"📦 عدد الأصناف ذات الرصيد: {items.Count}";
            TotalValueText.Text = $"💰 إجمالي قيمة أرصدة أول المدة: {items.Sum(i => i.TotalValue):N2} ج.م";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var items = inventoryItems.Where(i => i.Quantity > 0).ToList();
            if (!items.Any()) { MessageBox.Show("لا توجد أرصدة"); return; }

            _inventoryService.SaveOpeningInventory(currentWarehouseId, items.Select(i => new OpeningInventorySaveItemDto { ProductId = i.ProductId, Quantity = i.Quantity, UnitCost = i.UnitCost }));
            DashboardEvents.RequestRefresh(); MarkAsClean(); MessageBox.Show("تم الحفظ");
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e) => LoadInventory();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(InventoryGrid, "أرصدة أول المدة");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
