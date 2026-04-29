using SupermarketPOS.Business;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SupermarketPOS.UI.Views
{
    public partial class BulkEntryView : UserControl
    {
        private readonly BulkProductService _bulkService;
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly UnitService _unitService;
        private readonly ObservableCollection<BulkEntryRow> _rows;
        private System.Collections.Generic.List<CategoryDto> _categories;
        private System.Collections.Generic.List<UnitDto> _units;

        public BulkEntryView()
        {
            InitializeComponent();
            _bulkService = SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<BulkProductService>();
            _productService = SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<ProductService>();
            _categoryService = SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<CategoryService>();
            _unitService = SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<UnitService>();
            _rows = new ObservableCollection<BulkEntryRow>();
            BulkGrid.ItemsSource = _rows;
            LoadLookups();
            EnsureTrailingNewRow();
        }

        private void LoadLookups()
        {
            _categories = _categoryService.GetAll().Where(c => c.IsActive).ToList();
            _units = _unitService.GetAll();
            CategoryColumn.ItemsSource = _categories;
            UnitColumn.ItemsSource = _units;
        }

        private void EnsureTrailingNewRow()
        {
            if (!_rows.Any() || _rows.Last().HasAnyData)
            {
                _rows.Add(new BulkEntryRow());
            }
        }

        private void TemplateButton_Click(object sender, RoutedEventArgs e)
        {
            var bytes = _bulkService.BuildTemplate();
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Bulk_Product_Template.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllBytes(dialog.FileName, bytes);
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Excel|*.xlsx;*.xls" };
            if (dialog.ShowDialog() != true) return;

            var catLookup = _categories.ToDictionary(c => c.Name, c => c.Id);
            var unitLookup = _units.ToDictionary(u => u.Name, u => u.Id);
            var parsed = _bulkService.ParseExcel(dialog.FileName, catLookup, unitLookup);

            _rows.Clear();
            foreach (var row in parsed.Rows)
            {
                _rows.Add(new BulkEntryRow
                {
                    Name = row.Request.Name,
                    Barcode = row.Request.Barcode,
                    CategoryId = row.Request.CategoryId,
                    UnitId = row.Request.UnitId,
                    PurchasePrice = row.Request.PurchasePrice,
                    SellingPrice = row.Request.SellingPrice,
                    ReorderLevel = row.Request.ReorderLevel,
                    ErrorMessage = string.Join(" | ", row.Errors)
                });
            }
            EnsureTrailingNewRow();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var saveRows = _rows.Where(r => r.HasAnyData).ToList();
            if (!saveRows.Any()) return;

            var requests = saveRows.Select(r => new ProductSaveRequest
            {
                Name = r.Name,
                Barcode = r.Barcode,
                CategoryId = r.CategoryId,
                UnitId = r.UnitId,
                Unit = _units.FirstOrDefault(u => u.Id == r.UnitId)?.Name ?? string.Empty,
                PurchasePrice = r.PurchasePrice,
                SellingPrice = r.SellingPrice,
                ReorderLevel = r.ReorderLevel
            }).ToList();

            var result = _productService.SaveBulk(requests);
            foreach (var row in saveRows) row.ErrorMessage = string.Empty;

            if (!result.Success)
            {
                foreach (var err in result.Rows)
                {
                    if (err.RowNumber > 0 && err.RowNumber <= saveRows.Count)
                    {
                        saveRows[err.RowNumber - 1].ErrorMessage = err.ErrorMessage;
                    }
                }
                BulkGrid.Items.Refresh();
                SetStatus("⚠ توجد صفوف غير صالحة. راجع عمود الأخطاء.", true);
                return;
            }

            _rows.Clear();
            EnsureTrailingNewRow();
            SetStatus("✔ تم حفظ " + saveRows.Count + " منتج بنجاح.", false);
        }

        private void SetStatus(string text, bool isError)
        {
            StatusText.Text = text;
            StatusText.Foreground = isError
                ? (System.Windows.Media.Brush)FindResource("DangerBrush")
                : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        }

        private void BulkGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.InvokeAsync(() => EnsureTrailingNewRow());
        }

        private void BulkGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                var grid = (DataGrid)sender;
                var nextIndex = grid.Items.IndexOf(grid.CurrentItem) + 1;
                if (nextIndex < grid.Items.Count)
                {
                    grid.SelectedIndex = nextIndex;
                    grid.CurrentCell = new DataGridCellInfo(grid.Items[nextIndex], grid.Columns[0]);
                    grid.BeginEdit();
                }
            }
        }

        private void BulkGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var row = e.Row.Item as BulkEntryRow;
            if (row != null && !string.IsNullOrWhiteSpace(row.ErrorMessage))
            {
                e.Row.Background = new SolidColorBrush(Color.FromRgb(84, 36, 36));
            }
            else
            {
                e.Row.ClearValue(BackgroundProperty);
            }
        }
    }

    public class BulkEntryRow
    {
        public string Name { get; set; }
        public string Barcode { get; set; }
        public int CategoryId { get; set; }
        public int? UnitId { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int ReorderLevel { get; set; }
        public string ErrorMessage { get; set; }

        public bool HasAnyData =>
            !string.IsNullOrWhiteSpace(Name) ||
            !string.IsNullOrWhiteSpace(Barcode) ||
            CategoryId > 0 ||
            UnitId.HasValue ||
            PurchasePrice > 0m ||
            SellingPrice > 0m ||
            ReorderLevel > 0;
    }
}
