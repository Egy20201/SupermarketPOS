using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Pages;
using SupermarketPOS.UI.Controls;
using SupermarketPOS.UI.Controls.Dynamic;
using SupermarketPOS.UI.Services;
using SupermarketPOS.UI.Windows;
using SupermarketPOS.Business;
using SupermarketPOS.Business.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NavService = SupermarketPOS.UI.Services.NavigationService;

namespace SupermarketPOS.UI
{
    public partial class MainWindow : Window, INavigationHost
    {
        private const string DashboardTabKey = "dashboard";
        private List<Expander> _allExpanders;
        private bool _isSidebarExpanded = true;
        private readonly Dictionary<string, TabItem> _tabsByKey = new Dictionary<string, TabItem>();
        private readonly Dictionary<string, Button> _sidebarButtonsByKey = new Dictionary<string, Button>();
        private string _activeTabKey;

        private readonly SalesService _salesService;
        private readonly AuthenticationService _authService;
        private readonly ICurrentUserService _currentUserService;
        private readonly SystemHealthService _healthService;
        private readonly INotificationService _notificationService;
        private DispatcherTimer _healthTimer;
        private DateTime _lastQueueAlert = DateTime.MinValue;

        public bool IsSidebarCollapsed => !_isSidebarExpanded;

        public MainWindow(SalesService salesService, AuthenticationService authService, ICurrentUserService currentUserService)
        {
            InitializeComponent();

            _salesService = salesService ?? throw new ArgumentNullException(nameof(salesService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _healthService = DependencyInjection.GetRequiredService<SystemHealthService>();
            _notificationService = DependencyInjection.GetRequiredService<INotificationService>();

            _allExpanders = new List<Expander>
            {
                SalesExpander,
                InventoryExpander,
                PurchasesExpander,
                AccountingExpander,
                ReportsExpander,
                SettingsExpander,
                DynamicEntitiesExpander
            };

            NavService.Instance.Initialize(this);
            _sidebarButtonsByKey[DashboardTabKey] = DashboardButton;
            OpenDashboardTab();
            DateTimeText.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            if (App.CurrentUser != null)
            {
                SidebarUserNameText.Text = App.CurrentUser.FullName;
                SidebarUserRoleText.Text = App.CurrentUser.Role;
            }

            PopulateDynamicEntitiesMenu();
            StartHealthMonitor();
        }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _isSidebarExpanded = !_isSidebarExpanded;

            double targetWidth = _isSidebarExpanded ? 260 : 72;

            DoubleAnimation animation = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            SidebarBorder.BeginAnimation(Border.WidthProperty, animation);

            if (!_isSidebarExpanded)
            {
                foreach (var exp in _allExpanders)
                {
                    exp.IsExpanded = false;
                }
            }
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            if (_isSidebarExpanded)
            {
                Expander current = sender as Expander;
                foreach (var exp in _allExpanders)
                {
                    if (exp != current && exp.IsExpanded)
                        exp.IsExpanded = false;
                }
            }
        }

        public void OpenTab(string key, string title, Func<UserControl> contentFactory)
        {
            if (string.IsNullOrWhiteSpace(key) || contentFactory == null) return;

            if (_tabsByKey.TryGetValue(key, out var existingTab))
            {
                MainTabControl.SelectedItem = existingTab;
                PageTitleText.Text = title;
                SetActiveTabKey(key);
                return;
            }

            var content = contentFactory();
            var tab = new TabItem
            {
                Name = BuildSafeTabName(key),
                Content = content,
                Header = CreateTabHeader(title, key),
                Tag = key
            };

            _tabsByKey[key] = tab;
            MainTabControl.Items.Add(tab);
            MainTabControl.SelectedItem = tab;
            PageTitleText.Text = title;
            SetActiveTabKey(key);
        }

        public void CloseTab(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !_tabsByKey.TryGetValue(key, out var tab)) return;

            MainTabControl.Items.Remove(tab);
            _tabsByKey.Remove(key);

            if (!MainTabControl.Items.Cast<TabItem>().Any())
            {
                OpenDashboardTab();
                return;
            }

            var selected = MainTabControl.SelectedItem as TabItem;
            PageTitleText.Text = GetTabTitle(selected) ?? "ظ„ظˆط­ط© ط§ظ„طھط­ظƒظ…";
            SetActiveTabKey(selected?.Tag as string ?? DashboardTabKey);
        }

        private static string BuildSafeTabName(string key) => "Tab_" + key.Replace(".", "_").Replace("-", "_");

