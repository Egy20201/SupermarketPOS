using LiveCharts;
using LiveCharts.Wpf;
using SupermarketPOS.Business;
using SupermarketPOS.Business.Modules;
using SupermarketPOS.UI.Controls;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using SupermarketPOS.UI.Windows;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavService = SupermarketPOS.UI.Services.NavigationService;

namespace SupermarketPOS.UI.Pages
{
    public partial class DashboardPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private SeriesCollection _salesSeries;
        private SeriesCollection _categorySeries;
        private string[] _salesAxisX;
        private Func<double, string> _salesAxisY;

        public SeriesCollection SalesSeries
        {
            get => _salesSeries;
            set { _salesSeries = value; OnPropertyChanged(nameof(SalesSeries)); }
        }

        public SeriesCollection CategorySeries
        {
            get => _categorySeries;
            set { _categorySeries = value; OnPropertyChanged(nameof(CategorySeries)); }
        }

        public string[] SalesAxisX
        {
            get => _salesAxisX;
            set { _salesAxisX = value; OnPropertyChanged(nameof(SalesAxisX)); }
        }

        public Func<double, string> SalesAxisY
        {
            get => _salesAxisY;
            set { _salesAxisY = value; OnPropertyChanged(nameof(SalesAxisY)); }
        }

        private readonly ReportService _reportService;
        private readonly OperationalReportService _operationalReportService;
        private readonly AuthorizationService _authzService;
        private System.Windows.Threading.DispatcherTimer _refreshTimer;
        private ObservableCollection<RecentInvoiceItem> _recentInvoices;

        public DashboardPage()
        {
            InitializeComponent();
            DataContext = this;

            // âœ… Initialize collections FIRST â€” prevents NullReferenceException
            _recentInvoices = new ObservableCollection<RecentInvoiceItem>();
            RecentInvoicesGrid.ItemsSource = _recentInvoices;

            // âœ… Initialize LiveCharts bindings FIRST â€” prevents NullReferenceException
            SalesChart.Series = new SeriesCollection();
            CategoryChart.Series = new SeriesCollection();

            // âœ… Initialize axes
            SalesAxisY = value => value.ToString("N0");

            _reportService = DependencyInjection.GetRequiredService<ReportService>();
            _operationalReportService = DependencyInjection.GetRequiredService<OperationalReportService>();
            _authzService = DependencyInjection.GetRequiredService<AuthorizationService>();

            // Auto-refresh every 60 seconds
            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _refreshTimer.Tick += (s, e) => LoadDashboardData();
            _refreshTimer.Start();

            // Stop timer when page unloads
            this.Unloaded += Page_Unloaded;

            LoadDashboardData();
            LoadChartData();
            LoadRecentInvoices();
            SetGreeting();
        }

