using SupermarketPOS.Business;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SupermarketPOS.UI.Views
{
    public partial class UnitsView : UserControl
    {
        private readonly UnitService _unitService;
        private ObservableCollection<UnitDto> _rows;
        private int? _editingId;
        private bool _suppressSelectionLoad;

        public UnitsView()
        {
            InitializeComponent();
            _unitService = SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<UnitService>();
            _rows = new ObservableCollection<UnitDto>();
            UnitsGrid.ItemsSource = _rows;
            LoadData();
            ClearForm();
        }

        private void LoadData()
        {
            var keepId = _editingId;
            _suppressSelectionLoad = true;
            var allUnits = _unitService.GetAll();
            _rows.Clear();
            foreach (var unit in allUnits)
            {
                _rows.Add(unit);
            }
            _suppressSelectionLoad = false;

            if (keepId.HasValue)
            {
                var match = _rows.FirstOrDefault(r => r.Id == keepId.Value);
                if (match != null)
                {
                    UnitsGrid.SelectedItem = match;
                    UnitsGrid.ScrollIntoView(match);
                }
            }
        }

        private void UnitsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionLoad) return;
            var selected = UnitsGrid.SelectedItem as UnitDto;
            if (selected == null)
            {
                ClearForm();
                return;
            }
            _editingId = selected.Id;
            NameBox.Text = selected.Name;
            TypeCombo.Text = "";
            FactorBox.Text = "1";
            SetStatus("طھظ… طھط­ظ…ظٹظ„ ط§ظ„ظˆط­ط¯ط© ظ„ظ„طھط¹ط¯ظٹظ„.", false);
        }

        private void ClearForm()
        {
            _editingId = null;
            NameBox.Text = string.Empty;
            TypeCombo.Text = string.Empty;
            FactorBox.Text = "1";
        }

        private void NewButton_Click(object sender, RoutedEventArgs e)
        {
            UnitsGrid.SelectedItem = null;
            ClearForm();
            NameBox.Focus();
            SetStatus("ظˆط­ط¯ط© ط¬ط¯ظٹط¯ط© - ط£ط¯ط®ظ„ ط§ظ„ط¨ظٹط§ظ†ط§طھ ط«ظ… ط§ط­ظپط¸.", false);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            decimal.TryParse(FactorBox.Text, out var factor);

            var result = _unitService.Save(new UnitSaveRequest
            {
                UnitId = _editingId,
                Name = NameBox.Text,
                ConversionFactor = factor
            });

            if (!result.Success)
            {
                SetStatus("âڑ  " + result.ErrorMessage, true);
                return;
            }

            _editingId = result.UnitId;
            LoadData();
            SetStatus("âœ” طھظ… ط§ظ„ط­ظپط¸ ط¨ظ†ط¬ط§ط­.", false);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_editingId.HasValue)
            {
                SetStatus("ط§ط®طھط± ظˆط­ط¯ط© ظ…ظ† ط§ظ„ظ‚ط§ط¦ظ…ط©.", true);
                return;
            }

            var ok = _unitService.Delete(_editingId.Value);
            if (!ok)
            {
                SetStatus("âڑ  طھط¹ط°ظ‘ط± ط§ظ„ط­ط°ظپ (ظ‚ط¯ طھظƒظˆظ† ظ…ط±طھط¨ط·ط© ط¨ظ…ظ†طھط¬ط§طھ).", true);
                return;
            }

            ClearForm();
            LoadData();
            SetStatus("طھظ… ط­ط°ظپ ط§ظ„ظˆط­ط¯ط©.", false);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
            SetStatus("طھظ… ط§ظ„طھط­ط¯ظٹط«.", false);
        }

        private void SetStatus(string text, bool isError)
        {
            StatusText.Text = text;
            StatusText.Foreground = isError
                ? (Brush)FindResource("DangerBrush")
                : (Brush)FindResource("TextSecondaryBrush");
        }
    }
}
