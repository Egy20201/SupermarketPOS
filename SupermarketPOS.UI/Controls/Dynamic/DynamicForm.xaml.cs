using SupermarketPOS.Core.Metadata;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SupermarketPOS.UI.Controls.Dynamic
{
    /// <summary>
    /// Phase 3 + 3.5 + 3.6: Auto-generates a form from EntityDefinition + FieldDefinition[].
    /// 
    /// 3.6 Stabilization:
    ///   - BeginInvoke everywhere
    ///   - Inline confirmation banner (non-blocking, replaces MessageBox)
    ///   - Named handlers with Unloaded cleanup
    ///   - Field-level + summary validation
    /// </summary>
    public partial class DynamicForm : UserControl
    {
        private DynamicEntityViewModel _viewModel;
        private readonly List<DynamicField> _fieldControls = new List<DynamicField>();

        // Named handlers for cleanup
        private RoutedEventHandler _saveClickHandler;
        private RoutedEventHandler _cancelClickHandler;
        private RoutedEventHandler _closeClickHandler;
        private RoutedEventHandler _confirmYesHandler;
        private RoutedEventHandler _confirmNoHandler;
        private PropertyChangedEventHandler _vmPropertyChangedHandler;

        public DynamicForm()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Bind the form to a DynamicEntityViewModel.
        /// Generates field controls from metadata and wires up save/cancel.
        /// </summary>
        public void Bind(DynamicEntityViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            BuildForm();
            WireEvents();
        }

        /// <summary>
        /// Refresh all field values from the ViewModel.
        /// Called when the form opens or the selected record changes.
        /// </summary>
        public void RefreshValues()
        {
            UpdateTitle();
            foreach (var fc in _fieldControls)
            {
                fc.ClearError();
                fc.LoadValue();
            }
            ValidationText.Text = "";
            ConfirmBanner.Visibility = Visibility.Collapsed;
        }

        // ================================================================
        //  Form Building
        // ================================================================

        private void BuildForm()
        {
            FieldsContainer.Children.Clear();
            _fieldControls.Clear();

            var fieldsToShow = _viewModel.Fields
                .Where(f => f.IsVisible)
                .OrderBy(f => f.OrderIndex ?? int.MaxValue)
                .Take(DynamicEntityViewModel.MaxRenderedFields)
                .ToList();

            if (fieldsToShow.Count <= 4)
            {
                foreach (var field in fieldsToShow)
                {
                    var fc = new DynamicField();
                    fc.Bind(field, _viewModel);
                    _fieldControls.Add(fc);
                    FieldsContainer.Children.Add(fc);
                }
            }
            else
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16, GridUnitType.Pixel) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                int row = 0;
                for (int i = 0; i < fieldsToShow.Count; i += 2)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var leftField = new DynamicField();
                    leftField.Bind(fieldsToShow[i], _viewModel);
                    _fieldControls.Add(leftField);
                    Grid.SetRow(leftField, row);
                    Grid.SetColumn(leftField, 0);
                    grid.Children.Add(leftField);

                    if (i + 1 < fieldsToShow.Count)
                    {
                        var rightField = new DynamicField();
                        rightField.Bind(fieldsToShow[i + 1], _viewModel);
                        _fieldControls.Add(rightField);
                        Grid.SetRow(rightField, row);
                        Grid.SetColumn(rightField, 2);
                        grid.Children.Add(rightField);
                    }

                    row++;
                }

                FieldsContainer.Children.Add(grid);
            }
        }

        // ================================================================
        //  Events (named handlers for cleanup)
        // ================================================================

        private void WireEvents()
        {
            _saveClickHandler = async (s, e) =>
            {
                // Push all values first
                foreach (var fc in _fieldControls)
                    fc.PushValue();

                // Per-field validation (shows inline errors)
                bool hasFieldErrors = false;
                foreach (var fc in _fieldControls)
                {
                    var err = fc.ValidateField();
                    if (err != null) hasFieldErrors = true;
                }

                // Also run ViewModel-level validation for summary
                var errors = _viewModel.ValidateForm();
                if (errors.Count > 0 || hasFieldErrors)
                {
                    ValidationText.Text = string.Join("\n", errors);
                    return;
                }

                ValidationText.Text = "";
                await _viewModel.SaveAsync();
            };
            SaveButton.Click += _saveClickHandler;

            _cancelClickHandler = async (s, e) => await _viewModel.CancelEditAsync();
            CancelButton.Click += _cancelClickHandler;

            _closeClickHandler = async (s, e) => await _viewModel.CancelEditAsync();
            CloseFormButton.Click += _closeClickHandler;

            // Inline confirmation banner buttons
            _confirmYesHandler = (s, e) => _viewModel.ConfirmYesCommand.Execute(null);
            ConfirmYesBtn.Click += _confirmYesHandler;

            _confirmNoHandler = (s, e) => _viewModel.ConfirmNoCommand.Execute(null);
            ConfirmNoBtn.Click += _confirmNoHandler;

            // Listen for confirmation state changes
            _vmPropertyChangedHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(DynamicEntityViewModel.IsConfirmPending) ||
                    e.PropertyName == nameof(DynamicEntityViewModel.ConfirmMessage))
                {
                    Dispatcher.BeginInvoke(new Action(() => UpdateConfirmBanner()));
                }
            };
            _viewModel.PropertyChanged += _vmPropertyChangedHandler;
        }

        private void UpdateConfirmBanner()
        {
            if (_viewModel.IsConfirmPending)
            {
                ConfirmText.Text = _viewModel.ConfirmMessage;
                ConfirmBanner.Visibility = Visibility.Visible;
            }
            else
            {
                ConfirmBanner.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateTitle()
        {
            var entityDisplayName = _viewModel.Entity.Name;
            FormTitle.Text = _viewModel.IsNewRecord
                ? $"إنشاء {entityDisplayName} جديد"
                : $"تعديل {entityDisplayName} (Id={_viewModel.EditingId})";
        }

        // ================================================================
        //  Cleanup
        // ================================================================

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_saveClickHandler != null) SaveButton.Click -= _saveClickHandler;
            if (_cancelClickHandler != null) CancelButton.Click -= _cancelClickHandler;
            if (_closeClickHandler != null) CloseFormButton.Click -= _closeClickHandler;
            if (_confirmYesHandler != null) ConfirmYesBtn.Click -= _confirmYesHandler;
            if (_confirmNoHandler != null) ConfirmNoBtn.Click -= _confirmNoHandler;

            if (_viewModel != null && _vmPropertyChangedHandler != null)
                _viewModel.PropertyChanged -= _vmPropertyChangedHandler;

            Unloaded -= OnUnloaded;
        }
    }
}