        private void SetGreeting()
        {
            var hour = DateTime.Now.Hour;
            string greeting;
            if (hour < 12)
                greeting = "طµط¨ط§ط­ ط§ظ„ط®ظٹط±";
            else if (hour < 17)
                greeting = "ظ…ط³ط§ط، ط§ظ„ط®ظٹط±";
            else
                greeting = "ظ…ط³ط§ط، ط§ظ„ط®ظٹط±";

            GreetingText.Text = string.Format("{0}, {1} ًں‘‹",
                greeting,
                App.CurrentUser?.FullName ?? "ظ…ط³طھط®ط¯ظ…");

            DateText.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy",
                new System.Globalization.CultureInfo("ar-SA"));
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer = null;
            }
        }

        private void LoadDashboardData()
        {
            try
            {
                var dailyReport = _reportService.GetDailySales(DateTime.Today);
                KpiSales.Text = string.Format("{0:N2} ط¬.ظ…", dailyReport.NetSales);
                KpiInvoices.Text = dailyReport.InvoiceCount.ToString();
                KpiProfit.Text = string.Format("{0:N2} ط¬.ظ…", dailyReport.Profit);

                // Calculate trend (simple comparison with yesterday)
                var yesterdayReport = _reportService.GetDailySales(DateTime.Today.AddDays(-1));
                if (yesterdayReport.NetSales > 0)
                {
                    var trend = ((dailyReport.NetSales - yesterdayReport.NetSales) / yesterdayReport.NetSales) * 100;
                    KpiSalesTrend.Text = string.Format("{0}{1:F1}% ط¹ظ† ط£ظ…ط³",
                        trend >= 0 ? "â†‘ " : "â†“ ",
                        Math.Abs(trend));
                    KpiSalesTrend.Foreground = trend >= 0
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                }
                else
                {
                    KpiSalesTrend.Text = "ظ„ط§ طھظˆط¬ط¯ ط¨ظٹط§ظ†ط§طھ ط£ظ…ط³";
                    KpiSalesTrend.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0B0B0"));
                }

                var dashboard = _operationalReportService.GetDashboardSummary(DateTime.Today);
                KpiInventory.Text = dashboard.LowStockCount.ToString();
                KpiInventoryAlert.Text = dashboard.LowStockCount > 0
                    ? "âڑ ï¸ڈ ط¹ظ†ط§طµط± طھط­طھط§ط¬ ط¥ط¹ط§ط¯ط© ط·ظ„ط¨"
                    : "âœ… ط§ظ„ظƒظ…ظٹط§طھ ط¬ظٹط¯ط©";
                KpiInventoryAlert.Foreground = dashboard.LowStockCount > 0
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                KpiPurchases.Text = string.Format("{0:N2} ط¬.ظ…", dashboard.MonthPurchases);
                KpiEmployees.Text = dashboard.EmployeeCount.ToString();
                KpiLeads.Text = dashboard.LeadCount.ToString();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Dashboard KPI load failed");
            }
        }

        private void LoadChartData()
        {
            try
            {
                // Sales Trend â€” Last 7 days
                var salesValues = new ChartValues<decimal>();
                var dateLabels = new string[7];

                for (int i = 6; i >= 0; i--)
                {
                    var date = DateTime.Today.AddDays(-i);
                    var report = _reportService.GetDailySales(date);
                    salesValues.Add(report.NetSales);
                    dateLabels[6 - i] = date.ToString("dd/MM");
                }

                SalesSeries = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = "ط§ظ„ظ…ط¨ظٹط¹ط§طھ",
                        Values = salesValues,
                        PointGeometry = DefaultGeometries.Circle,
                        PointGeometrySize = 10,
                        Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
                        Fill = Brushes.Transparent
                    }
                };
                SalesAxisX = dateLabels;

                // Category Distribution â€” Last 30 days
                var categoryData = _operationalReportService.GetTopCategoryQuantities(DateTime.Today.AddDays(-30), 5);
                var categorySeries = new SeriesCollection();
                var colors = new[] { "#10B981", "#3B82F6", "#F59E0B", "#8B5CF6", "#EF4444" };
                int colorIndex = 0;

                foreach (var cat in categoryData)
                {
                    var color = (Color)ColorConverter.ConvertFromString(colors[colorIndex % colors.Length]);
                    categorySeries.Add(new PieSeries
                    {
                        Title = cat.CategoryName,
                        Values = new ChartValues<int> { cat.TotalQuantity },
                        DataLabels = true,
                        Fill = new SolidColorBrush(color)
                    });
                    colorIndex++;
                }

                CategorySeries = categorySeries;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Dashboard chart load failed");
            }
        }

        private void LoadRecentInvoices()
        {
            try
            {
                // âœ… Null safety check
                if (_recentInvoices == null)
                    _recentInvoices = new ObservableCollection<RecentInvoiceItem>();

                var invoices = _operationalReportService.GetRecentInvoices(5);
                _recentInvoices.Clear();
                foreach (var inv in invoices)
                {
                    _recentInvoices.Add(new RecentInvoiceItem
                    {
                        InvoiceNumber = inv.InvoiceNumber,
                        CustomerName = inv.CustomerName,
                        Total = inv.Total,
                        Status = inv.Status,
                        Date = inv.Date
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Dashboard recent invoices load failed");
            }
        }

        public void RefreshData()
        {
            LoadDashboardData();
            LoadChartData();
            LoadRecentInvoices();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }

        private void DateRange_Click(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }

        private void ViewAllInvoices_Click(object sender, RoutedEventArgs e)
        {
            NavService.Instance.OpenTab("sales.report", "طھظ‚ط±ظٹط± ط§ظ„ظ…ط¨ظٹط¹ط§طھ",
                () => new WindowHostControl(new SalesReportWindow()));
        }

        private void QuickAccess_Employees_Click(object sender, RoutedEventArgs e) => NavService.Instance.OpenTab("hr.employees", "ط§ظ„ظ…ظˆط¸ظپظٹظ†", () => new WindowHostControl(new EmployeesWindow()));
        private void QuickAccess_Attendance_Click(object sender, RoutedEventArgs e) => NavService.Instance.OpenTab("hr.attendance", "ط§ظ„ط­ط¶ظˆط± ظˆط§ظ„ط§ظ†طµط±ط§ظپ", () => new WindowHostControl(new AttendanceWindow()));
        private void QuickAccess_Payroll_Click(object sender, RoutedEventArgs e) => NavService.Instance.OpenTab("hr.payroll", "ط§ظ„ط±ظˆط§طھط¨", () => new WindowHostControl(new PayrollWindow()));
        private void QuickAccess_Leads_Click(object sender, RoutedEventArgs e) => NavService.Instance.OpenTab("crm.leads", "ط§ظ„ط¹ظ…ظ„ط§ط، ط§ظ„ظ…ط­طھظ…ظ„ظٹظ†", () => new WindowHostControl(new LeadsWindow()));
        private void QuickAccess_Pipeline_Click(object sender, RoutedEventArgs e) => NavService.Instance.OpenTab("crm.pipeline", "ظ…ط±ط§ط­ظ„ ط§ظ„ط¨ظٹط¹", () => new WindowHostControl(new PipelineWindow()));

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class RecentInvoiceItem : INotifyPropertyChanged
    {
        public string InvoiceNumber { get; set; }
        public string CustomerName { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; }
        public DateTime Date { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
