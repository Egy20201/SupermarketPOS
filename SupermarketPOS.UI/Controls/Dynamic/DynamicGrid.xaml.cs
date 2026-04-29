using SupermarketPOS.Core.Metadata;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SupermarketPOS.UI.Controls.Dynamic
{
    /// <summary>
    /// Phase 3 + 3.5 + 3.6: Metadata-driven DataGrid.
    /// 
    /// 3.6 Stabilization:
    ///   - CTS-based search debounce (replaces DispatcherTimer)
    ///   - BeginInvoke everywhere (non-blocking)
    ///   - Full DataGrid virtualization (XAML)
    ///   - Named handlers with Unloaded cleanup
    ///   - Safe disposal: cancel CTS on Unloaded
    /// </summary>
    public partial class DynamicGrid : UserControl
    {
        private DynamicEntityViewModel _viewModel;
        private bool _disposed;

        // CTS-based search debounce
        private CancellationTokenSource _searchDebounceCts;
        private const int SearchDebounceMs = 300;

        // Named handlers for cleanup
        private KeyEventHandler _searchKeyDownHandler;
        private RoutedEventHandler _searchClickHandler;
        private RoutedEventHandler _refreshClickHandler;
        private RoutedEventHandler _createClickHandler;
        private RoutedEventHandler _deleteClickHandler;
        private SelectionChangedEventHandler _selectionChangedHandler;
        private MouseButtonEventHandler _doubleClickHandler;
        private DataGridSortingEventHandler _sortingHandler;
        private RoutedEventHandler _firstPageHandler;
        private RoutedEventHandler _prevPageHandler;
        private RoutedEventHandler _nextPageHandler;
        private RoutedEventHandler _lastPageHandler;
        private EventHandler _dataLoadedHandler;
        private PropertyChangedEventHandler _propertyChangedHandler;
        private TextChangedEventHandler _searchTextChangedHandler;

        public DataGrid Grid => DataGrid;

        public DynamicGrid()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Bind this grid to a DynamicEntityViewModel.
        /// Auto-generates columns from metadata and wires up pagination/search.
        /// </summary>
        public void Bind(DynamicEntityViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            BuildColumns();
            WireEvents();
        }

        /// <summary>
        /// Refresh grid data and pagination info.
        /// </summary>
        public void RefreshView()
        {
            DataGrid.ItemsSource = _viewModel.Items;
            UpdatePaginationInfo();
            StatusText.Text = _viewModel.StatusMessage;
        }

        // ================================================================
        //  Column Generation
        // ================================================================

        private void BuildColumns()
        {
            DataGrid.Columns.Clear();

            DataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Id",
                Binding = new Binding("[Id]"),
                Width = new DataGridLength(60),
                IsReadOnly = true
            });

            foreach (var field in _viewModel.VisibleFields)
            {
                var column = CreateColumn(field);
                DataGrid.Columns.Add(column);
            }
        }

        private DataGridColumn CreateColumn(FieldDefinition field)
        {
            var controlType = FieldTypeMapper.Resolve(field);
            var header = field.DisplayName ?? field.Name;
            var width = FieldTypeMapper.GetDefaultColumnWidth(field);

            switch (controlType)
            {
                case DynamicControlType.CheckBox:
                    return new DataGridCheckBoxColumn
                    {
                        Header = header,
                        Binding = new Binding($"[{field.Name}]"),
                        Width = new DataGridLength(width),
                        IsReadOnly = true
                    };

                default:
                    var col = new DataGridTextColumn
                    {
                        Header = header,
                        Binding = CreateBinding(field),
                        Width = new DataGridLength(width, DataGridLengthUnitType.Auto),
                        IsReadOnly = true
                    };
                    col.MinWidth = 80;
                    return col;
            }
        }

        private Binding CreateBinding(FieldDefinition field)
        {
            var binding = new Binding($"[{field.Name}]");

            switch (field.DataType?.ToLowerInvariant())
            {
                case "decimal":
                    binding.StringFormat = "N2";
                    break;
                case "date":
                case "datetime":
                    binding.StringFormat = "yyyy-MM-dd";
                    break;
            }

            return binding;
        }

        // ================================================================
        //  Events (all named for cleanup)
        // ================================================================

        private void WireEvents()
        {
            // CTS-based search debounce on typing
            _searchTextChangedHandler = (s, e) => StartSearchDebounce();
            SearchBox.TextChanged += _searchTextChangedHandler;

            // Search on Enter key (immediate)
            _searchKeyDownHandler = (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CancelSearchDebounce();
                    _viewModel.SearchText = SearchBox.Text;
                    _viewModel.SearchCommand.Execute(null);
                }
            };
            SearchBox.KeyDown += _searchKeyDownHandler;

            // Search button (immediate)
            _searchClickHandler = (s, e) =>
            {
                CancelSearchDebounce();
                _viewModel.SearchText = SearchBox.Text;
                _viewModel.SearchCommand.Execute(null);
            };
            SearchButton.Click += _searchClickHandler;

            // Refresh
            _refreshClickHandler = (s, e) => _viewModel.LoadCommand.Execute(null);
            RefreshButton.Click += _refreshClickHandler;

            // Create
            _createClickHandler = (s, e) => _viewModel.NewCommand.Execute(null);
            CreateButton.Click += _createClickHandler;

            // Delete
            _deleteClickHandler = (s, e) =>
            {
                if (_viewModel.SelectedItem != null)
                    _viewModel.DeleteCommand.Execute(null);
            };
            DeleteButton.Click += _deleteClickHandler;

            // Row selection
            _selectionChangedHandler = (s, e) =>
            {
                if (DataGrid.SelectedItem is Dictionary<string, object> row)
                    _viewModel.SelectedItem = row;
                else
                    _viewModel.SelectedItem = null;
            };
            DataGrid.SelectionChanged += _selectionChangedHandler;

            // Double-click to edit
            _doubleClickHandler = (s, e) =>
            {
                if (_viewModel.SelectedItem != null)
                    _viewModel.EditCommand.Execute(null);
            };
            DataGrid.MouseDoubleClick += _doubleClickHandler;

            // Column header click for sorting
            _sortingHandler = (s, e) =>
            {
                e.Handled = true;
                var fieldName = GetFieldNameFromHeader(e.Column.Header?.ToString());
                if (fieldName != null)
                    _viewModel.SortCommand.Execute(fieldName);
            };
            DataGrid.Sorting += _sortingHandler;

            // Pagination
            _firstPageHandler = (s, e) => _viewModel.FirstPageCommand.Execute(null);
            FirstPageBtn.Click += _firstPageHandler;
            _prevPageHandler = (s, e) => _viewModel.PrevPageCommand.Execute(null);
            PrevPageBtn.Click += _prevPageHandler;
            _nextPageHandler = (s, e) => _viewModel.NextPageCommand.Execute(null);
            NextPageBtn.Click += _nextPageHandler;
            _lastPageHandler = (s, e) => _viewModel.LastPageCommand.Execute(null);
            LastPageBtn.Click += _lastPageHandler;

            // ViewModel events (BeginInvoke for thread safety — non-blocking)
            _dataLoadedHandler = (s, e) =>
                Dispatcher.BeginInvoke(new Action(() => { if (!_disposed) RefreshView(); }));
            _viewModel.DataLoaded += _dataLoadedHandler;

            _propertyChangedHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(DynamicEntityViewModel.StatusMessage))
                    Dispatcher.BeginInvoke(new Action(() => { if (!_disposed) StatusText.Text = _viewModel.StatusMessage; }));
            };
            _viewModel.PropertyChanged += _propertyChangedHandler;
        }

        private string GetFieldNameFromHeader(string header)
        {
            if (header == "Id") return "Id";

            var field = _viewModel.VisibleFields.FirstOrDefault(f =>
                (f.DisplayName ?? f.Name) == header);
            return field?.Name;
        }

        // ================================================================
        //  CTS-based Search Debounce (replaces DispatcherTimer)
        // ================================================================

        private void StartSearchDebounce()
        {
            if (_disposed) return;

            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;

            _ = SearchDebounceAsync(token);
        }

        private void CancelSearchDebounce()
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = null;
        }

        private async Task SearchDebounceAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(SearchDebounceMs, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested && !_disposed)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_disposed) return;
                        _viewModel.SearchText = SearchBox.Text;
                        _viewModel.SearchCommand.Execute(null);
                    }));
                }
            }
            catch (TaskCanceledException)
            {
                // Expected — new input cancelled previous debounce
            }
        }

        // ================================================================
        //  Pagination Display
        // ================================================================

        private void UpdatePaginationInfo()
        {
            var vm = _viewModel;
            PageInfoText.Text = $"{vm.CurrentPage} / {vm.TotalPages}";
            ItemsCountText.Text = $"إجمالي: {vm.TotalItems} سجل";

            FirstPageBtn.IsEnabled = vm.CurrentPage > 1;
            PrevPageBtn.IsEnabled = vm.CurrentPage > 1;
            NextPageBtn.IsEnabled = vm.CurrentPage < vm.TotalPages;
            LastPageBtn.IsEnabled = vm.CurrentPage < vm.TotalPages;
        }

        // ================================================================
        //  Cleanup
        // ================================================================

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _disposed = true;

            // Cancel CTS
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = null;

            // Unsubscribe search
            if (_searchTextChangedHandler != null) SearchBox.TextChanged -= _searchTextChangedHandler;
            if (_searchKeyDownHandler != null) SearchBox.KeyDown -= _searchKeyDownHandler;
            if (_searchClickHandler != null) SearchButton.Click -= _searchClickHandler;

            // Unsubscribe toolbar buttons
            if (_refreshClickHandler != null) RefreshButton.Click -= _refreshClickHandler;
            if (_createClickHandler != null) CreateButton.Click -= _createClickHandler;
            if (_deleteClickHandler != null) DeleteButton.Click -= _deleteClickHandler;

            // Unsubscribe DataGrid
            if (_selectionChangedHandler != null) DataGrid.SelectionChanged -= _selectionChangedHandler;
            if (_doubleClickHandler != null) DataGrid.MouseDoubleClick -= _doubleClickHandler;
            if (_sortingHandler != null) DataGrid.Sorting -= _sortingHandler;

            // Unsubscribe pagination
            if (_firstPageHandler != null) FirstPageBtn.Click -= _firstPageHandler;
            if (_prevPageHandler != null) PrevPageBtn.Click -= _prevPageHandler;
            if (_nextPageHandler != null) NextPageBtn.Click -= _nextPageHandler;
            if (_lastPageHandler != null) LastPageBtn.Click -= _lastPageHandler;

            // Unsubscribe ViewModel events
            if (_viewModel != null)
            {
                if (_dataLoadedHandler != null) _viewModel.DataLoaded -= _dataLoadedHandler;
                if (_propertyChangedHandler != null) _viewModel.PropertyChanged -= _propertyChangedHandler;
            }

            Unloaded -= OnUnloaded;
        }
    }
}
