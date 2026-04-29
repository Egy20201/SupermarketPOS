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
    public partial class CategoriesView : BasePage
    {
        private readonly CategoryService _categoryService;
        private ObservableCollection<CategoryItem> _items;
        private CategoryItem _selectedCategory;
        private bool _isEditMode;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalItems;
        private int _totalPages;
        private string _searchText = "";

        public CategoriesView()
        {
            InitializeComponent();
            PageTitle = "ط§ظ„ظپط¦ط§طھ";
            _categoryService = SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<CategoryService>();
            _items = new ObservableCollection<CategoryItem>();

            SetupListView();
            LoadData();
            ClearForm();
        }

        private void SetupListView()
        {
            CategoriesList.SetColumns(
                new DataGridTextColumn { Header = "ط§ظ„ط§ط³ظ…", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
                new DataGridTextColumn { Header = "ط§ظ„ظˆطµظپ", Binding = new Binding("Description"), Width = 250 },
                new DataGridTextColumn { Header = "ط§ظ„ط­ط§ظ„ط©", Binding = new Binding("StatusText"), Width = 100 }
            );

            CategoriesList.Search.TextChanged += (s, e) => { _searchText = CategoriesList.Search.Text; LoadData(); };
            CategoriesList.Create.Click += (s, e) => SetAddMode();
            CategoriesList.Filter.Click += (s, e) => LoadData();
            CategoriesList.FirstPage.Click += (s, e) => { _currentPage = 1; LoadData(); };
            CategoriesList.PrevPage.Click += (s, e) => { if (_currentPage > 1) { _currentPage--; LoadData(); } };
            CategoriesList.NextPage.Click += (s, e) => { _currentPage++; LoadData(); };
            CategoriesList.LastPage.Click += (s, e) => { _currentPage = (_totalItems + _pageSize - 1) / _pageSize; LoadData(); };
            CategoriesList.Grid.SelectionChanged += CategoriesGrid_SelectionChanged;
        }

        private void LoadData()
        {
            var all = _categoryService.GetAll()
                .Where(c => string.IsNullOrEmpty(_searchText) || c.Name.Contains(_searchText))
                .ToList();
            _totalItems = all.Count;
            _totalPages = (_totalItems + _pageSize - 1) / _pageSize;
            var paged = all.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList();

            _items.Clear();
            foreach (var c in paged)
            {
                _items.Add(new CategoryItem { Id = c.Id, Name = c.Name, Description = c.Description, IsActive = c.IsActive });
            }

            CategoriesList.SetItemsSource(_items);
            CategoriesList.SetPaginationInfo(_currentPage, _totalPages, _totalItems);
        }

        private void CategoriesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isEditMode && CategoriesList.GetSelectedItem() is CategoryItem selected)
            {
                _selectedCategory = selected;
                LoadToForm();
            }
        }

        private void LoadToForm()
        {
            if (_selectedCategory == null) return;
            NameBox.Text = _selectedCategory.Name;
            DescriptionBox.Text = _selectedCategory.Description;
            ActiveBox.IsChecked = _selectedCategory.IsActive;
        }

        private void ClearForm()
        {
            _isEditMode = false;
            _selectedCategory = null;
            NameBox.Text = "";
            DescriptionBox.Text = "";
            ActiveBox.IsChecked = true;
            CategoriesList.ClearSelection();
        }

        private void SetAddMode()
        {
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ط£ط¯ط®ظ„ ط¨ظٹط§ظ†ط§طھ ط§ظ„ظپط¦ط© ط§ظ„ط¬ط¯ظٹط¯ط©", NotificationType.Info);
        }

        private void SetEditMode()
        {
            if (_selectedCategory == null)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ط§ط®طھط± ظپط¦ط© ظ…ظ† ط§ظ„ظ‚ط§ط¦ظ…ط© ط£ظˆظ„ط§ظ‹", NotificationType.Warning);
                return;
            }
            _isEditMode = true;
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ظ‚ظ… ط¨طھط¹ط¯ظٹظ„ ط§ظ„ط¨ظٹط§ظ†ط§طھ ط«ظ… ط§ط­ظپط¸", NotificationType.Info);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ظٹط±ط¬ظ‰ ط¥ط¯ط®ط§ظ„ ط§ط³ظ… ط§ظ„ظپط¦ط©", NotificationType.Warning);
                return;
            }

            var result = _categoryService.Save(new CategorySaveRequest
            {
                CategoryId = _selectedCategory?.Id,
                Name = NameBox.Text,
                Description = DescriptionBox.Text,
                IsActive = ActiveBox.IsChecked == true
            });

            if (!result.Success)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show(result.ErrorMessage, NotificationType.Error);
                return;
            }

            LoadData();
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("طھظ… ط­ظپط¸ ط§ظ„ظپط¦ط© ط¨ظ†ط¬ط§ط­", NotificationType.Success);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCategory == null)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ط§ط®طھط± ظپط¦ط© ظ…ظ† ط§ظ„ظ‚ط§ط¦ظ…ط© ط£ظˆظ„ط§ظ‹", NotificationType.Warning);
                return;
            }

            var result = _categoryService.Delete(_selectedCategory.Id);
            if (!result)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("ظ„ط§ ظٹظ…ظƒظ† ط­ط°ظپ ظپط¦ط© ظ…ط±طھط¨ط·ط© ط¨ظ…ظ†طھط¬ط§طھ", NotificationType.Error);
                return;
            }

            LoadData();
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("طھظ… ط­ط°ظپ ط§ظ„ظپط¦ط© ط¨ظ†ط¬ط§ط­", NotificationType.Success);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e) => SetAddMode();
        private void EditButton_Click(object sender, RoutedEventArgs e) => SetEditMode();
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadData();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => ClearForm();
    }

    public class CategoryItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public string StatusText => IsActive ? "âœ… ظ†ط´ط·" : "â‌Œ ط؛ظٹط± ظ†ط´ط·";
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
