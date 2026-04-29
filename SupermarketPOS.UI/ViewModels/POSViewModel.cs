using SupermarketPOS.Business;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace SupermarketPOS.UI.ViewModels
{
    public class POSViewModel : BaseViewModel
    {
        private readonly SalesService _salesService;
        private readonly ICurrentUserService _currentUserService;
        private string _barcodeInput;
        private SaleCustomerDto _selectedCustomer;
        private PosInvoiceItem _selectedItem;
        private decimal _globalDiscountPercent;
        private bool _hasGlobalDiscount;
        private bool _hasTax = true;
        private decimal _paidAmount;
        private string _validationMessage;
        private bool _hasValidationError;

        public POSViewModel(SalesService salesService, ICurrentUserService currentUserService)
        {
            _salesService = salesService ?? throw new ArgumentNullException(nameof(salesService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));

            Items = new ObservableCollection<PosInvoiceItem>();
            Customers = new ObservableCollection<SaleCustomerDto>();
            Products = new ObservableCollection<SaleProductDto>();
            PaymentMethods = new ObservableCollection<string> { "نقدي", "آجل" };
            PaymentMethod = PaymentMethods.FirstOrDefault();

            AddProductCommand = new RelayCommand(AddProduct);
            RemoveItemCommand = new RelayCommand<PosInvoiceItem>(RemoveItem);
            AddNewEmptyRowCommand = new RelayCommand(() => { });
            NewCommand = new RelayCommand(NewInvoice);
            ClearCommand = new RelayCommand(NewInvoice);
            SaveCommand = new RelayCommand(SaveInvoice, () => Items.Any(i => i.IsValid));
            PrintCommand = new RelayCommand(() => { });

            LoadLookups();
            NewInvoice();
        }

        public ObservableCollection<PosInvoiceItem> Items { get; }
        public ObservableCollection<SaleCustomerDto> Customers { get; }
        public ObservableCollection<SaleProductDto> Products { get; }
        public ObservableCollection<string> PaymentMethods { get; }

        public string InvoiceNumber { get; private set; }
        public DateTime InvoiceDate { get; set; }
        public string PaymentMethod { get; set; }

        public string BarcodeInput
        {
            get => _barcodeInput;
            set => SetProperty(ref _barcodeInput, value);
        }

        public SaleCustomerDto SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                    OnPropertyChanged(nameof(ShowBalance));
            }
        }

        public PosInvoiceItem SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public bool HasGlobalDiscount
        {
            get => _hasGlobalDiscount;
            set
            {
                if (SetProperty(ref _hasGlobalDiscount, value))
                    RefreshTotals();
            }
        }

        public decimal GlobalDiscountPercent
        {
            get => _globalDiscountPercent;
            set
            {
                if (SetProperty(ref _globalDiscountPercent, value))
                    RefreshTotals();
            }
        }

        public bool HasTax
        {
            get => _hasTax;
            set
            {
                if (SetProperty(ref _hasTax, value))
                    RefreshTotals();
            }
        }

        public decimal PaidAmount
        {
            get => _paidAmount;
            set
            {
                if (SetProperty(ref _paidAmount, value))
                    OnPropertyChanged(nameof(RemainingDisplay));
            }
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

        public bool ShowBalance => SelectedCustomer != null && SelectedCustomer.Id > 0;
        public decimal CustomerBalance => 0m;
        public decimal Subtotal => Items.Where(i => i.IsValid).Sum(i => i.Total);
        public decimal Discount => HasGlobalDiscount ? Subtotal * GlobalDiscountPercent / 100m : 0m;
        public decimal Tax => HasTax ? (Subtotal - Discount) * 0.14m : 0m;
        public decimal NetTotal => Subtotal - Discount + Tax;
        public string SubtotalDisplay => Subtotal.ToString("N2");
        public string DiscountDisplay => Discount.ToString("N2");
        public string TaxDisplay => Tax.ToString("N2");
        public string NetTotalDisplay => NetTotal.ToString("N2");
        public string RemainingDisplay => (NetTotal - PaidAmount).ToString("N2");

        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand AddNewEmptyRowCommand { get; }

        private void LoadLookups()
        {
            Customers.Clear();
            Customers.Add(new SaleCustomerDto { Id = 0, Name = "عميل نقدي" });
            foreach (var customer in _salesService.GetActiveCustomers(_currentUserService.BranchId, _currentUserService.UserRole))
                Customers.Add(customer);

            SelectedCustomer = Customers.FirstOrDefault();
        }

        private void NewInvoice()
        {
            InvoiceNumber = _salesService.GetNextInvoiceNumber();
            InvoiceDate = DateTime.Today;
            Items.Clear();
            PaidAmount = 0m;
            BarcodeInput = string.Empty;
            ValidationMessage = string.Empty;
            HasValidationError = false;
            OnPropertyChanged(nameof(InvoiceNumber));
            RefreshTotals();
        }

        private void AddProduct()
        {
            var warehouseId = _salesService.GetDefaultWarehouseId();
            var result = _salesService.FindProductByBarcode(BarcodeInput, warehouseId, _currentUserService.BranchId, _currentUserService.UserRole);
            if (!result.Success)
            {
                ValidationMessage = result.ErrorMessage;
                HasValidationError = true;
                return;
            }

            var existing = Items.FirstOrDefault(i => i.ProductId == result.Product.ProductId);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                var item = new PosInvoiceItem(result.Product);
                item.PropertyChanged += (sender, args) => RefreshTotals();
                Items.Add(item);
            }

            BarcodeInput = string.Empty;
            HasValidationError = false;
            RefreshTotals();
        }

        private void RemoveItem(PosInvoiceItem item)
        {
            if (item == null) return;
            Items.Remove(item);
            RefreshTotals();
        }

        private void SaveInvoice()
        {
            var request = new SaleRequest
            {
                InvoiceNumber = InvoiceNumber,
                Date = InvoiceDate,
                UserId = _currentUserService.UserId == 0 ? 1 : _currentUserService.UserId,
                BranchId = _currentUserService.BranchId,
                UserRole = _currentUserService.UserRole,
                CustomerId = SelectedCustomer != null && SelectedCustomer.Id > 0 ? SelectedCustomer.Id : (int?)null,
                WarehouseId = _salesService.GetDefaultWarehouseId(),
                ApplyDiscount = HasGlobalDiscount,
                DiscountPercent = GlobalDiscountPercent,
                ApplyTax = HasTax,
                Items = Items.Where(i => i.IsValid).Select(i => new SaleRequestItem
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    ProductUnitId = i.ProductUnitId,
                    UnitName = i.UnitName,
                    ConversionFactor = i.ConversionFactor
                }).ToList()
            };

            var result = _salesService.CreateSale(request);
            if (!result.Success)
            {
                ValidationMessage = result.ErrorMessage;
                HasValidationError = true;
                return;
            }

            NewInvoice();
        }

        private void RefreshTotals()
        {
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(Discount));
            OnPropertyChanged(nameof(Tax));
            OnPropertyChanged(nameof(NetTotal));
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(DiscountDisplay));
            OnPropertyChanged(nameof(TaxDisplay));
            OnPropertyChanged(nameof(NetTotalDisplay));
            OnPropertyChanged(nameof(RemainingDisplay));
        }
    }

    public class PosInvoiceItem : BaseViewModel
    {
        private int _quantity = 1;
        private decimal _unitPrice;
        private decimal _discountPercent;

        public PosInvoiceItem(SaleProductDto product)
        {
            ProductId = product.ProductId;
            ProductCode = product.ProductCode;
            ProductName = product.ProductName;
            UnitPrice = product.UnitPrice;
            ProductUnitId = product.ProductUnitId;
            UnitName = product.UnitName;
            ConversionFactor = product.ConversionFactor;
        }

        public int RowIndex { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public int? ProductUnitId { get; set; }
        public string UnitName { get; set; }
        public decimal ConversionFactor { get; set; } = 1m;

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value < 1 ? 1 : value))
                    OnPropertyChanged(nameof(Total));
            }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value < 0m ? 0m : value))
                    OnPropertyChanged(nameof(Total));
            }
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (SetProperty(ref _discountPercent, value < 0m ? 0m : value))
                    OnPropertyChanged(nameof(Total));
            }
        }

        public decimal Total => Quantity * UnitPrice * (1m - DiscountPercent / 100m);
        public bool IsValid => ProductId > 0 && Quantity > 0 && UnitPrice >= 0m;
    }
}
