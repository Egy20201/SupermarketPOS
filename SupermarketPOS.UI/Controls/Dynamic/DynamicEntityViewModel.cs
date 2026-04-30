using SupermarketPOS.Business.Metadata;
using SupermarketPOS.Core.Metadata;
using SupermarketPOS.UI.Services;
using SupermarketPOS.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SupermarketPOS.UI.Controls.Dynamic
{
    /// <summary>
    /// Phase 3 Full UI ↔ Engine Integration: Generic ViewModel for ANY metadata-registered entity.
    /// 
    /// Replaces ALL entity-specific ViewModels (ProductsViewModel, CustomersViewModel, etc.).
    /// Fully metadata-driven: loads EntityDefinition, Fields, FormLayout, GridLayout, Actions,
    /// and FieldPermissions from the registry. No hardcoded screens.
    /// 
    /// Features:
    ///   - Dynamic CRUD via IGenericDataService (GetAsync with lookup resolution)
    ///   - Action buttons from ActionDefinitions + ExecuteActionAsync
    ///   - Security: FieldPermissions (read/write) per role — hidden fields not rendered
    ///   - FormLayout JSON → field arrangement
    ///   - GridLayout JSON → column configuration
    ///   - Paging always ON, MaxRows hard cap (1000)
    ///   - Global error pipeline via UIExceptionHandler
    ///   - Non-blocking notifications (no MessageBox)
    /// </summary>
    public sealed class DynamicEntityViewModel : BaseViewModel, IDisposable
    {
        // ================================================================
        //  Configuration
        // ================================================================

        private const int MaxLookupItems = 500;
        public const int MaxRenderedFields = 50;
        private const int MaxRowsCap = 1000;

        // ================================================================
        //  Dependencies
        // ================================================================

        private readonly IGenericDataService _dataService;
        private readonly IMetadataService _metadataService;
        private readonly MetadataRegistryService _registry;
        private readonly FieldPermissionService _fieldPermissions;
        private readonly ICurrentUserService _currentUserService;
        private readonly DynamicNotificationService _notify;

        // Serialization lock for LoadDataAsync
        private int _loadLock;
        private bool _disposed;

        // Master CTS — cancelled on Dispose to kill all background work
        private CancellationTokenSource _masterCts = new CancellationTokenSource();

        // ================================================================
        //  Metadata (loaded once)
        // ================================================================

        public string EntityName { get; }
        public EntityDefinition Entity { get; private set; }
        public IReadOnlyList<FieldDefinition> Fields { get; private set; }
        public IReadOnlyList<FieldDefinition> VisibleFields { get; private set; }
        public IReadOnlyList<FieldDefinition> EditableFields { get; private set; }
        public FormLayoutDefinition FormLayout { get; private set; }
        public GridLayoutDefinition GridLayout { get; private set; }
        public IReadOnlyList<ActionDefinition> Actions { get; private set; }
        public HashSet<string> ReadableFields { get; private set; }
        public HashSet<string> WritableFields { get; private set; }

        // ================================================================
        //  Grid State
        // ================================================================

        private ObservableCollection<Dictionary<string, object>> _items =
            new ObservableCollection<Dictionary<string, object>>();
        public ObservableCollection<Dictionary<string, object>> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        private Dictionary<string, object> _selectedItem;
        public Dictionary<string, object> SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                    OnSelectedItemChanged();
            }
        }

        // ================================================================
        //  Pagination (always enforced, never exceed MaxRowsCap)
        // ================================================================

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        private int _pageSize = 50;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                // Enforce: pageSize never exceeds MaxRowsCap
                var clamped = Math.Min(Math.Max(1, value), MaxRowsCap);
                SetProperty(ref _pageSize, clamped);
            }
        }

        private int _totalItems;
        public int TotalItems
        {
            get => _totalItems;
            set
            {
                if (SetProperty(ref _totalItems, value))
                    OnPropertyChanged(nameof(TotalPages));
            }
        }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize));

        // ================================================================
        //  Sort
        // ================================================================

        private string _sortField = "Id";
        public string SortField
        {
            get => _sortField;
            set => SetProperty(ref _sortField, value);
        }

        private bool _sortDescending;
        public bool SortDescending
        {
            get => _sortDescending;
            set => SetProperty(ref _sortDescending, value);
        }

        // ================================================================
        //  Search
        // ================================================================

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        // ================================================================
        //  Form State
        // ================================================================

        private Dictionary<string, object> _formData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, object> FormData
        {
            get => _formData;
            set => SetProperty(ref _formData, value);
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        private bool _isNewRecord;
        public bool IsNewRecord
        {
            get => _isNewRecord;
            set => SetProperty(ref _isNewRecord, value);
        }

        private int? _editingId;
        public int? EditingId
        {
            get => _editingId;
            set => SetProperty(ref _editingId, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // ================================================================
        //  Dirty State Tracking
        // ================================================================

        private Dictionary<string, object> _originalFormData;

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set => SetProperty(ref _isDirty, value);
        }

        // ================================================================
        //  Confirmation pending state (non-blocking confirmation)
        // ================================================================

        private bool _isConfirmPending;
        public bool IsConfirmPending
        {
            get => _isConfirmPending;
            set => SetProperty(ref _isConfirmPending, value);
        }

        private string _confirmMessage = string.Empty;
        public string ConfirmMessage
        {
            get => _confirmMessage;
            set => SetProperty(ref _confirmMessage, value);
        }

        private TaskCompletionSource<bool> _confirmTcs;

        // ================================================================
        //  Lookup Cache
        // ================================================================

        private readonly Dictionary<string, List<LookupItem>> _lookupCache =
            new Dictionary<string, List<LookupItem>>(StringComparer.OrdinalIgnoreCase);

        // ================================================================
        //  Commands
        // ================================================================

        public ICommand LoadCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand FirstPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand LastPageCommand { get; }
        public ICommand SortCommand { get; }
        public ICommand ConfirmYesCommand { get; }
        public ICommand ConfirmNoCommand { get; }
        public ICommand ExecuteActionCommand { get; }

        // ================================================================
        //  Events
        // ================================================================

        public event EventHandler DataLoaded;
        public event EventHandler FormOpened;
        public event EventHandler FormClosed;

        // ================================================================
        //  Constructor
        // ================================================================

        public DynamicEntityViewModel(
            string entityName,
            IGenericDataService dataService,
            MetadataRegistryService registry,
            IMetadataService metadataService = null,
            FieldPermissionService fieldPermissions = null,
            ICurrentUserService currentUserService = null)
        {
            EntityName = entityName ?? throw new ArgumentNullException(nameof(entityName));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _metadataService = metadataService;
            _fieldPermissions = fieldPermissions;
            _currentUserService = currentUserService;
            _notify = DynamicNotificationService.Instance;

            // Resolve metadata
            Entity = _registry.GetEntity(entityName);
            if (Entity == null)
                throw new ArgumentException($"Entity '{entityName}' not found in metadata registry.");

            Fields = (IReadOnlyList<FieldDefinition>)Entity.Fields;

            // Load FormLayout, GridLayout, Actions from IMetadataService
            if (_metadataService != null)
            {
                FormLayout = _metadataService.GetFormLayout(Entity.Id);
                GridLayout = _metadataService.GetGridLayout(Entity.Id);
                Actions = _metadataService.GetActions(Entity.Id);
            }
            else
            {
                Actions = Array.Empty<ActionDefinition>();
            }

            // Load field-level permissions for current user's role
            LoadFieldPermissions();

            // Apply security: filter visible/editable fields based on permissions
            VisibleFields = Fields
                .Where(f => f.IsVisible && IsFieldReadable(f.Name))
                .Take(MaxRenderedFields)
                .ToList();
            EditableFields = Fields
                .Where(f => f.IsEditable && IsFieldWritable(f.Name))
                .ToList();

            // Wire commands
            LoadCommand = new RelayCommand(() => SafeExecuteAsync(() => LoadDataAsync()));
            SearchCommand = new RelayCommand(() => { CurrentPage = 1; SafeExecuteAsync(() => LoadDataAsync()); });
            NewCommand = new RelayCommand(() => StartNew());
            EditCommand = new RelayCommand(() => StartEdit(), () => SelectedItem != null);
            SaveCommand = new RelayCommand(() => SafeExecuteAsync(SaveAsync));
            DeleteCommand = new RelayCommand(() => SafeExecuteAsync(DeleteAsync), () => SelectedItem != null);
            CancelCommand = new RelayCommand(() => SafeExecuteAsync(CancelEditAsync));
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; SafeExecuteAsync(() => LoadDataAsync()); });
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; SafeExecuteAsync(() => LoadDataAsync()); } });
            NextPageCommand = new RelayCommand(() => { if (CurrentPage < TotalPages) { CurrentPage++; SafeExecuteAsync(() => LoadDataAsync()); } });
            LastPageCommand = new RelayCommand(() => { CurrentPage = TotalPages; SafeExecuteAsync(() => LoadDataAsync()); });
            SortCommand = new RelayCommand<string>(field => { ToggleSort(field); SafeExecuteAsync(() => LoadDataAsync()); });

            // Confirmation inline commands
            ConfirmYesCommand = new RelayCommand(() => ResolveConfirmation(true));
            ConfirmNoCommand = new RelayCommand(() => ResolveConfirmation(false));

            // Action execution command (for metadata-defined actions)
            ExecuteActionCommand = new RelayCommand<string>(actionName =>
                SafeExecuteAsync(() => ExecuteMetadataActionAsync(actionName)));
        }

        // ================================================================
        //  Security: Field Permissions
        // ================================================================

        private void LoadFieldPermissions()
        {
            if (_fieldPermissions == null || _currentUserService == null)
            {
                ReadableFields = null;
                WritableFields = null;
                return;
            }

            var roleId = GetCurrentRoleId();
            ReadableFields = _fieldPermissions.GetReadableFields(Entity.Id, roleId);
            WritableFields = _fieldPermissions.GetWritableFields(Entity.Id, roleId);
        }

        private int? GetCurrentRoleId()
        {
            if (_currentUserService?.CurrentUser == null) return null;
            return _currentUserService.CurrentUser.RoleId;
        }

        public bool IsFieldReadable(string fieldName)
        {
            if (ReadableFields == null) return true;
            return ReadableFields.Contains(fieldName);
        }

        public bool IsFieldWritable(string fieldName)
        {
            if (WritableFields == null) return true;
            return WritableFields.Contains(fieldName);
        }

        // ================================================================
        //  Action Engine (metadata-defined actions)
        // ================================================================

        public async Task ExecuteMetadataActionAsync(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName)) return;

            IsBusy = true;
            StatusMessage = $"جاري تنفيذ {actionName}...";

            try
            {
                var token = _masterCts.Token;

                // Collect form data for the action
                var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                if (IsEditing)
                {
                    foreach (var field in EditableFields)
                    {
                        if (FormData.TryGetValue(field.Name, out var value))
                            data[field.Name] = value;
                    }
                    if (EditingId.HasValue)
                        data["Id"] = EditingId.Value;
                }
                else if (SelectedItem != null)
                {
                    foreach (var kvp in SelectedItem)
                        data[kvp.Key] = kvp.Value;
                }

                var result = await _dataService.ExecuteActionAsync(EntityName, actionName, data)
                    .ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                BeginInvoke(() =>
                {
                    StatusMessage = $"تم تنفيذ {actionName} بنجاح";
                    _notify.ShowSuccess(StatusMessage);
                    CloseForm();
                });
                await LoadDataAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                UIExceptionHandler.Handle(ex, $"Action:{actionName}:{EntityName}");
                BeginInvoke(() => StatusMessage = $"خطأ في تنفيذ {actionName}: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Fire-and-forget wrapper with global error pipeline.
        /// </summary>
        private async void SafeExecuteAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected cancellation — ignore
            }
            catch (Exception ex)
            {
                UIExceptionHandler.Handle(ex, EntityName);
                BeginInvoke(() => StatusMessage = $"خطأ: {ex.Message}");
            }
        }

        // ================================================================
        //  Non-blocking Confirmation (replaces MessageBox)
        // ================================================================

        /// <summary>
        /// Show a non-blocking inline confirmation banner.
        /// Returns true if user confirms, false otherwise.
        /// </summary>
        private Task<bool> RequestConfirmAsync(string message)
        {
            _confirmTcs = new TaskCompletionSource<bool>();
            ConfirmMessage = message;
            IsConfirmPending = true;
            return _confirmTcs.Task;
        }

        private void ResolveConfirmation(bool result)
        {
            IsConfirmPending = false;
            ConfirmMessage = string.Empty;
            _confirmTcs?.TrySetResult(result);
            _confirmTcs = null;
        }

        // ================================================================
        //  Data Loading (with IsBusy guard + CTS)
        // ================================================================

        public async Task LoadDataAsync()
        {
            if (_disposed) return;

            // Prevent concurrent loads
            if (Interlocked.CompareExchange(ref _loadLock, 1, 0) != 0)
                return;

            IsBusy = true;
            StatusMessage = "جاري التحميل...";

            try
            {
                var token = _masterCts.Token;

                // Enforce MaxRowsCap on PageSize
                var effectivePageSize = Math.Min(PageSize, MaxRowsCap);

                // Use GetAsync with QueryOptions for automatic lookup resolution
                var options = new QueryOptions
                {
                    Filters = BuildSearchFieldFilters(),
                    SortField = SortField,
                    SortDescending = SortDescending,
                    Page = CurrentPage,
                    PageSize = effectivePageSize,
                    ResolveLookups = true
                };

                var results = await _dataService.GetAsync(EntityName, options).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                // Apply field-level read permissions — strip non-readable fields
                if (_fieldPermissions != null && _currentUserService != null)
                {
                    _fieldPermissions.FilterReadableRows(
                        Entity.Id, GetCurrentRoleId(), results);
                }

                // Count query for pagination
                var countReq = new QueryRequest
                {
                    Filters = BuildSearchFilters(),
                    SelectFields = new List<string> { "Id" },
                    Limit = MaxRowsCap,
                    Offset = 0
                };
                var allIds = await _dataService.QueryAsync(EntityName, countReq).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                BeginInvoke(() =>
                {
                    Items = new ObservableCollection<Dictionary<string, object>>(results);
                    TotalItems = allIds.Count;
                    StatusMessage = $"تم تحميل {results.Count} من {TotalItems} سجل";
                    DataLoaded?.Invoke(this, EventArgs.Empty);
                });
            }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex)
            {
                UIExceptionHandler.Handle(ex, $"Load:{EntityName}");
                BeginInvoke(() => StatusMessage = $"خطأ: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
                Interlocked.Exchange(ref _loadLock, 0);
            }
        }

        // ================================================================
        //  Lookup Loading (with CancellationToken + performance guard)
        // ================================================================

        public async Task<List<LookupItem>> GetLookupItemsAsync(
            string lookupEntity, string displayField, CancellationToken token = default)
        {
            var cacheKey = $"{lookupEntity}:{displayField}";

            if (_lookupCache.TryGetValue(cacheKey, out var cached))
                return cached;

            // Link to master CTS so dispose cancels everything
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _masterCts.Token))
            {
                var request = new QueryRequest
                {
                    SelectFields = new List<string> { "Id", displayField },
                    Limit = MaxLookupItems,
                    Offset = 0
                };

                var rows = await _dataService.QueryAsync(lookupEntity, request).ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();

                var items = rows.Select(r => new LookupItem
                {
                    Id = Convert.ToInt32(r.ContainsKey("Id") ? r["Id"] : 0),
                    DisplayValue = r.ContainsKey(displayField) ? r[displayField]?.ToString() ?? "" : ""
                }).ToList();

                _lookupCache[cacheKey] = items;
                return items;
            }
        }

        // ================================================================
        //  Form Operations (no MessageBox)
        // ================================================================

        public void StartNew()
        {
            FormData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _originalFormData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            EditingId = null;
            IsNewRecord = true;
            IsEditing = true;
            IsDirty = false;
            FormOpened?.Invoke(this, EventArgs.Empty);
        }

        public void StartEdit()
        {
            if (SelectedItem == null) return;

            FormData = new Dictionary<string, object>(SelectedItem, StringComparer.OrdinalIgnoreCase);
            _originalFormData = new Dictionary<string, object>(SelectedItem, StringComparer.OrdinalIgnoreCase);
            EditingId = FormData.ContainsKey("Id") ? Convert.ToInt32(FormData["Id"]) : (int?)null;
            IsNewRecord = false;
            IsEditing = true;
            IsDirty = false;
            FormOpened?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Cancel editing with non-blocking dirty confirmation.
        /// </summary>
        public async Task CancelEditAsync()
        {
            if (IsDirty)
            {
                var confirmed = await RequestConfirmAsync("يوجد تغييرات غير محفوظة. هل تريد الإغلاق؟");
                if (!confirmed) return;
            }

            CloseForm();
        }

        private void CloseForm()
        {
            IsEditing = false;
            IsNewRecord = false;
            EditingId = null;
            IsDirty = false;
            _originalFormData = null;
            FormData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            FormClosed?.Invoke(this, EventArgs.Empty);
        }

        // ================================================================
        //  Save
        // ================================================================

        public async Task SaveAsync()
        {
            var errors = ValidateForm();
            if (errors.Count > 0)
            {
                StatusMessage = string.Join(" | ", errors);
                _notify.ShowWarning(string.Join("\n", errors));
                return;
            }

            IsBusy = true;
            StatusMessage = "جاري الحفظ...";

            try
            {
                var token = _masterCts.Token;
                var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in EditableFields)
                {
                    if (FormData.TryGetValue(field.Name, out var value))
                        data[field.Name] = value;
                }

                // Apply field-level write permissions
                if (_fieldPermissions != null && _currentUserService != null)
                {
                    data = _fieldPermissions.FilterWritableData(
                        Entity.Id, GetCurrentRoleId(), data);
                }

                if (IsNewRecord)
                {
                    var newId = await _dataService.CreateAsync(EntityName, data).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    BeginInvoke(() =>
                    {
                        StatusMessage = $"تم الإنشاء بنجاح (Id={newId})";
                        _notify.ShowSuccess(StatusMessage);
                    });
                }
                else if (EditingId.HasValue)
                {
                    await _dataService.UpdateAsync(EntityName, EditingId.Value, data).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    BeginInvoke(() =>
                    {
                        StatusMessage = "تم التحديث بنجاح";
                        _notify.ShowSuccess(StatusMessage);
                    });
                }

                BeginInvoke(() => CloseForm());
                await LoadDataAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex)
            {
                UIExceptionHandler.Handle(ex, $"Save:{EntityName}");
                BeginInvoke(() => StatusMessage = $"خطأ في الحفظ: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ================================================================
        //  Delete (non-blocking confirmation)
        // ================================================================

        public async Task DeleteAsync()
        {
            if (SelectedItem == null) return;
            if (!SelectedItem.ContainsKey("Id")) return;

            var id = Convert.ToInt32(SelectedItem["Id"]);

            // Non-blocking confirmation
            var confirmed = await RequestConfirmAsync($"هل تريد حذف هذا السجل (Id={id})؟");
            if (!confirmed) return;

            IsBusy = true;
            StatusMessage = "جاري الحذف...";

            try
            {
                var token = _masterCts.Token;
                await _dataService.DeleteAsync(EntityName, id).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                BeginInvoke(() =>
                {
                    StatusMessage = "تم الحذف بنجاح";
                    _notify.ShowSuccess(StatusMessage);
                    CloseForm();
                });
                await LoadDataAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex)
            {
                UIExceptionHandler.Handle(ex, $"Delete:{EntityName}");
                BeginInvoke(() => StatusMessage = $"خطأ في الحذف: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ================================================================
        //  Validation from Metadata
        // ================================================================

        public List<string> ValidateForm()
        {
            var errors = new List<string>();

            foreach (var field in Fields)
            {
                FormData.TryGetValue(field.Name, out var value);
                var displayName = field.DisplayName ?? field.Name;

                if (field.IsRequired && (value == null || string.IsNullOrWhiteSpace(value.ToString())))
                {
                    errors.Add($"{displayName} مطلوب");
                    continue;
                }

                if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                    continue;

                var strValue = value.ToString();

                if (field.MaxLength.HasValue && strValue.Length > field.MaxLength.Value)
                    errors.Add($"{displayName} يتجاوز الحد الأقصى ({field.MaxLength.Value} حرف)");

                switch (field.DataType?.ToLowerInvariant())
                {
                    case "int":
                        if (!int.TryParse(strValue, out _))
                            errors.Add($"{displayName} يجب أن يكون رقم صحيح");
                        break;
                    case "decimal":
                        if (!decimal.TryParse(strValue, out _))
                            errors.Add($"{displayName} يجب أن يكون رقم");
                        break;
                    case "date":
                    case "datetime":
                        if (!(value is DateTime) && !DateTime.TryParse(strValue, out _))
                            errors.Add($"{displayName} يجب أن يكون تاريخ صحيح");
                        break;
                }
            }

            return errors;
        }

        // ================================================================
        //  Form Data Access Helpers
        // ================================================================

        public object GetFieldValue(string fieldName)
        {
            FormData.TryGetValue(fieldName, out var val);
            return val;
        }

        public void SetFieldValue(string fieldName, object value)
        {
            FormData[fieldName] = value;
            OnPropertyChanged(nameof(FormData));
            UpdateDirtyState();
        }

        // ================================================================
        //  Dirty State
        // ================================================================

        private void UpdateDirtyState()
        {
            if (_originalFormData == null)
            {
                IsDirty = false;
                return;
            }

            foreach (var field in EditableFields)
            {
                FormData.TryGetValue(field.Name, out var current);
                _originalFormData.TryGetValue(field.Name, out var original);

                var currentStr = current?.ToString() ?? "";
                var originalStr = original?.ToString() ?? "";

                if (!string.Equals(currentStr, originalStr, StringComparison.Ordinal))
                {
                    IsDirty = true;
                    return;
                }
            }

            IsDirty = false;
        }

        // ================================================================
        //  Helpers
        // ================================================================

        /// <summary>
        /// Marshal action to UI thread via BeginInvoke (non-blocking).
        /// </summary>
        private void BeginInvoke(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.BeginInvoke(action);
        }

        private void ToggleSort(string fieldName)
        {
            if (SortField == fieldName)
                SortDescending = !SortDescending;
            else
            {
                SortField = fieldName;
                SortDescending = false;
            }
            CurrentPage = 1;
        }

        private void OnSelectedItemChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private List<FilterCondition> BuildSearchFilters()
        {
            var filters = new List<FilterCondition>();
            if (string.IsNullOrWhiteSpace(SearchText)) return filters;

            var stringField = VisibleFields.FirstOrDefault(f =>
                f.DataType?.Equals("string", StringComparison.OrdinalIgnoreCase) == true);

            if (stringField != null)
            {
                filters.Add(new FilterCondition
                {
                    FieldName = stringField.Name,
                    Operator = "contains",
                    Value = SearchText.Trim()
                });
            }

            return filters;
        }

        private List<FieldFilter> BuildSearchFieldFilters()
        {
            var filters = new List<FieldFilter>();
            if (string.IsNullOrWhiteSpace(SearchText)) return filters;

            var stringField = VisibleFields.FirstOrDefault(f =>
                f.DataType?.Equals("string", StringComparison.OrdinalIgnoreCase) == true);

            if (stringField != null)
            {
                filters.Add(new FieldFilter
                {
                    Field = stringField.Name,
                    Operator = "contains",
                    Value = SearchText.Trim()
                });
            }

            return filters;
        }

        // ================================================================
        //  IDisposable — cancel all background work
        // ================================================================

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _masterCts?.Cancel();
            _masterCts?.Dispose();
            _masterCts = null;

            // Resolve any pending confirmation so it doesn't hang
            _confirmTcs?.TrySetResult(false);
            _confirmTcs = null;
        }
    }

    /// <summary>
    /// Represents an item in a lookup ComboBox.
    /// </summary>
    public class LookupItem
    {
        public int Id { get; set; }
        public string DisplayValue { get; set; }
        public override string ToString() => DisplayValue;
    }
}
