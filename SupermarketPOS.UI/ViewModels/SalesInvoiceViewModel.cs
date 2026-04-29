using SupermarketPOS.Business;
using SupermarketPOS.Core.Entities;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SupermarketPOS.UI.ViewModels
{
    public class SalesInvoiceViewModel : BaseViewModel
    {
        private readonly SalesService _salesService;
        private readonly CustomerService _customerService;
        private readonly ProductService _productService;
        private const decimal TAX_RATE = 14m;

        private ObservableCollection<InvoiceItem> _items;
        private ObservableCollection<CustomerDto> _customers;
        private CustomerDto _selectedCustomer;
        private InvoiceItem _selectedItem;
        private string _invoiceNumber;
        private DateTime _invoiceDate;
        private DocumentStatus _status;
        private decimal _subtotal;
        private decimal _discountPercent;
        private decimal _discountAmount;
        private decimal _taxAmount;
        private decimal _netTotal;
        private decimal _paidAmount;
        private decimal _remainingAmount;
        private string _paymentMethod;
        private string _notes;
        private bool _isLoading;
        private bool _isNewInvoice;
        private string _barcodeInput;
        private string _validationMessage;
        private bool _hasValidationError;
        private ContentState _viewState;

        public event Action ItemsChanged;
        public event Action TotalsChanged;
        public event Action LoadingStateChanged;

        public ObservableCollection<InvoiceItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public ObservableCollection<CustomerDto> Customers
        {
            get => _customers;
            set => SetProperty(ref _customers, value);
        }

        public CustomerDto SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value) && value?.Id == 0 && _paidAmount == 0)
                    PaidAmount = NetTotal;
            }
        }

        public InvoiceItem SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value);
        }

        public DateTime InvoiceDate
        {
            get => _invoiceDate;
            set => SetProperty(ref _invoiceDate, value);
        }

        public DocumentStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string StatusText
        {
            get
            {
                if (Status == DocumentStatus.Draft) return "مسودة";
                if (Status == DocumentStatus.Confirmed) return "مؤكد";
                if (Status == DocumentStatus.Posted) return "مرحّل";
                return "جديد";
            }
        }

        public decimal Subtotal
        {
            get => _subtotal;
            set => SetProperty(ref _subtotal, value);
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (SetProperty(ref _discountPercent, value))
                    RecalculateTotals();
            }
        }

        public decimal DiscountAmount
        {
            get => _discountAmount;
            set => SetProperty(ref _discountAmount, value);
        }

        public decimal TaxAmount
        {
            get => _taxAmount;
            set => SetProperty(ref _taxAmount, value);
        }

        public decimal NetTotal
        {
            get => _netTotal;
            set
            {
                if (SetProperty(ref _netTotal, value))
                {
                    OnPropertyChanged(nameof(NetTotalDisplay));
                    if (SelectedCustomer?.Id == 0 && !_isLoading && _paidAmount == 0)
                        PaidAmount = value;
                }
            }
        }

        public decimal PaidAmount
        {
            get => _paidAmount;
            set
            {
                if (SetProperty(ref _paidAmount, value))
                {
                    RemainingAmount = NetTotal - value;
                    OnPropertyChanged(nameof(IsFullyPaid));
                    OnPropertyChanged(nameof(RemainingDisplay));
                }
            }
        }

        public decimal RemainingAmount
        {
            get => _remainingAmount;
            set => SetProperty(ref _remainingAmount, value);
        }

        public bool IsFullyPaid => RemainingAmount <= 0.01m;

        public string PaymentMethod
        {
            get => _paymentMethod;
            set => SetProperty(ref _paymentMethod, value);
        }

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
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

        public bool IsNewInvoice
        {
            get => _isNewInvoice;
            set => SetProperty(ref _isNewInvoice, value);
        }

        public string BarcodeInput
        {
            get => _barcodeInput;
            set => SetProperty(ref _barcodeInput, value);
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            set => SetProperty(ref _validationMessage, value);
        }

        public bool HasValidationError
        {
            get => _hasValidationError;
            set => SetProperty(ref _hasValidationError, value);
        }

        public ContentState ViewState
        {
            get => _viewState;
            set => SetProperty(ref _viewState, value);
        }

        public bool CanSave => Items.Any(i => i.IsValid) && NetTotal > 0;
        public bool HasItems => Items.Any(i => i.IsValid);
        public bool IsCashCustomer => SelectedCustomer?.Id == 0;
        public string SubtotalDisplay => $"{Subtotal:N2} ج.م";
        public string DiscountDisplay => $"- {DiscountAmount:N2} ج.م";
        public string TaxDisplay => $"{TaxAmount:N2} ج.م";
        public string NetTotalDisplay => $"{NetTotal:N2} ج.م";
        public string RemainingDisplay => $"{RemainingAmount:N2} ج.م";

        public ObservableCollection<string> PaymentMethods { get; } = new ObservableCollection<string>
        {
            "نقدي", "محفظة (Vodafone Cash)", "تحويل (InstaPay)", "بطاقة ائتمان"
        };

        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand RemoveItemCommand { get; }

        public SalesInvoiceViewModel(SalesService salesService, CustomerService customerService, ProductService productService)
        {
            _salesService = salesService;
            _customerService = customerService;
            _productService = productService;
            _items = new ObservableCollection<InvoiceItem>();
            _customers = new ObservableCollection<CustomerDto>();
            _invoiceDate = DateTime.Today;
            _status = DocumentStatus.Draft;
            _paymentMethod = "نقدي";
            _isNewInvoice = true;

            NewCommand = new RelayCommand(NewInvoice);
            SaveCommand = new RelayCommand(() => _ = SaveInvoiceAsync(), () => CanSave && !IsLoading);
            PrintCommand = new RelayCommand(PrintInvoice);
            ClearCommand = new RelayCommand(ClearInvoice);
            AddProductCommand = new RelayCommand(() => _ = AddProductAsync());
            RemoveItemCommand = new RelayCommand<InvoiceItem>(RemoveItem);

            LoadCustomers();
            NewInvoice();
        }

        private async void LoadCustomers()
        {
            ViewState = ContentState.Loading;
            var customers = await Task.Run(() => _customerService.GetCustomers());
            Customers.Clear();
            Customers.Add(new CustomerDto { Id = 0, Name = "عميل نقدي" });
            foreach (var c in customers)
            {
                Customers.Add(new CustomerDto { Id = c.Id, Name = c.Name });
            }
            SelectedCustomer = Customers.FirstOrDefault();
            ViewState = ContentState.Content;
        }

        public void NewInvoice()
        {
            Items.Clear();
            AddItem(new InvoiceItem { IsNewRow = true });
            InvoiceNumber = GenerateInvoiceNumber();
            InvoiceDate = DateTime.Today;
            Status = DocumentStatus.Draft;
            Subtotal = 0;
            DiscountPercent = 0;
            DiscountAmount = 0;
            TaxAmount = 0;
            NetTotal = 0;
            PaidAmount = 0;
            RemainingAmount = 0;
            Notes = "";
            SelectedCustomer = Customers.FirstOrDefault();
            IsNewInvoice = true;
            HasValidationError = false;
            ValidationMessage = "";
            BarcodeInput = "";
            ItemsChanged?.Invoke();
            SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("فاتورة جديدة - امسح باركود المنتج", NotificationType.Info);
        }

        public void ClearInvoice()
        {
            if (Items.Any(i => i.IsValid))
            {
                Items.Clear();
                ItemsChanged?.Invoke();
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("تم مسح الفاتورة", NotificationType.Info);
            }
            NewInvoice();
        }

        private async Task AddProductAsync()
        {
            if (string.IsNullOrWhiteSpace(BarcodeInput))
            {
                ShowValidation("يرجى إدخال باركود المنتج", true);
                return;
            }

            IsLoading = true;
            HideValidation();

            await Task.Run(() =>
            {
                var result = _salesService.FindProductByBarcode(BarcodeInput, 1);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!result.Success)
                    {
                        ShowValidation(result.ErrorMessage, true);
                        IsLoading = false;
                        return;
                    }

                    var product = result.Product;
                    var existingItem = Items.FirstOrDefault(i => i.ProductId == product.ProductId && i.IsValid);

                    if (existingItem != null)
                    {
                        existingItem.Quantity++;
                        existingItem.RecalculateTotal();
                        OnPropertyChanged(nameof(Items));
                    }
                    else
                    {
                        var newItem = new InvoiceItem
                        {
                            ProductId = product.ProductId,
                            ProductCode = product.ProductCode,
                            ProductName = product.ProductName,
                            Quantity = 1,
                            UnitPrice = product.UnitPrice,
                            DiscountPercent = 0,
                            DiscountAmount = 0,
                            IsNewRow = false
                        };
                        newItem.PropertyChanged += OnItemPropertyChanged;
                        Items.Insert(Items.Count - 1, newItem);
                    }

                    RecalculateTotals();
                    BarcodeInput = "";
                    ItemsChanged?.Invoke();
                    SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show($"تم إضافة {product.ProductName}", NotificationType.Success, 1);
                    IsLoading = false;
                });
            });
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InvoiceItem.Total) ||
                e.PropertyName == nameof(InvoiceItem.Quantity) ||
                e.PropertyName == nameof(InvoiceItem.UnitPrice) ||
                e.PropertyName == nameof(InvoiceItem.DiscountPercent))
            {
                RecalculateTotals();
            }

            if (sender is InvoiceItem item && item.IsValid && !item.IsNewRow)
            {
                var lastItem = Items.LastOrDefault();
                if (lastItem != null && lastItem.IsNewRow == false)
                    AddItem(new InvoiceItem { IsNewRow = true });
            }
        }

        private void AddItem(InvoiceItem item)
        {
            item.PropertyChanged += OnItemPropertyChanged;
            Items.Add(item);
        }

        private void RemoveItem(InvoiceItem item)
        {
            if (item != null && item.IsValid)
            {
                Items.Remove(item);
                RecalculateTotals();
                ItemsChanged?.Invoke();
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("تم حذف الصنف", NotificationType.Info, 1);
            }
        }

        private void RecalculateTotals()
        {
            var validItems = Items.Where(i => i.IsValid);
            Subtotal = validItems.Sum(i => i.Total);
            DiscountAmount = Subtotal * DiscountPercent / 100;
            var afterDiscount = Subtotal - DiscountAmount;
            TaxAmount = afterDiscount * TAX_RATE / 100;
            NetTotal = afterDiscount + TaxAmount;

            TotalsChanged?.Invoke();
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(DiscountDisplay));
            OnPropertyChanged(nameof(TaxDisplay));
            OnPropertyChanged(nameof(NetTotalDisplay));
            OnPropertyChanged(nameof(RemainingDisplay));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(HasItems));
        }

        private async Task SaveInvoiceAsync()
        {
            if (!Items.Any(i => i.IsValid))
            {
                ShowValidation("لا توجد أصناف في الفاتورة", true);
                return;
            }

            if (NetTotal <= 0)
            {
                ShowValidation("إجمالي الفاتورة يجب أن يكون أكبر من صفر", true);
                return;
            }

            IsLoading = true;
            HideValidation();

            await Task.Run(() =>
            {
                var request = new SaleRequest
                {
                    InvoiceNumber = InvoiceNumber,
                    Date = InvoiceDate,
                    UserId = App.CurrentUser?.Id ?? 1,
                    BranchId = App.CurrentUser?.BranchId,
                    CustomerId = SelectedCustomer?.Id > 0 ? SelectedCustomer.Id : (int?)null,
                    WarehouseId = 1,
                    ApplyDiscount = DiscountPercent > 0,
                    DiscountPercent = DiscountPercent,
                    ApplyTax = true,
                    Items = Items.Where(i => i.IsValid).Select(i => new SaleRequestItem
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice
                    }).ToList()
                };

                var result = _salesService.CreateSale(request);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!result.Success)
                    {
                        ShowValidation(result.ErrorMessage, true);
                        IsLoading = false;
                        return;
                    }

                    Status = DocumentStatus.Confirmed;
                    IsNewInvoice = false;
                    SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show($"تم حفظ الفاتورة رقم {InvoiceNumber}", NotificationType.Success);
                    IsLoading = false;
                });
            });
        }

        private void PrintInvoice()
        {
            if (!IsNewInvoice)
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("جاري طباعة الفاتورة...", NotificationType.Info);
            else
                SupermarketPOS.UI.Infrastructure.DependencyInjection.GetRequiredService<INotificationService>().Show("احفظ الفاتورة أولاً قبل الطباعة", NotificationType.Warning);
        }

        private void ShowValidation(string message, bool isError)
        {
            ValidationMessage = message;
            HasValidationError = isError;
        }

        private void HideValidation()
        {
            ValidationMessage = "";
            HasValidationError = false;
        }

        private string GenerateInvoiceNumber() => $"INV-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Ticks % 1000:D3}";
    }

    public class InvoiceItem : INotifyPropertyChanged
    {
        private int _productId;
        private string _productCode;
        private string _productName;
        private int _quantity = 1;
        private decimal _unitPrice;
        private decimal _discountPercent;
        private decimal _discountAmount;
        private decimal _total;
        private bool _isNewRow = true;
        private int _rowIndex;

        public event PropertyChangedEventHandler PropertyChanged;

        public int ProductId
        {
            get => _productId;
            set { _productId = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValid)); }
        }

        public string ProductCode
        {
            get => _productCode;
            set { _productCode = value; OnPropertyChanged(); }
        }

        public string ProductName
        {
            get => _productName;
            set { _productName = value; OnPropertyChanged(); }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value < 1 ? 1 : (value > 9999 ? 9999 : value);
                OnPropertyChanged();
                RecalculateTotal();
            }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                _unitPrice = value < 0 ? 0 : value;
                OnPropertyChanged();
                RecalculateTotal();
            }
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                _discountPercent = value < 0 ? 0 : (value > 100 ? 100 : value);
                OnPropertyChanged();
                RecalculateTotal();
            }
        }

        public decimal DiscountAmount
        {
            get => _discountAmount;
            set { _discountAmount = value < 0 ? 0 : value; OnPropertyChanged(); }
        }

        public decimal Total
        {
            get => _total;
            set { _total = value < 0 ? 0 : value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayTotal)); }
        }

        public bool IsNewRow
        {
            get => _isNewRow;
            set { _isNewRow = value; OnPropertyChanged(); }
        }

        public int RowIndex
        {
            get => _rowIndex;
            set { _rowIndex = value; OnPropertyChanged(); }
        }

        public bool IsValid => ProductId > 0 && Quantity > 0 && UnitPrice > 0;
        public string DisplayQuantity => Quantity.ToString();
        public string DisplayUnitPrice => $"{UnitPrice:N2}";
        public string DisplayDiscountPercent => $"{DiscountPercent}%";
        public string DisplayTotal => $"{Total:N2}";

        public void RecalculateTotal()
        {
            Total = Quantity * UnitPrice * (1m - DiscountPercent / 100m);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class CustomerDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public enum ContentState
    {
        Loading,
        Empty,
        Error,
        Content
    }
}
