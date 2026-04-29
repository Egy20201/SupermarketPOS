using SupermarketPOS.Business;
using SupermarketPOS.UI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SupermarketPOS.UI.ViewModels
{
    public class ProductsViewModel : BaseViewModel
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly UnitService _unitService;
        private readonly DebounceService _debounceService;
        private ObservableCollection<ProductItem> _items;
        private ProductItem _selectedProduct;
        private string _searchText;
        private int? _filterCategoryId;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalItems;
        private int _totalPages;
        private bool _isLoading;
        private bool _isEditMode;
        private string _formName;
        private string _formBarcode;
        private int? _formCategoryId;
        private int? _formUnitId;
        private decimal _formPurchasePrice;
        private decimal _formSellingPrice;
        private int _formReorderLevel;
        private ObservableCollection<CategoryDto> _categories;
        private ObservableCollection<UnitDto> _units;

        public event System.Action DataLoaded;
        public event System.Action LoadingStateChanged;

        public ObservableCollection<ProductItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public ProductItem SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value) && value != null && !_isEditMode)
                    LoadToForm();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _currentPage = 1;
                    _debounceService.Debounce(() => _ = LoadDataAsync());
                }
            }
        }

        public int? FilterCategoryId
        {
            get => _filterCategoryId;
            set
            {
                if (SetProperty(ref _filterCategoryId, value))
                    _ = LoadDataAsync();
            }
        }

        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public int TotalItems
        {
            get => _totalItems;
            set => SetProperty(ref _totalItems, value);
        }

        public int TotalPages
        {
            get => _totalPages;
            set => SetProperty(ref _totalPages, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                    LoadingStateChanged?.Invoke();
            }
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public string FormName
        {
            get => _formName;
            set => SetProperty(ref _formName, value);
        }

        public string FormBarcode
        {
            get => _formBarcode;
            set => SetProperty(ref _formBarcode, value);
        }

        public int? FormCategoryId
        {
            get => _formCategoryId;
            set => SetProperty(ref _formCategoryId, value);
        }

        public int? FormUnitId
        {
            get => _formUnitId;
            set => SetProperty(ref _formUnitId, value);
        }

        public decimal FormPurchasePrice
        {
            get => _formPurchasePrice;
            set => SetProperty(ref _formPurchasePrice, value);
        }

        public decimal FormSellingPrice
        {
            get => _formSellingPrice;
            set => SetProperty(ref _formSellingPrice, value);
        }

        public int FormReorderLevel
        {
            get => _formReorderLevel;
            set => SetProperty(ref _formReorderLevel, value);
        }

        public ObservableCollection<CategoryDto> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        public ObservableCollection<UnitDto> Units
        {
            get => _units;
            set => SetProperty(ref _units, value);
        }

        public ICommand LoadCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand FirstPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand LastPageCommand { get; }

        public ProductsViewModel(ProductService productService, CategoryService categoryService, UnitService unitService)
        {
            _productService = productService;
            _categoryService = categoryService;
            _unitService = unitService;
            _debounceService = new DebounceService(300);
            _items = new ObservableCollection<ProductItem>();
            _categories = new ObservableCollection<CategoryDto>();
            _units = new ObservableCollection<UnitDto>();

            LoadCommand = new RelayCommand(() => _ = LoadDataAsync());
            SaveCommand = new RelayCommand(() => _ = SaveAsync());
            DeleteCommand = new RelayCommand(() => _ = DeleteAsync(), () => SelectedProduct != null);
            AddCommand = new RelayCommand(SetAddMode);
            EditCommand = new RelayCommand(SetEditMode, () => SelectedProduct != null);
            CancelCommand = new RelayCommand(ClearForm);
            ExportCommand = new RelayCommand(ExportData);
            FirstPageCommand = new RelayCommand(() => { _currentPage = 1; _ = LoadDataAsync(); });
            PrevPageCommand = new RelayCommand(() => { if (_currentPage > 1) { _currentPage--; _ = LoadDataAsync(); } });
            NextPageCommand = new RelayCommand(() => { if (_currentPage < TotalPages) { _currentPage++; _ = LoadDataAsync(); } });
            LastPageCommand = new RelayCommand(() => { _currentPage = TotalPages; _ = LoadDataAsync(); });

            LoadLookups();
            _ = LoadDataAsync();
        }

        private void LoadLookups()
        {
            var categories = _categoryService.GetAll().Where(c => c.IsActive).ToList();
            Categories.Clear();
            foreach (var c in categories) Categories.Add(new CategoryDto { Id = c.Id, Name = c.Name, IsActive = c.IsActive });
            var filterCategories = new ObservableCollection<CategoryDto>();
            filterCategories.Add(new CategoryDto { Id = 0, Name = "جميع الفئات" });
            foreach (var c in categories) filterCategories.Add(new CategoryDto { Id = c.Id, Name = c.Name });

            var units = _unitService.GetAll();
            Units.Clear();
            foreach (var u in units) Units.Add(new UnitDto { Id = u.Id, Name = u.Name });
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            await Task.Run(() =>
            {
                var all = _productService.GetProducts(SearchText, FilterCategoryId ?? 0);
                _totalItems = all.Count;
                _totalPages = (_totalItems + _pageSize - 1) / _pageSize;
                var paged = all.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList();

                var newItems = new ObservableCollection<ProductItem>();
                foreach (var p in paged)
                {
                    newItems.Add(new ProductItem
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

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Items.Clear();
                    foreach (var item in newItems) Items.Add(item);
                    DataLoaded?.Invoke();
                    IsLoading = false;
                });
            });
        }

        private void LoadToForm()
        {
            if (SelectedProduct == null) return;
            FormName = SelectedProduct.Name;
            FormBarcode = SelectedProduct.Barcode;
            FormCategoryId = SelectedProduct.CategoryId;
            var unit = Units.FirstOrDefault(u => u.Name == SelectedProduct.Unit);
            FormUnitId = unit?.Id;
            FormPurchasePrice = SelectedProduct.PurchasePrice;
            FormSellingPrice = SelectedProduct.SellingPrice;
            FormReorderLevel = SelectedProduct.ReorderLevel;
            IsEditMode = true;
        }

        private void ClearForm()
        {
            SelectedProduct = null;
            FormName = "";
            FormBarcode = "";
            FormCategoryId = null;
            FormUnitId = null;
            FormPurchasePrice = 0;
            FormSellingPrice = 0;
            FormReorderLevel = 10;
            IsEditMode = false;
        }

        private void SetAddMode()
        {
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("أدخل بيانات المنتج الجديد", NotificationType.Info);
        }

        private void SetEditMode()
        {
            if (SelectedProduct == null)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("اختر منتجاً من القائمة أولاً", NotificationType.Warning);
                return;
            }
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("قم بتعديل البيانات ثم احفظ", NotificationType.Info);
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormName))
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("يرجى إدخال اسم المنتج", NotificationType.Warning);
                return;
            }

            if (FormSellingPrice <= 0)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("يرجى إدخال سعر بيع صحيح", NotificationType.Warning);
                return;
            }

            IsLoading = true;
            await Task.Run(() =>
            {
                var unitName = FormUnitId.HasValue ? Units.FirstOrDefault(u => u.Id == FormUnitId)?.Name ?? "" : "";
                var result = _productService.Save(new ProductSaveRequest
                {
                    ProductId = SelectedProduct?.Id,
                    Name = FormName,
                    Barcode = FormBarcode,
                    CategoryId = FormCategoryId ?? 0,
                    UnitId = FormUnitId,
                    Unit = unitName,
                    PurchasePrice = FormPurchasePrice,
                    SellingPrice = FormSellingPrice,
                    ReorderLevel = FormReorderLevel
                });

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsLoading = false;
                    if (!result.Success)
                    {
                        SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show(result.ErrorMessage, NotificationType.Error);
                        return;
                    }
                    _ = LoadDataAsync();
                    ClearForm();
                    SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("تم حفظ المنتج بنجاح", NotificationType.Success);
                });
            });
        }

        private async Task DeleteAsync()
        {
            if (SelectedProduct == null) return;

            IsLoading = true;
            await Task.Run(() =>
            {
                _productService.Delete(SelectedProduct.Id);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsLoading = false;
                    _ = LoadDataAsync();
                    ClearForm();
                    SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("تم حذف المنتج بنجاح", NotificationType.Success);
                });
            });
        }

        private void ExportData()
        {
            var bytes = _productService.ExportToExcel();
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Products.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllBytes(dialog.FileName, bytes);
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("تم تصدير المنتجات بنجاح", NotificationType.Success);
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

    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }

    public class UnitDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
