using SupermarketPOS.Business;
using SupermarketPOS.UI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SupermarketPOS.UI.ViewModels
{
    [System.Obsolete("Phase 3: Use DynamicEntityViewModel(\"Customer\") instead. This static ViewModel is deprecated.")]
    public class CustomersViewModel : BaseViewModel
    {
        private readonly ICustomerService _customerService;
        private readonly DebounceService _debounceService;
        private ObservableCollection<CustomerItem> _items;
        private CustomerItem _selectedCustomer;
        private string _searchText;
        private int _currentPage = 1;
        private int _pageSize = 50;
        private int _totalItems;
        private int _totalPages;
        private bool _isLoading;
        private bool _isEditMode;
        private string _formName;
        private string _formPhone;
        private string _formEmail;
        private string _formAddress;
        private string _formNotes;
        private decimal _formBalance;

        public event System.Action DataLoaded;
        public event System.Action LoadingStateChanged;

        public ObservableCollection<CustomerItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public CustomerItem SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value) && value != null && !_isEditMode)
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

        public string FormPhone
        {
            get => _formPhone;
            set => SetProperty(ref _formPhone, value);
        }

        public string FormEmail
        {
            get => _formEmail;
            set => SetProperty(ref _formEmail, value);
        }

        public string FormAddress
        {
            get => _formAddress;
            set => SetProperty(ref _formAddress, value);
        }

        public string FormNotes
        {
            get => _formNotes;
            set => SetProperty(ref _formNotes, value);
        }

        public decimal FormBalance
        {
            get => _formBalance;
            set => SetProperty(ref _formBalance, value);
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
        public ICommand ExportCommand { get; }

        public CustomersViewModel(ICustomerService customerService)
        {
            _customerService = customerService;
            _debounceService = new DebounceService(300);
            _items = new ObservableCollection<CustomerItem>();

            LoadCommand = new RelayCommand(() => _ = LoadDataAsync());
            SaveCommand = new RelayCommand(() => _ = SaveAsync());
            DeleteCommand = new RelayCommand(() => _ = DeleteAsync(), () => SelectedCustomer != null);
            AddCommand = new RelayCommand(SetAddMode);
            EditCommand = new RelayCommand(SetEditMode, () => SelectedCustomer != null);
            CancelCommand = new RelayCommand(ClearForm);
            FirstPageCommand = new RelayCommand(() => { _currentPage = 1; _ = LoadDataAsync(); });
            PrevPageCommand = new RelayCommand(() => { if (_currentPage > 1) { _currentPage--; _ = LoadDataAsync(); } });
            NextPageCommand = new RelayCommand(() => { if (_currentPage < TotalPages) { _currentPage++; _ = LoadDataAsync(); } });
            LastPageCommand = new RelayCommand(() => { _currentPage = TotalPages; _ = LoadDataAsync(); });
            ExportCommand = new RelayCommand(ExportData);

            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            await Task.Run(async () =>
            {
                _totalItems = await _customerService.GetTotalCountAsync(_searchText);
                _totalPages = (_totalItems + _pageSize - 1) / _pageSize;
                var all = await _customerService.GetCustomersAsync(_searchText);
                var paged = all.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList();

                var newItems = new ObservableCollection<CustomerItem>();
                foreach (var c in paged)
                {
                    newItems.Add(new CustomerItem
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
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("يرجى إدخال اسم العميل", NotificationType.Warning);
                return;
            }

            IsLoading = true;
            await Task.Run(async () =>
            {
                var request = new CustomerSaveRequest
                {
                    CustomerId = SelectedCustomer?.Id,
                    Name = FormName,
                    Phone = FormPhone,
                    Email = FormEmail,
                    Address = FormAddress,
                    Notes = FormNotes
                };
                var result = await _customerService.SaveAsync(request);

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
                    SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("تم حفظ العميل بنجاح", NotificationType.Success);
                });
            });
        }

        private async Task DeleteAsync()
        {
            if (SelectedCustomer == null) return;

            IsLoading = true;
            await Task.Run(async () =>
            {
                var result = await _customerService.DeleteAsync(SelectedCustomer.Id);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsLoading = false;
                    if (!result)
                    {
                        SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("لا يمكن حذف العميل", NotificationType.Error);
                        return;
                    }
                    _ = LoadDataAsync();
                    ClearForm();
                    SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("تم حذف العميل بنجاح", NotificationType.Success);
                });
            });
        }

        private void LoadToForm()
        {
            if (SelectedCustomer == null) return;
            FormName = SelectedCustomer.Name;
            FormPhone = SelectedCustomer.Phone;
            FormEmail = SelectedCustomer.Email;
            FormAddress = SelectedCustomer.Address;
            FormNotes = SelectedCustomer.Notes;
            FormBalance = SelectedCustomer.Balance;
            IsEditMode = true;
        }

        private void ClearForm()
        {
            SelectedCustomer = null;
            FormName = "";
            FormPhone = "";
            FormEmail = "";
            FormAddress = "";
            FormNotes = "";
            FormBalance = 0;
            IsEditMode = false;
        }

        private void SetAddMode()
        {
            ClearForm();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("أدخل بيانات العميل الجديد", NotificationType.Info);
        }

        private void SetEditMode()
        {
            if (SelectedCustomer == null)
            {
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("اختر عميلاً من القائمة أولاً", NotificationType.Warning);
                return;
            }
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("قم بتعديل البيانات ثم احفظ", NotificationType.Info);
        }

        private void ExportData()
        {
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show($"تم تصدير {Items.Count} عميل", NotificationType.Success);
        }
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


