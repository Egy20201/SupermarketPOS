using SupermarketPOS.Business;
using SupermarketPOS.UI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SupermarketPOS.UI.ViewModels
{
    public class CategoriesViewModel : BaseViewModel
    {
        private readonly CategoryService _categoryService;
        private readonly DebounceService _debounceService;
        private ObservableCollection<CategoryItem> _items;
        private CategoryItem _selectedCategory;
        private string _searchText;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalItems;
        private int _totalPages;
        private bool _isLoading;
        private bool _isEditMode;
        private string _formName;
        private string _formDescription;
        private bool _formIsActive;

        public event System.Action DataLoaded;
        public event System.Action LoadingStateChanged;

        public ObservableCollection<CategoryItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public CategoryItem SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value) && value != null && !_isEditMode)
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

        public string FormDescription
        {
            get => _formDescription;
            set => SetProperty(ref _formDescription, value);
        }

        public bool FormIsActive
        {
            get => _formIsActive;
            set => SetProperty(ref _formIsActive, value);
        }

        public ICommand LoadCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand FirstPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand LastPageCommand { get; }

        public CategoriesViewModel(CategoryService categoryService)
        {
            _categoryService = categoryService;
            _debounceService = new DebounceService(300);
            _items = new ObservableCollection<CategoryItem>();

            LoadCommand = new RelayCommand(() => _ = LoadDataAsync());
            SaveCommand = new RelayCommand(() => _ = SaveAsync());
            DeleteCommand = new RelayCommand(() => _ = DeleteAsync(), () => SelectedCategory != null);
            AddCommand = new RelayCommand(SetAddMode);
            EditCommand = new RelayCommand(SetEditMode, () => SelectedCategory != null);
            CancelCommand = new RelayCommand(ClearForm);
            FirstPageCommand = new RelayCommand(() => { _currentPage = 1; _ = LoadDataAsync(); });
            PrevPageCommand = new RelayCommand(() => { if (_currentPage > 1) { _currentPage--; _ = LoadDataAsync(); } });
            NextPageCommand = new RelayCommand(() => { if (_currentPage < TotalPages) { _currentPage++; _ = LoadDataAsync(); } });
            LastPageCommand = new RelayCommand(() => { _currentPage = TotalPages; _ = LoadDataAsync(); });

            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            await Task.Run(() =>
            {
                var all = _categoryService.GetAll()
                    .Where(c => string.IsNullOrEmpty(SearchText) || c.Name.Contains(SearchText))
                    .ToList();
                _totalItems = all.Count;
                _totalPages = (_totalItems + _pageSize - 1) / _pageSize;
                var paged = all.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList();

                var newItems = new ObservableCollection<CategoryItem>();
                foreach (var c in paged)
                {
                    newItems.Add(new CategoryItem { Id = c.Id, Name = c.Name, Description = c.Description, IsActive = c.IsActive });
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

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FormName))
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("يرجى إدخال اسم الفئة", NotificationType.Warning);
                return;
            }

            IsLoading = true;
            await Task.Run(() =>
            {
                var result = _categoryService.Save(new CategorySaveRequest
                {
                    CategoryId = SelectedCategory?.Id,
                    Name = FormName,
                    Description = FormDescription,
                    IsActive = FormIsActive
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
                    SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("تم حفظ الفئة بنجاح", NotificationType.Success);
                });
            });
        }

        private async Task DeleteAsync()
        {
            if (SelectedCategory == null) return;

            IsLoading = true;
            await Task.Run(() =>
            {
                var result = _categoryService.Delete(SelectedCategory.Id);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsLoading = false;
                    if (!result)
                    {
                        SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("لا يمكن حذف فئة مرتبطة بمنتجات", NotificationType.Error);
                        return;
                    }
                    _ = LoadDataAsync();
                    ClearForm();
                    SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("تم حذف الفئة بنجاح", NotificationType.Success);
                });
            });
        }

        private void LoadToForm()
        {
            if (SelectedCategory == null) return;
            FormName = SelectedCategory.Name;
            FormDescription = SelectedCategory.Description;
            FormIsActive = SelectedCategory.IsActive;
            IsEditMode = true;
        }

        private void ClearForm()
        {
            SelectedCategory = null;
            FormName = "";
            FormDescription = "";
            FormIsActive = true;
            IsEditMode = false;
        }

        private void SetAddMode()
        {
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("أدخل بيانات الفئة الجديدة", NotificationType.Info);
        }

        private void SetEditMode()
        {
            if (SelectedCategory == null)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("اختر فئة من القائمة أولاً", NotificationType.Warning);
                return;
            }
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("قم بتعديل البيانات ثم احفظ", NotificationType.Info);
        }
    }

    public class CategoryItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public string StatusText => IsActive ? "✅ نشط" : "❌ غير نشط";
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
