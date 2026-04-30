using SupermarketPOS.Business.Metadata;
using SupermarketPOS.Core.Metadata;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SupermarketPOS.UI.Controls.Dynamic
{
    /// <summary>
    /// Phase 3 + 3.5 + 3.6: The main dynamic entity page — combines DynamicGrid + DynamicForm.
    /// 
    /// 3.6 Stabilization:
    ///   - BeginInvoke everywhere
    ///   - Dispose ViewModel on Unloaded (cancels all background work)
    ///   - Wire UIExceptionHandler for global error pipeline
    ///   - Non-blocking notification toast bar
    /// </summary>
    public partial class DynamicEntityPage : UserControl
    {
        private DynamicEntityViewModel _viewModel;
        private bool _isInitialized;
        private bool _dataLoaded;

        // Named handlers for cleanup
        private RoutedEventHandler _loadedHandler;
        private EventHandler _formOpenedHandler;
        private EventHandler _formClosedHandler;
        private EventHandler _notificationChangedHandler;
        private RoutedEventHandler _dismissNotificationHandler;

        public DynamicEntityPage()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Initialize this page for a specific entity.
        /// Resolves services from DI, creates the ViewModel, and binds everything.
        /// </summary>
        public void Initialize(string entityName)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            if (string.IsNullOrWhiteSpace(entityName))
                throw new ArgumentException("Entity name is required.", nameof(entityName));

            var dataService = DependencyInjection.GetRequiredService<IGenericDataService>();
            var registry = DependencyInjection.GetRequiredService<MetadataRegistryService>();
            var metadataService = DependencyInjection.GetService<IMetadataService>();
            var fieldPermissions = DependencyInjection.GetService<FieldPermissionService>();
            var currentUserService = DependencyInjection.GetService<ICurrentUserService>();

            _viewModel = new DynamicEntityViewModel(
                entityName, dataService, registry,
                metadataService, fieldPermissions, currentUserService);

            // Bind grid and form
            EntityGrid.Bind(_viewModel);
            EntityForm.Bind(_viewModel);

            // Wire form open/close visibility (BeginInvoke — non-blocking)
            _formOpenedHandler = (s, e) =>
                Dispatcher.BeginInvoke(new Action(() => ShowForm()));
            _viewModel.FormOpened += _formOpenedHandler;

            _formClosedHandler = (s, e) =>
                Dispatcher.BeginInvoke(new Action(() => HideForm()));
            _viewModel.FormClosed += _formClosedHandler;

            // Wire notification toast bar
            _notificationChangedHandler = (s, e) =>
                Dispatcher.BeginInvoke(new Action(() => UpdateNotificationBar()));
            DynamicNotificationService.Instance.NotificationChanged += _notificationChangedHandler;

            _dismissNotificationHandler = (s, e) => DynamicNotificationService.Instance.Clear();
            DismissNotificationBtn.Click += _dismissNotificationHandler;

            // Load data once on first Loaded event
            _loadedHandler = async (s, e) =>
            {
                if (_dataLoaded) return;
                _dataLoaded = true;
                await UIExceptionHandler.RunSafeAsync(
                    () => _viewModel.LoadDataAsync(), $"InitialLoad:{entityName}");
            };
            Loaded += _loadedHandler;
        }

        /// <summary>
        /// Gets the underlying ViewModel for external access.
        /// </summary>
        public DynamicEntityViewModel ViewModel => _viewModel;

        private void ShowForm()
        {
            FormPanel.Visibility = Visibility.Visible;
            EntityForm.RefreshValues();
        }

        private void HideForm()
        {
            FormPanel.Visibility = Visibility.Collapsed;
        }

        // ================================================================
        //  Notification Toast Bar
        // ================================================================

        private void UpdateNotificationBar()
        {
            var notify = DynamicNotificationService.Instance;
            if (!notify.IsVisible)
            {
                NotificationBar.Visibility = Visibility.Collapsed;
                return;
            }

            NotificationText.Text = notify.Message;
            NotificationBar.Visibility = Visibility.Visible;

            // Color by type
            switch (notify.Type)
            {
                case DynamicNotificationType.Success:
                    NotificationBar.Background = new SolidColorBrush(Color.FromRgb(209, 250, 229)); // green-100
                    NotificationBar.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // green-500
                    NotificationBar.BorderThickness = new Thickness(1);
                    NotificationText.Foreground = new SolidColorBrush(Color.FromRgb(6, 78, 59));    // green-900
                    break;
                case DynamicNotificationType.Error:
                    NotificationBar.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // red-100
                    NotificationBar.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));  // red-500
                    NotificationBar.BorderThickness = new Thickness(1);
                    NotificationText.Foreground = new SolidColorBrush(Color.FromRgb(127, 29, 29));  // red-900
                    break;
                case DynamicNotificationType.Warning:
                    NotificationBar.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // amber-100
                    NotificationBar.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // amber-500
                    NotificationBar.BorderThickness = new Thickness(1);
                    NotificationText.Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14));  // amber-900
                    break;
                default: // Info
                    NotificationBar.Background = new SolidColorBrush(Color.FromRgb(219, 234, 254)); // blue-100
                    NotificationBar.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // blue-500
                    NotificationBar.BorderThickness = new Thickness(1);
                    NotificationText.Foreground = new SolidColorBrush(Color.FromRgb(30, 58, 138));  // blue-900
                    break;
            }
        }

        // ================================================================
        //  Cleanup (dispose ViewModel to cancel all background work)
        // ================================================================

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_loadedHandler != null) Loaded -= _loadedHandler;
            if (_dismissNotificationHandler != null) DismissNotificationBtn.Click -= _dismissNotificationHandler;

            // Unsubscribe notification service
            if (_notificationChangedHandler != null)
                DynamicNotificationService.Instance.NotificationChanged -= _notificationChangedHandler;

            if (_viewModel != null)
            {
                if (_formOpenedHandler != null) _viewModel.FormOpened -= _formOpenedHandler;
                if (_formClosedHandler != null) _viewModel.FormClosed -= _formClosedHandler;

                // Dispose ViewModel — cancels master CTS, kills all background tasks
                _viewModel.Dispose();
            }

            Unloaded -= OnUnloaded;
        }
    }
}
