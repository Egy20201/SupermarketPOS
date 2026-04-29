using SupermarketPOS.Core.Metadata;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SupermarketPOS.UI.Controls.Dynamic
{
    /// <summary>
    /// Phase 3 + 3.5 + 3.6: Renders a single field control based on FieldDefinition metadata.
    /// 
    /// 3.6 Stabilization:
    ///   - CancellationTokenSource-based debounce (replaces DispatcherTimer)
    ///   - Lookup cancellation: cancel previous, ignore stale responses
    ///   - Safe disposal: cancel all background tasks on Unloaded
    ///   - BeginInvoke everywhere (non-blocking)
    ///   - Global error pipeline via UIExceptionHandler
    /// </summary>
    public partial class DynamicField : UserControl
    {
        private FieldDefinition _field;
        private DynamicEntityViewModel _viewModel;
        private FrameworkElement _inputControl;
        private bool _disposed;

        // CTS-based debounce (replaces DispatcherTimer)
        private CancellationTokenSource _debounceCts;
        private const int DebounceMs = 300;

        // Lookup cancellation
        private CancellationTokenSource _lookupCts;

        // Named handlers for proper unsubscription
        private TextChangedEventHandler _textChangedHandler;
        private RoutedEventHandler _checkChangedHandler;
        private EventHandler<SelectionChangedEventArgs> _dateChangedHandler;
        private SelectionChangedEventHandler _comboChangedHandler;
        private TextCompositionEventHandler _previewTextHandler;

        public DynamicField()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Bind this control to a field definition and its parent ViewModel.
        /// Creates the appropriate WPF control based on metadata.
        /// </summary>
        public void Bind(FieldDefinition field, DynamicEntityViewModel viewModel)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            // Set label
            FieldLabel.Text = field.DisplayName ?? field.Name;

            // Required marker
            if (field.IsRequired)
                RequiredMarker.Visibility = Visibility.Visible;

            // Read-only check
            bool readOnly = !field.IsEditable;

            // Build control
            var controlType = FieldTypeMapper.Resolve(field);
            _inputControl = CreateControl(controlType, readOnly);
            ControlHost.Content = _inputControl;

            // Load initial value
            LoadValue();
        }

        /// <summary>
        /// Reload value from ViewModel into the control.
        /// </summary>
        public void LoadValue()
        {
            if (_field == null || _viewModel == null) return;

            var value = _viewModel.GetFieldValue(_field.Name);
            SetControlValue(value);
        }

        /// <summary>
        /// Push the current control value back to the ViewModel.
        /// </summary>
        public void PushValue()
        {
            if (_field == null || _viewModel == null) return;
            _viewModel.SetFieldValue(_field.Name, GetControlValue());
        }

        /// <summary>
        /// Show a validation error message with red border.
        /// </summary>
        public void ShowError(string message)
        {
            ErrorText.Text = message ?? "";
            ErrorText.Visibility = string.IsNullOrEmpty(message)
                ? Visibility.Collapsed : Visibility.Visible;

            if (_inputControl is Control ctrl)
            {
                if (string.IsNullOrEmpty(message))
                {
                    var brush = TryFindResource("InputBorderBrush") as Brush;
                    ctrl.BorderBrush = brush ?? SystemColors.ControlDarkBrush;
                }
                else
                {
                    ctrl.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    ctrl.BorderThickness = new Thickness(2);
                }
            }
        }

        /// <summary>
        /// Clear any validation error, restore normal border.
        /// </summary>
        public void ClearError()
        {
            ErrorText.Text = "";
            ErrorText.Visibility = Visibility.Collapsed;

            if (_inputControl is Control ctrl)
            {
                var brush = TryFindResource("InputBorderBrush") as Brush;
                ctrl.BorderBrush = brush ?? SystemColors.ControlDarkBrush;
                ctrl.BorderThickness = new Thickness(1);
            }
        }

        /// <summary>
        /// Validate this single field and show/clear error accordingly.
        /// Returns the error message or null if valid.
        /// </summary>
        public string ValidateField()
        {
            if (_field == null || _viewModel == null) return null;

            PushValue();
            var value = _viewModel.GetFieldValue(_field.Name);
            var displayName = _field.DisplayName ?? _field.Name;

            // Required
            if (_field.IsRequired && (value == null || string.IsNullOrWhiteSpace(value.ToString())))
            {
                ShowError($"{displayName} مطلوب");
                return $"{displayName} مطلوب";
            }

            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                ClearError();
                return null;
            }

            var strValue = value.ToString();

            // MaxLength
            if (_field.MaxLength.HasValue && strValue.Length > _field.MaxLength.Value)
            {
                var msg = $"{displayName} يتجاوز الحد الأقصى ({_field.MaxLength.Value} حرف)";
                ShowError(msg);
                return msg;
            }

            // DataType
            string typeError = null;
            switch (_field.DataType?.ToLowerInvariant())
            {
                case "int":
                    if (!int.TryParse(strValue, out _))
                        typeError = $"{displayName} يجب أن يكون رقم صحيح";
                    break;
                case "decimal":
                    if (!decimal.TryParse(strValue, out _))
                        typeError = $"{displayName} يجب أن يكون رقم";
                    break;
                case "date":
                case "datetime":
                    if (!(value is DateTime) && !DateTime.TryParse(strValue, out _))
                        typeError = $"{displayName} يجب أن يكون تاريخ صحيح";
                    break;
            }

            if (typeError != null)
            {
                ShowError(typeError);
                return typeError;
            }

            ClearError();
            return null;
        }

        // ================================================================
        //  Unload / Cleanup  (Phase 3.6: cancel all CTS + background tasks)
        // ================================================================

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _disposed = true;

            // Cancel all pending async operations
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;

            _lookupCts?.Cancel();
            _lookupCts?.Dispose();
            _lookupCts = null;

            // Unsubscribe all named handlers
            if (_inputControl is TextBox tb)
            {
                if (_textChangedHandler != null) tb.TextChanged -= _textChangedHandler;
                if (_previewTextHandler != null) tb.PreviewTextInput -= _previewTextHandler;
            }
            else if (_inputControl is CheckBox cb)
            {
                if (_checkChangedHandler != null)
                {
                    cb.Checked -= _checkChangedHandler;
                    cb.Unchecked -= _checkChangedHandler;
                }
            }
            else if (_inputControl is DatePicker dp)
            {
                if (_dateChangedHandler != null) dp.SelectedDateChanged -= _dateChangedHandler;
            }
            else if (_inputControl is ComboBox combo)
            {
                if (_comboChangedHandler != null) combo.SelectionChanged -= _comboChangedHandler;
            }

            Unloaded -= OnUnloaded;
        }

        // ================================================================
        //  CTS-based Debounce (replaces DispatcherTimer)
        // ================================================================

        private void StartDebounce()
        {
            if (_disposed) return;

            // Cancel previous debounce
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            _ = DebounceAsync(token);
        }

        private async Task DebounceAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(DebounceMs, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested && !_disposed)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_disposed) PushValue();
                    }));
                }
            }
            catch (TaskCanceledException)
            {
                // Expected — new input cancelled previous debounce
            }
        }

        // ================================================================
        //  Control Factory
        // ================================================================

        private FrameworkElement CreateControl(DynamicControlType controlType, bool readOnly)
        {
            switch (controlType)
            {
                case DynamicControlType.TextBox:
                    return CreateTextBox(readOnly);

                case DynamicControlType.MultiLineText:
                    return CreateMultiLineTextBox(readOnly);

                case DynamicControlType.NumericBox:
                case DynamicControlType.DecimalBox:
                    return CreateNumericBox(readOnly);

                case DynamicControlType.CheckBox:
                    return CreateCheckBox(readOnly);

                case DynamicControlType.DatePicker:
                    return CreateDatePicker(readOnly);

                case DynamicControlType.ComboBox:
                    return CreateComboBox(readOnly);

                default:
                    return CreateTextBox(readOnly);
            }
        }

        private TextBox CreateTextBox(bool readOnly)
        {
            var tb = new TextBox
            {
                Height = 36,
                FontSize = 13,
                Padding = new Thickness(8, 6, 8, 6),
                IsReadOnly = readOnly,
                Background = (Brush)FindResource("InputBackgroundBrush"),
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1)
            };

            if (_field.MaxLength.HasValue)
                tb.MaxLength = _field.MaxLength.Value;

            _textChangedHandler = (s, e) => StartDebounce();
            tb.TextChanged += _textChangedHandler;
            return tb;
        }

        private TextBox CreateMultiLineTextBox(bool readOnly)
        {
            var tb = new TextBox
            {
                MinHeight = 80,
                MaxHeight = 200,
                FontSize = 13,
                Padding = new Thickness(8, 6, 8, 6),
                IsReadOnly = readOnly,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = (Brush)FindResource("InputBackgroundBrush"),
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1)
            };

            if (_field.MaxLength.HasValue)
                tb.MaxLength = _field.MaxLength.Value;

            _textChangedHandler = (s, e) => StartDebounce();
            tb.TextChanged += _textChangedHandler;
            return tb;
        }

        private TextBox CreateNumericBox(bool readOnly)
        {
            var tb = new TextBox
            {
                Height = 36,
                FontSize = 13,
                Padding = new Thickness(8, 6, 8, 6),
                IsReadOnly = readOnly,
                Background = (Brush)FindResource("InputBackgroundBrush"),
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };

            _previewTextHandler = (s, e) =>
            {
                var text = tb.Text.Insert(tb.SelectionStart, e.Text);
                var dataType = _field.DataType?.ToLowerInvariant();
                if (dataType == "decimal")
                    e.Handled = !decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out _);
                else
                    e.Handled = !int.TryParse(text, out _) && text != "-";
            };
            tb.PreviewTextInput += _previewTextHandler;

            _textChangedHandler = (s, e) => StartDebounce();
            tb.TextChanged += _textChangedHandler;
            return tb;
        }

        private CheckBox CreateCheckBox(bool readOnly)
        {
            var cb = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = !readOnly,
                Margin = new Thickness(0, 4, 0, 4)
            };

            _checkChangedHandler = (s, e) => PushValue();
            cb.Checked += _checkChangedHandler;
            cb.Unchecked += _checkChangedHandler;
            return cb;
        }

        private DatePicker CreateDatePicker(bool readOnly)
        {
            var dp = new DatePicker
            {
                Height = 36,
                FontSize = 13,
                IsEnabled = !readOnly,
                Background = (Brush)FindResource("InputBackgroundBrush"),
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1)
            };

            _dateChangedHandler = (s, e) => PushValue();
            dp.SelectedDateChanged += _dateChangedHandler;
            return dp;
        }

        private ComboBox CreateComboBox(bool readOnly)
        {
            var cb = new ComboBox
            {
                Height = 36,
                FontSize = 13,
                IsEnabled = !readOnly,
                DisplayMemberPath = "DisplayValue",
                SelectedValuePath = "Id",
                Background = (Brush)FindResource("InputBackgroundBrush"),
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                BorderBrush = (Brush)FindResource("InputBorderBrush"),
                BorderThickness = new Thickness(1)
            };

            // Safe lookup with cancellation
            if (!string.IsNullOrEmpty(_field.LookupEntity))
            {
                var displayField = _field.LookupDisplayField ?? "Name";
                cb.IsEnabled = false;

                // Cancel any previous lookup, start new one
                _lookupCts?.Cancel();
                _lookupCts?.Dispose();
                _lookupCts = new CancellationTokenSource();
                _ = LoadLookupSafeAsync(cb, _field.LookupEntity, displayField, _lookupCts.Token);
            }

            _comboChangedHandler = (s, e) =>
            {
                if (cb.SelectedValue != null)
                    _viewModel.SetFieldValue(_field.Name, cb.SelectedValue);
            };
            cb.SelectionChanged += _comboChangedHandler;

            return cb;
        }

        // ================================================================
        //  Safe Lookup Loading (with cancellation + retry)
        // ================================================================

        private async Task LoadLookupSafeAsync(ComboBox cb, string lookupEntity, string displayField, CancellationToken token)
        {
            const int maxRetries = 1;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (token.IsCancellationRequested || _disposed) return;

                try
                {
                    var items = await _viewModel.GetLookupItemsAsync(lookupEntity, displayField, token)
                        .ConfigureAwait(false);

                    // Ignore stale response
                    if (token.IsCancellationRequested || _disposed) return;

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_disposed) return;
                        cb.ItemsSource = items;
                        cb.IsEnabled = _field.IsEditable;

                        // If we already have a value, select it
                        var currentValue = _viewModel.GetFieldValue(_field.Name);
                        if (currentValue != null && int.TryParse(currentValue.ToString(), out var id))
                            cb.SelectedValue = id;
                    }));
                    return; // success
                }
                catch (OperationCanceledException)
                {
                    return; // Expected cancellation
                }
                catch (Exception ex)
                {
                    if (attempt >= maxRetries)
                    {
                        if (!_disposed)
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (_disposed) return;
                                cb.IsEnabled = false;
                                ShowError($"تعذر تحميل القائمة: {ex.Message}");
                            }));
                        }
                        UIExceptionHandler.Handle(ex, $"Lookup:{lookupEntity}");
                    }
                    else
                    {
                        // Brief pause before retry
                        try { await Task.Delay(500, token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { return; }
                    }
                }
            }
        }

        // ================================================================
        //  Value Get/Set
        // ================================================================

        private void SetControlValue(object value)
        {
            if (_inputControl is TextBox tb)
            {
                tb.Text = value?.ToString() ?? "";
            }
            else if (_inputControl is CheckBox cb)
            {
                if (value is bool b) cb.IsChecked = b;
                else if (value != null && bool.TryParse(value.ToString(), out var parsed)) cb.IsChecked = parsed;
                else cb.IsChecked = false;
            }
            else if (_inputControl is DatePicker dp)
            {
                if (value is DateTime dt) dp.SelectedDate = dt;
                else if (value != null && DateTime.TryParse(value.ToString(), out var parsed)) dp.SelectedDate = parsed;
                else dp.SelectedDate = null;
            }
            else if (_inputControl is ComboBox combo)
            {
                if (value != null && int.TryParse(value.ToString(), out var id))
                    combo.SelectedValue = id;
                else
                    combo.SelectedIndex = -1;
            }
        }

        private object GetControlValue()
        {
            if (_inputControl is TextBox tb)
            {
                var text = tb.Text?.Trim();
                if (string.IsNullOrEmpty(text)) return null;

                switch (_field.DataType?.ToLowerInvariant())
                {
                    case "int":
                        return int.TryParse(text, out var i) ? (object)i : text;
                    case "decimal":
                        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var d) ? (object)d : text;
                    default:
                        return text;
                }
            }
            else if (_inputControl is CheckBox cb)
            {
                return cb.IsChecked ?? false;
            }
            else if (_inputControl is DatePicker dp)
            {
                return dp.SelectedDate;
            }
            else if (_inputControl is ComboBox combo)
            {
                return combo.SelectedValue;
            }

            return null;
        }
    }
}
