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
    public partial class CustomersView : BasePage
    {
        private readonly CustomerService _customerService;
        private ObservableCollection<CustomerItem> _items;
        private CustomerItem _selectedCustomer;
        private bool _isEditMode;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalItems;
        private int _totalPages;
        private string _searchText = "";

        public CustomersView()
        {
            InitializeComponent();
            PageTitle = "العملاء";
            _customerService = SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<CustomerService>();
            _items = new ObservableCollection<CustomerItem>();

            SetupListView();
            LoadData();
            ClearForm();
        }

        private void SetupListView()
        {
            CustomersList.SetColumns(
                new DataGridTextColumn { Header = "الاسم", Binding = new Binding("Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) },
                new DataGridTextColumn { Header = "التليفون", Binding = new Binding("Phone"), Width = 150 },
                new DataGridTextColumn { Header = "البريد", Binding = new Binding("Email"), Width = 200 },
                new DataGridTextColumn { Header = "الرصيد", Binding = new Binding("Balance") { StringFormat = "{0:N2}" }, Width = 120 }
            );

            CustomersList.Search.TextChanged += (s, e) => { _searchText = CustomersList.Search.Text; LoadData(); };
            CustomersList.Create.Click += (s, e) => SetAddMode();
            CustomersList.Filter.Click += (s, e) => LoadData();
            CustomersList.FirstPage.Click += (s, e) => { _currentPage = 1; LoadData(); };
            CustomersList.PrevPage.Click += (s, e) => { if (_currentPage > 1) { _currentPage--; LoadData(); } };
            CustomersList.NextPage.Click += (s, e) => { _currentPage++; LoadData(); };
            CustomersList.LastPage.Click += (s, e) => { _currentPage = (_totalItems + _pageSize - 1) / _pageSize; LoadData(); };
            CustomersList.Grid.SelectionChanged += CustomersGrid_SelectionChanged;
        }

        private async void LoadData()
        {
            LoadingOverlay.Visibility = Visibility.Visible;

            var all = await _customerService.GetCustomersAsync(_searchText);
            _totalItems = all.Count;
            _totalPages = (_totalItems + _pageSize - 1) / _pageSize;
            var paged = all.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList();

            _items.Clear();
            foreach (var c in paged)
            {
                _items.Add(new CustomerItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    Email = c.Email,
                    Address = c.Address,
                    Notes = c.Notes,
                    Balance = c.Balance
                });
            }

            CustomersList.SetItemsSource(_items);
            CustomersList.SetPaginationInfo(_currentPage, _totalPages, _totalItems);
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void CustomersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isEditMode && CustomersList.GetSelectedItem() is CustomerItem selected)
            {
                _selectedCustomer = selected;
                LoadToForm();
            }
        }

        private void LoadToForm()
        {
            if (_selectedCustomer == null) return;
            NameBox.Text = _selectedCustomer.Name;
            PhoneBox.Text = _selectedCustomer.Phone;
            EmailBox.Text = _selectedCustomer.Email;
            AddressBox.Text = _selectedCustomer.Address;
            NotesBox.Text = _selectedCustomer.Notes;
            BalanceBox.Text = _selectedCustomer.Balance.ToString("N2");
        }

        private void ClearForm()
        {
            _isEditMode = false;
            _selectedCustomer = null;
            NameBox.Text = "";
            PhoneBox.Text = "";
            EmailBox.Text = "";
            AddressBox.Text = "";
            NotesBox.Text = "";
            BalanceBox.Text = "";
            CustomersList.ClearSelection();
        }

        private void SetAddMode()
        {
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("أدخل بيانات العميل الجديد", NotificationType.Info);
        }

        private void SetEditMode()
        {
            if (_selectedCustomer == null)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("اختر عميلاً من القائمة أولاً", NotificationType.Warning);
                return;
            }
            _isEditMode = true;
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("قم بتعديل البيانات ثم احفظ", NotificationType.Info);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("يرجى إدخال اسم العميل", NotificationType.Warning);
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;

            var request = new CustomerSaveRequest
            {
                CustomerId = _selectedCustomer?.Id,
                Name = NameBox.Text,
                Phone = PhoneBox.Text,
                Email = EmailBox.Text,
                Address = AddressBox.Text,
                Notes = NotesBox.Text
            };

            var result = await _customerService.SaveAsync(request);

            LoadingOverlay.Visibility = Visibility.Collapsed;

            if (!result.Success)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show(result.ErrorMessage, NotificationType.Error);
                return;
            }

            LoadData();
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("تم حفظ العميل بنجاح", NotificationType.Success);
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCustomer == null)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("اختر عميلاً من القائمة أولاً", NotificationType.Warning);
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            var result = await _customerService.DeleteAsync(_selectedCustomer.Id);
            LoadingOverlay.Visibility = Visibility.Collapsed;

            if (!result)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("لا يمكن حذف العميل", NotificationType.Error);
                return;
            }

            LoadData();
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("تم حذف العميل بنجاح", NotificationType.Success);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e) => SetAddMode();
        private void EditButton_Click(object sender, RoutedEventArgs e) => SetEditMode();
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadData();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => ClearForm();
        private void ExportButton_Click(object sender, RoutedEventArgs e) => ExportHelper.ExportToExcel(CustomersList.Grid, "العملاء");
    }

    public class CustomerItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Notes { get; set; }
        public decimal Balance { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
