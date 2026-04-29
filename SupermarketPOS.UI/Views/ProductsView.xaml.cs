using SupermarketPOS.Business;
using SupermarketPOS.UI.Controls;
using SupermarketPOS.UI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SupermarketPOS.UI.Views
{
    public partial class ProductsView : BasePage
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly UnitService _unitService;
        private ObservableCollection<ProductItem> _items;
        private ProductItem _selectedProduct;
        private bool _isEditMode;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalItems;
        private int _totalPages;
        private string _searchText = "";
        private int? _filterCategoryId;

        public ProductsView()
        {
            InitializeComponent();
            PageTitle = "ط§ظ„ظ…ظ†طھط¬ط§طھ";
            _productService = SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<ProductService>();
            _categoryService = SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<CategoryService>();
            _unitService = SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<UnitService>();
            _items = new ObservableCollection<ProductItem>();

            SetupListView();
            LoadLookups();
            LoadData();
            ClearForm();
        }

        private void SetupListView()
        {
            ProductsList.SetColumns(
                new DataGridTextColumn { Header = "ط§ظ„ط§ط³ظ…", Binding = new Binding("Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) },
                new DataGridTextColumn { Header = "ط§ظ„ط¨ط§ط±ظƒظˆط¯", Binding = new Binding("Barcode"), Width = 130 },
                new DataGridTextColumn { Header = "ط§ظ„ظپط¦ط©", Binding = new Binding("CategoryName"), Width = 120 },
                new DataGridTextColumn { Header = "ط³ط¹ط± ط§ظ„ط¨ظٹط¹", Binding = new Binding("SellingPrice") { StringFormat = "{0:N2}" }, Width = 100 },
                new DataGridTextColumn { Header = "ط§ظ„ظ…ط®ط²ظˆظ†", Binding = new Binding("StockQuantity"), Width = 80 }
            );

            ProductsList.Search.TextChanged += (s, e) => { _searchText = ProductsList.Search.Text; LoadData(); };
            ProductsList.Create.Click += (s, e) => SetAddMode();
            ProductsList.Filter.Click += (s, e) => LoadData();
            ProductsList.FirstPage.Click += (s, e) => { _currentPage = 1; LoadData(); };
            ProductsList.PrevPage.Click += (s, e) => { if (_currentPage > 1) { _currentPage--; LoadData(); } };
            ProductsList.NextPage.Click += (s, e) => { _currentPage++; LoadData(); };
            ProductsList.LastPage.Click += (s, e) => { _currentPage = (_totalItems + _pageSize - 1) / _pageSize; LoadData(); };
            ProductsList.Grid.SelectionChanged += ProductsGrid_SelectionChanged;
        }

        private void LoadLookups()
        {
            var categories = _categoryService.GetAll().Where(c => c.IsActive).ToList();
            var filterCategories = categories.ToList();
            filterCategories.Insert(0, new CategoryDto { Id = 0, Name = "ط¬ظ…ظٹط¹ ط§ظ„ظپط¦ط§طھ" });
            CategoryFilterCombo.ItemsSource = filterCategories;
            CategoryFilterCombo.SelectedValuePath = "Id";
            CategoryFilterCombo.SelectedIndex = 0;

            CategoryCombo.ItemsSource = categories;
            CategoryCombo.SelectedValuePath = "Id";

            var units = _unitService.GetAll();
            UnitCombo.ItemsSource = units;
            UnitCombo.SelectedValuePath = "Id";
        }

        private void LoadData()
        {
            LoadingOverlay.Visibility = Visibility.Visible;

            var all = _productService.GetProducts(_searchText, _filterCategoryId ?? 0);
            _totalItems = all.Count;
            _totalPages = (_totalItems + _pageSize - 1) / _pageSize;
            var paged = all.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList();

            _items.Clear();
            foreach (var p in paged)
            {
                _items.Add(new ProductItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    Barcode = p.Barcode,
                    CategoryId = p.CategoryId,
                    CategoryName = p.CategoryName,
                    PurchasePrice = p.PurchasePrice,
                    SellingPrice = p.SellingPrice,
                    Unit = p.Unit,
                    ReorderLevel = p.ReorderLevel,
                    StockQuantity = p.StockQuantity
                });
            }

            ProductsList.SetItemsSource(_items);
            ProductsList.SetPaginationInfo(_currentPage, _totalPages, _totalItems);
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void ProductsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isEditMode && ProductsList.GetSelectedItem() is ProductItem selected)
            {
                _selectedProduct = selected;
                LoadToForm();
            }
        }

        private void LoadToForm()
        {
            if (_selectedProduct == null) return;
            NameBox.Text = _selectedProduct.Name;
            BarcodeBox.Text = _selectedProduct.Barcode;
            CategoryCombo.SelectedValue = _selectedProduct.CategoryId;
            var unit = _unitService.GetAll().FirstOrDefault(u => u.Name == _selectedProduct.Unit);
            if (unit != null) UnitCombo.SelectedValue = unit.Id;
            PurchaseBox.Text = _selectedProduct.PurchasePrice.ToString();
            SellBox.Text = _selectedProduct.SellingPrice.ToString();
            ReorderBox.Text = _selectedProduct.ReorderLevel.ToString();
        }

        private void ClearForm()
        {
            _isEditMode = false;
            _selectedProduct = null;
            NameBox.Text = "";
            BarcodeBox.Text = "";
            CategoryCombo.SelectedIndex = -1;
            UnitCombo.SelectedIndex = -1;
            PurchaseBox.Text = "0";
            SellBox.Text = "";
            ReorderBox.Text = "10";
            ProductsList.ClearSelection();
        }

        private void SetAddMode()
        {
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ط£ط¯ط®ظ„ ط¨ظٹط§ظ†ط§طھ ط§ظ„ظ…ظ†طھط¬ ط§ظ„ط¬ط¯ظٹط¯", NotificationType.Info);
        }

        private void SetEditMode()
        {
            if (_selectedProduct == null)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ط§ط®طھط± ظ…ظ†طھط¬ط§ظ‹ ظ…ظ† ط§ظ„ظ‚ط§ط¦ظ…ط© ط£ظˆظ„ط§ظ‹", NotificationType.Warning);
                return;
            }
            _isEditMode = true;
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ظ‚ظ… ط¨طھط¹ط¯ظٹظ„ ط§ظ„ط¨ظٹط§ظ†ط§طھ ط«ظ… ط§ط­ظپط¸", NotificationType.Info);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ظٹط±ط¬ظ‰ ط¥ط¯ط®ط§ظ„ ط§ط³ظ… ط§ظ„ظ…ظ†طھط¬", NotificationType.Warning);
                return;
            }

            if (!decimal.TryParse(SellBox.Text, out var sellPrice) || sellPrice <= 0)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ظٹط±ط¬ظ‰ ط¥ط¯ط®ط§ظ„ ط³ط¹ط± ط¨ظٹط¹ طµط­ظٹط­", NotificationType.Warning);
                return;
            }

            decimal.TryParse(PurchaseBox.Text, out var purchasePrice);
            int.TryParse(ReorderBox.Text, out var reorderLevel);
            int categoryId = CategoryCombo.SelectedValue as int? ?? 0;
            int? unitId = UnitCombo.SelectedValue as int?;
            var unitName = unitId.HasValue ? _unitService.GetAll().FirstOrDefault(u => u.Id == unitId)?.Name ?? "" : "";

            var result = _productService.Save(new ProductSaveRequest
            {
                ProductId = _selectedProduct?.Id,
                Name = NameBox.Text,
                Barcode = BarcodeBox.Text,
                CategoryId = categoryId,
                UnitId = unitId,
                Unit = unitName,
                PurchasePrice = purchasePrice,
                SellingPrice = sellPrice,
                ReorderLevel = reorderLevel
            });

            if (!result.Success)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show(result.ErrorMessage, NotificationType.Error);
                return;
            }

            LoadData();
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("طھظ… ط­ظپط¸ ط§ظ„ظ…ظ†طھط¬ ط¨ظ†ط¬ط§ط­", NotificationType.Success);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProduct == null)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ط§ط®طھط± ظ…ظ†طھط¬ط§ظ‹ ظ…ظ† ط§ظ„ظ‚ط§ط¦ظ…ط© ط£ظˆظ„ط§ظ‹", NotificationType.Warning);
                return;
            }

            _productService.Delete(_selectedProduct.Id);
            LoadData();
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("طھظ… ط­ط°ظپ ط§ظ„ظ…ظ†طھط¬ ط¨ظ†ط¬ط§ط­", NotificationType.Success);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => ClearForm();
        private void AddButton_Click(object sender, RoutedEventArgs e) => SetAddMode();
        private void EditButton_Click(object sender, RoutedEventArgs e) => SetEditMode();
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadData();
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => LoadData();
        private void CategoryFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _filterCategoryId = CategoryFilterCombo.SelectedValue as int?;
            if (_filterCategoryId == 0) _filterCategoryId = null;
            LoadData();
        }
        private void FilterButton_Click(object sender, RoutedEventArgs e) => LoadData();

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var bytes = _productService.ExportToExcel();
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Products.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllBytes(dialog.FileName, bytes);
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("طھظ… طھطµط¯ظٹط± ط§ظ„ظ…ظ†طھط¬ط§طھ ط¨ظ†ط¬ط§ط­", NotificationType.Success);
            }
        }
    }

    public class ProductItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Barcode { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public string Unit { get; set; }
        public int ReorderLevel { get; set; }
        public int StockQuantity { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
    }

}
