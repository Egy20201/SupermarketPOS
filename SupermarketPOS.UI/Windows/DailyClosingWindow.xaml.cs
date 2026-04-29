using SupermarketPOS.Core.Entities;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SupermarketPOS.UI
{
    public partial class DailyClosingWindow : BaseWindow
    {
        private readonly AccountingService _accountingService;
        private DailyClosingSummaryDto _summary;

        public DailyClosingWindow()
        {
            InitializeComponent();
            _accountingService = DependencyInjection.GetRequiredService<AccountingService>();
            RegisterFormShortcuts(() => CloseDayButton_Click(null, null), () => Close());
            LoadTodayData();
        }

        private void LoadTodayData()
        {
            var today = DateTime.Today;
            _summary = _accountingService.GetDailyClosingSummary(today);
            if (_summary.IsClosed)
            {
                StatusText.Text = "✅ تم إغلاق هذا اليوم بالفعل";
                StatusText.Foreground = new SolidColorBrush(Colors.Green);
                return;
            }

            StatusText.Text = "🟢 اليوم مفتوح - جاهز للإغلاق";
            StatusText.Foreground = new SolidColorBrush(Colors.Orange);
            TotalInvoicesText.Text = $"🧾 عدد الفواتير: {_summary.TotalInvoices}";
            TotalSalesText.Text = $"💰 إجمالي المبيعات: {_summary.TotalSales:N2} ج.م";
            TotalCashSalesText.Text = $"💵 المبيعات النقدية: {_summary.TotalCashSales:N2} ج.م";
            TotalCreditSalesText.Text = $"📋 المبيعات الآجلة: {_summary.TotalCreditSales:N2} ج.م";
            ExpectedCashText.Text = $"🏦 النقدية المتوقعة: {_summary.ExpectedCash:N2} ج.م";
            ActualCashBox.Text = _summary.ExpectedCash.ToString("F2");
            UpdateDifference();
        }

        private void UpdateDifference()
        {
            if (!decimal.TryParse(ActualCashBox.Text, out var act)) return;

            var diff = act - (_summary?.ExpectedCash ?? 0);
            DifferenceText.Text = diff == 0 ? "✅ متطابق" : $"📊 الفرق: {diff:N2} ج.م";
            DifferenceText.Foreground = diff == 0 ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
        }

        private void CloseDayButton_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            if (!decimal.TryParse(ActualCashBox.Text, out var act)) return;
            _accountingService.CloseDay(today, App.CurrentUser?.Id ?? 1, act, NotesBox.Text);

            DashboardEvents.RequestRefresh();
            MarkAsClean();
            MessageBox.Show($"✅ تم إغلاق يوم {today:dd/MM/yyyy} بنجاح!\nالنقدية الفعلية: {ActualCashBox.Text} ج.م");
            Close();
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e) => MessageBox.Show("طباعة التقرير");
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