        private object CreateTabHeader(string title, string key)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 0, 6, 0) });

            if (key != DashboardTabKey)
            {
                var closeButton = new Button
                {
                    Content = "âœ•",
                    Width = 18,
                    Height = 18,
                    Padding = new Thickness(0),
                    Tag = key,
                    BorderThickness = new Thickness(0),
                    Background = System.Windows.Media.Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                closeButton.Click += CloseTabButton_Click;
                panel.Children.Add(closeButton);
            }

            return panel;
        }

        private static string GetTabTitle(TabItem tab)
        {
            if (tab?.Header is StackPanel panel && panel.Children.Count > 0 && panel.Children[0] is TextBlock text)
            {
                return text.Text;
            }
            return tab?.Header as string;
        }

        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string key)
            {
                NavService.Instance.CloseTab(key);
            }
        }

        private UserControl CreatePageHost(Page page)
        {
            var frame = new Frame { Content = page };
            return new UserControl { Content = frame };
        }

        private void OpenDashboardTab()
        {
            NavService.Instance.OpenTab(DashboardTabKey, "ظ„ظˆط­ط© ط§ظ„طھط­ظƒظ…", () => CreatePageHost(new DashboardPage()));
        }

        private void OpenWindowTabFromSidebar(Func<Window> windowFactory, object sender, string key, string title)
        {
            TrackSidebarButton(sender, key);
            NavService.Instance.OpenTab(key, title, () => new WindowHostControl(windowFactory()));
        }

        private void OpenViewTabFromSidebar<T>(object sender, string key, string title) where T : UserControl, new()
        {
            TrackSidebarButton(sender, key);
            NavService.Instance.OpenTab(key, title, () => new T());
        }

        private void TrackSidebarButton(object sender, string key)
        {
            if (sender is Button clickedButton && !string.IsNullOrWhiteSpace(key))
            {
                _sidebarButtonsByKey[key] = clickedButton;
            }
        }

        private void SetActiveTabKey(string key)
        {
            _activeTabKey = key ?? DashboardTabKey;
            RefreshSidebarHighlight();
        }

        private void RefreshSidebarHighlight()
        {
            foreach (var pair in _sidebarButtonsByKey)
            {
                var isActive = string.Equals(pair.Key, _activeTabKey, StringComparison.OrdinalIgnoreCase);
                ApplySidebarButtonState(pair.Value, isActive);
            }
        }

        private void ApplySidebarButtonState(Button button, bool isActive)
        {
            if (button == null) return;

            button.ApplyTemplate();
            var border = button.Template?.FindName("border", button) as Border;
            if (border == null) return;

            if (isActive)
            {
                border.Background = TryFindResource("SidebarHoverBrush") as System.Windows.Media.Brush;
                border.BorderThickness = new Thickness(0, 0, 4, 0);
                border.BorderBrush = TryFindResource("AccentBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.DodgerBlue;
            }
            else
            {
                border.Background = System.Windows.Media.Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                border.BorderBrush = System.Windows.Media.Brushes.Transparent;
            }
        }

        private bool EnsureAdminAccess()
        {
            if (App.CurrentUser?.Role == "Admin") return true;
            MessageBox.Show("ظ‡ط°ط§ ط§ظ„ط¥ط¬ط±ط§ط، ظ…طھط§ط­ ظ„ظ…ط¯ظٹط± ط§ظ„ظ†ط¸ط§ظ… ظپظ‚ط·");
            return false;
        }

        // Navigation Handlers
        private void NavigateToPOS_Click(object sender, RoutedEventArgs e)
        {
            var authz = DependencyInjection.GetRequiredService<AuthorizationService>();
            var settings = DependencyInjection.GetRequiredService<SettingsService>();
            OpenWindowTabFromSidebar(() => new POSWindow(_salesService, authz, settings, _currentUserService), sender, "sales.pos", "ظ†ظ‚ط·ط© ط§ظ„ط¨ظٹط¹");
        }

        private void NavigateToDashboard_Click(object sender, RoutedEventArgs e)
        {
            TrackSidebarButton(DashboardButton, DashboardTabKey);
            OpenDashboardTab();
        }

        private void NavigateToCustomers_Click(object sender, RoutedEventArgs e) => OpenViewTabFromSidebar<Views.CustomersView>(sender, "sales.customers", "ط§ظ„ط¹ظ…ظ„ط§ط،");
        private void NavigateToCustomerStatement_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new CustomerStatementWindow(), sender, "sales.customerStatement", "ظƒط´ظپ ط­ط³ط§ط¨ ط¹ظ…ظٹظ„");
        private void NavigateToCustomerPayment_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new CustomerPaymentWindow(), sender, "sales.customerPayment", "ط³ط¯ط§ط¯ ط¹ظ…ظٹظ„");
        private void NavigateToCustomerBalance_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new CustomerBalanceWindow(), sender, "sales.customerBalance", "ط£ط±طµط¯ط© ط§ظ„ط¹ظ…ظ„ط§ط،");
        private void NavigateToSalesReturn_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => DependencyInjection.GetRequiredService<SalesReturnWindow>(), sender, "sales.return", "ظ…ط±طھط¬ط¹ط§طھ ط§ظ„ظ…ط¨ظٹط¹ط§طھ");
        private void NavigateToSalesReport_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new SalesReportWindow(), sender, "sales.report", "طھظ‚ط±ظٹط± ط§ظ„ظ…ط¨ظٹط¹ط§طھ");

        private void NavigateToProductsView_Click(object sender, RoutedEventArgs e) => OpenViewTabFromSidebar<Views.ProductsView>(sender, "inventory.products", "ط¥ط¯ط§ط±ط© ط§ظ„ظ…ظ†طھط¬ط§طھ");
        private void NavigateToCategoriesView_Click(object sender, RoutedEventArgs e) => OpenViewTabFromSidebar<Views.CategoriesView>(sender, "inventory.categories", "ط§ظ„ظپط¦ط§طھ");
        private void NavigateToUnitsView_Click(object sender, RoutedEventArgs e) => OpenViewTabFromSidebar<Views.UnitsView>(sender, "inventory.units", "ط§ظ„ظˆط­ط¯ط§طھ");
        private void NavigateToBulkEntryView_Click(object sender, RoutedEventArgs e) => OpenViewTabFromSidebar<Views.BulkEntryView>(sender, "inventory.bulkEntry", "ط¥ط¯ط®ط§ظ„ ط¬ظ…ط§ط¹ظٹ");
        private void NavigateToWarehouses_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new InventoryReportWindow(), sender, "inventory.warehouses", "ط§ظ„ظ…ط®ط§ط²ظ†");
        private void NavigateToStockTransfer_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new StockTransferWindow(), sender, "inventory.transfer", "طھط­ظˆظٹظ„ ط¨ظٹظ† ط§ظ„ظ…ط®ط§ط²ظ†");
        private void NavigateToStockTake_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new StockTakeWindow(), sender, "inventory.stockTake", "ط¬ط±ط¯ ط§ظ„ظ…ط®ط²ظˆظ†");
        private void NavigateToStockLoss_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new StockLossWindow(), sender, "inventory.stockLoss", "طھط§ظ„ظپ / ظ‡ط§ظ„ظƒ");
        private void NavigateToStockCard_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new StockCardWindow(), sender, "inventory.stockCard", "ظƒط§ط±طھ ط§ظ„طµظ†ظپ");
        private void NavigateToInventoryReport_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new InventoryReportWindow(), sender, "inventory.report", "طھظ‚ظٹظٹظ… ط§ظ„ظ…ط®ط²ظˆظ†");

        private void NavigateToPurchases_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new PurchaseInvoiceWindow(), sender, "purchase.invoice", "ظپظˆط§طھظٹط± ط§ظ„ط´ط±ط§ط،");
        private void NavigateToSuppliers_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new SupplierBalanceWindow(), sender, "purchase.suppliers", "ط§ظ„ظ…ظˆط±ط¯ظٹظ†");
        private void NavigateToPurchaseReturn_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => DependencyInjection.GetRequiredService<PurchaseReturnWindow>(), sender, "purchase.return", "ظ…ط±طھط¬ط¹ط§طھ ط§ظ„ظ…ط´طھط±ظٹط§طھ");
        private void NavigateToPurchaseReport_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new PurchaseReportWindow(), sender, "purchase.report", "طھظ‚ط±ظٹط± ط§ظ„ظ…ط´طھط±ظٹط§طھ");

        private void NavigateToChartOfAccounts_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new ChartOfAccountsWindow(), sender, "account.chart", "ط´ط¬ط±ط© ط§ظ„ط­ط³ط§ط¨ط§طھ");
        private void NavigateToGeneralLedger_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new GeneralLedgerWindow(), sender, "account.generalLedger", "ط¯ظپطھط± ط§ظ„ط£ط³طھط§ط° ط§ظ„ط¹ط§ظ…");
        private void NavigateToJournal_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new JournalVoucherWindow(), sender, "account.journal", "ظ‚ظٹط¯ ظٹظˆظ…ظٹط©");
        private void NavigateToTrialBalance_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new TrialBalanceWindow(), sender, "account.trial", "ظ…ظٹط²ط§ظ† ط§ظ„ظ…ط±ط§ط¬ط¹ط©");
        private void NavigateToProfitLoss_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new ProfitLossReportWindow(), sender, "account.profitLoss", "ط§ظ„ط£ط±ط¨ط§ط­ ظˆط§ظ„ط®ط³ط§ط¦ط±");
        private void NavigateToBalanceSheet_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new BalanceSheetWindow(), sender, "account.balanceSheet", "ط§ظ„ظ…ظٹط²ط§ظ†ظٹط© ط§ظ„ط¹ظ…ظˆظ…ظٹط©");

        private void NavigateToVATReport_Click(object sender, RoutedEventArgs e) => OpenWindowTabFromSidebar(() => new VATReportWindow(), sender, "reports.vat", "طھظ‚ط±ظٹط± VAT");

        private void NavigateToUsers_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdminAccess()) return;
            OpenWindowTabFromSidebar(() => new UsersWindow(), sender, "settings.users", "ط§ظ„ظ…ط³طھط®ط¯ظ…ظٹظ†");
        }

        private void NavigateToRoleManagement_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdminAccess()) return;
            OpenWindowTabFromSidebar(() => new RoleManagementWindow(), sender, "settings.roles", "ط¥ط¯ط§ط±ط© ط§ظ„طµظ„ط§ط­ظٹط§طھ");
        }

        private void NavigateToBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdminAccess()) return;
            OpenWindowTabFromSidebar(() => new BackupWindow(), sender, "settings.backup", "ظ†ط³ط® ط§ط­طھظٹط§ط·ظٹ");
        }

        private void NavigateToAuditLog_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAdminAccess()) return;
            OpenWindowTabFromSidebar(() => new AuditLogWindow(), sender, "settings.audit", "ط³ط¬ظ„ ط§ظ„طھط¯ظ‚ظٹظ‚");
        }

        // ================================================================
        //  Phase 3: Dynamic Entity Navigation
        // ================================================================

        /// <summary>
        /// Populates the sidebar section with buttons for all
        /// metadata-registered entities. Fully dynamic - add entity in DB, restart, menu updates.
        /// </summary>
        private void PopulateDynamicEntitiesMenu()
        {
            try
            {
                var registry = DependencyInjection.GetRequiredService<MetadataRegistryService>();
                var entities = registry.GetAllEntities();

                if (entities == null || entities.Count == 0)
                {
                    DynamicEntitiesExpander.Visibility = Visibility.Collapsed;
                    return;
                }

                DynamicEntitiesPanel.Children.Clear();

                foreach (var entity in entities)
                {
                    var entityName = entity.Name;
                    var btn = new Button
                    {
                        Content = entity.Name,
                        Height = 36,
                        Background = System.Windows.Media.Brushes.Transparent,
                        Foreground = (Brush)FindResource("SidebarSubTextBrush"),
                        BorderThickness = new Thickness(0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Tag = entityName
                    };
                    btn.Click += NavigateToDynamicEntity_Click;
                    DynamicEntitiesPanel.Children.Add(btn);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[DynamicUI] Failed to populate dynamic entities menu: {ex.Message}");
            }
        }

        private void NavigateToDynamicEntity_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is string entityName)) return;

            var tabKey = $"dynamic.{entityName.ToLowerInvariant()}";
            var title = entityName;

            TrackSidebarButton(sender, tabKey);
            NavService.Instance.OpenTab(tabKey, title, () =>
            {
                var page = new DynamicEntityPage();
                page.Initialize(entityName);
                return page;
            });
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem selectedTab)
            {
                SetActiveTabKey(selectedTab.Tag as string ?? DashboardTabKey);
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            _currentUserService.ClearUser();
            DependencyInjection.GetRequiredService<LoginWindow>().Show();
            Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void StartHealthMonitor()
        {
            _healthTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _healthTimer.Tick += (s, e) => UpdateHealthIndicator();
            _healthTimer.Start();
        }

        private void UpdateHealthIndicator()
        {
            var status = _healthService.Status;

            switch (status)
            {
                case HealthStatus.Healthy:
                    HealthIndicatorText.Text = "\U0001f7e2 سليم";
                    HealthIndicatorText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    break;
                case HealthStatus.Degraded:
                    HealthIndicatorText.Text = "\U0001f7e0 متأخر";
                    HealthIndicatorText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    break;
                case HealthStatus.Unhealthy:
                    HealthIndicatorText.Text = "\U0001f534 متوقف";
                    HealthIndicatorText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    break;
            }

            HealthIndicatorText.ToolTip = _healthService.StatusSummary;

            if (_healthService.QueueSize > 200 && (DateTime.UtcNow - _lastQueueAlert).TotalMinutes >= 2)
            {
                _lastQueueAlert = DateTime.UtcNow;
                _notificationService.Show(
                    $"تحذير: قائمة الانتظار كبيرة ({_healthService.QueueSize} حدث)",
                    NotificationType.Warning, 10);
            }
        }
    }
}
