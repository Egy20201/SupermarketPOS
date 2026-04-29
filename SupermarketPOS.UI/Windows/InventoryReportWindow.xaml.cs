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

namespace SupermarketPOS.UI
{
    public partial class InventoryReportWindow : BaseWindow
    {
        private ObservableCollection<InventoryValuationItemDto> items;
        private readonly InventoryModuleService _inventoryService;

        public InventoryReportWindow()
        {
            InitializeComponent();
            _inventoryService = DependencyInjection.GetRequiredService<InventoryModuleService>();
            items = new ObservableCollection<InventoryValuationItemDto>();
            InventoryGrid.ItemsSource = items;
            RegisterReportShortcuts(() => LoadData(), () => ExportButton_Click(null, null), null);
            LoadWarehouses();
            LoadData();
        }

        private void LoadWarehouses()
        {
            WarehouseFilterCombo.ItemsSource = _inventoryService.GetActiveWarehouses(true);
            WarehouseFilterCombo.DisplayMemberPath = "Name";
            WarehouseFilterCombo.SelectedValuePath = "Id";
            WarehouseFilterCombo.SelectedIndex = 0;
        }

        private void LoadData()
        {
            int? warehouseId = (int?)WarehouseFilterCombo.SelectedValue == 0 ? null : (int?)WarehouseFilterCombo.SelectedValue;
            var asOf = AsOfDatePicker.SelectedDate ?? DateTime.Today;
            var showZero = ShowZeroStockCheckBox.IsChecked == true;

            var result = _inventoryService.GetInventoryValuation(warehouseId, showZero);
            items.Clear();
            foreach (var item in result.Items) items.Add(item);

            TotalItemsText.Text = $"📦 عدد الأصناف: {result.TotalItems}";
            TotalQuantityText.Text = $"📊 إجمالي الكميات: {result.TotalQuantity:N0}";
            TotalValueText.Text = $"💰 إجمالي قيمة المخزون: {result.TotalValue:N2} ج.م";
            FooterText.Text = $"تاريخ التقييم: {asOf:yyyy/MM/dd} | طريقة التقييم: متوسط التكلفة المرجح";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadData();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(InventoryGrid, "تقييم المخزون");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
