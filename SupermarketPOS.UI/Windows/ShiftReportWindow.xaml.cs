using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SupermarketPOS.UI
{
    public partial class ShiftReportWindow : BaseWindow
    {
        private ObservableCollection<ShiftReportItemDto> reportItems;
        private readonly OperationalReportService _reportService;

        public ShiftReportWindow()
        {
            InitializeComponent();
            _reportService = DependencyInjection.GetRequiredService<OperationalReportService>();
            reportItems = new ObservableCollection<ShiftReportItemDto>();
            ReportGrid.ItemsSource = reportItems;
            RegisterReportShortcuts(() => LoadReport(), () => ExportButton_Click(null, null), null);
            LoadFilters();
            FromDatePicker.SelectedDate = DateTime.Today;
            ToDatePicker.SelectedDate = DateTime.Today;
            LoadReport();
        }

        private void LoadFilters()
        {
            UserFilterCombo.ItemsSource = _reportService.GetShiftReportUsers();
            UserFilterCombo.DisplayMemberPath = "FullName";
            UserFilterCombo.SelectedValuePath = "Id";
            UserFilterCombo.SelectedIndex = 0;
        }

        private void LoadReport()
        {
            var fromDate = FromDatePicker.SelectedDate ?? DateTime.Today;
            var toDate = (ToDatePicker.SelectedDate ?? DateTime.Today).AddDays(1);
            int? userId = (int?)UserFilterCombo.SelectedValue == 0 ? null : (int?)UserFilterCombo.SelectedValue;

            var result = _reportService.GetShiftReport(fromDate, toDate, userId);
            reportItems.Clear();
            foreach (var item in result.Items) reportItems.Add(item);

            TotalShiftsText.Text = $"🔄 عدد الشيفتات: {result.TotalShifts}";
            TotalSalesText.Text = $"💰 إجمالي المبيعات: {result.TotalSales:N2} ج.م";
            TotalCashText.Text = $"💵 إجمالي النقدية: {result.TotalCash:N2} ج.م";

            if (result.TotalDifference == 0)
            {
                TotalDifferenceText.Text = $"✅ إجمالي الفروق: {result.TotalDifference:N2} ج.م";
                TotalDifferenceText.Foreground = System.Windows.Media.Brushes.Green;
            }
            else if (result.TotalDifference > 0)
            {
                TotalDifferenceText.Text = $"📈 إجمالي الفروق: +{result.TotalDifference:N2} ج.م";
                TotalDifferenceText.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                TotalDifferenceText.Text = $"📉 إجمالي الفروق: {result.TotalDifference:N2} ج.م";
                TotalDifferenceText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void ReportGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ReportGrid.SelectedItem is ShiftReportItemDto item)
                ShowShiftDetails(item.Id);
        }

        private void ShowShiftDetails(int shiftId)
        {
            var details = _reportService.GetShiftDetailsText(shiftId);
            if (!string.IsNullOrWhiteSpace(details))
                MessageBox.Show(details, "تفاصيل الشيفت", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadReport();

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (reportItems.Count == 0)
            {
                MessageBox.Show("لا توجد بيانات للتصدير", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ExportHelper.ExportToExcel(ReportGrid, "تقرير الشيفتات",
                $"تقرير الشيفتات - {FromDatePicker.SelectedDate:dd/MM/yyyy} إلى {ToDatePicker.SelectedDate:dd/MM/yyyy}");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

}
