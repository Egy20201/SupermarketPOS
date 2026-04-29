using SupermarketPOS.Business;
using SupermarketPOS.UI.Infrastructure;
using SupermarketPOS.UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace SupermarketPOS.UI.ViewModels
{
    public class PurchaseInvoiceViewModel : BaseViewModel
    {
        private readonly PurchaseService _purchaseService;
        private readonly ICurrentUserService _currentUserService;
        private PurchaseSupplierDto _selectedSupplier;
        private PurchaseWarehouseDto _selectedWarehouse;
        private PurchaseInvoiceItem _selectedItem;
        private decimal _paidAmount;

        public PurchaseInvoiceViewModel()
            : this(
                  DependencyInjection.GetRequiredService<PurchaseService>(),
                  DependencyInjection.GetRequiredService<ICurrentUserService>())
        {
        }

        public PurchaseInvoiceViewModel(PurchaseService purchaseService, ICurrentUserService currentUserService)
        {
            _purchaseService = purchaseService ?? throw new ArgumentNullException(nameof(purchaseService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));

            Items = new ObservableCollection<PurchaseInvoiceItem>();
            Suppliers = new ObservableCollection<PurchaseSupplierDto>();
            Warehouses = new ObservableCollection<PurchaseWarehouseDto>();
            PaymentTypes = new ObservableCollection<string> { "نقدي", "آجل" };
            PaymentType = PaymentTypes.FirstOrDefault();

            NewCommand = new RelayCommand(NewInvoice);
            SaveCommand = new RelayCommand(SaveInvoice, () => Items.Any(i => i.IsValid) && SelectedSupplier != null && SelectedWarehouse != null);

            LoadLookups();
            NewInvoice();
        }

        public ObservableCollection<PurchaseInvoiceItem> Items { get; }
        public ObservableCollection<PurchaseSupplierDto> Suppliers { get; }
        public ObservableCollection<PurchaseWarehouseDto> Warehouses { get; }
        public ObservableCollection<string> PaymentTypes { get; }

        public string InvoiceNumber { get; private set; }
        public DateTime InvoiceDate { get; set; }
        public string PaymentType { get; set; }

        public PurchaseSupplierDto SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value);
        }

        public PurchaseWarehouseDto SelectedWarehouse
        {
            get => _selectedWarehouse;
            set => SetProperty(ref _selectedWarehouse, value);
        }

        public PurchaseInvoiceItem SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public decimal PaidAmount
        {
            get => _paidAmount;
            set
            {
                if (SetProperty(ref _paidAmount, value))
                    OnPropertyChanged(nameof(NetTotalDisplay));
            }
        }

        public decimal Subtotal => Items.Where(i => i.IsValid).Sum(i => i.Quantity * i.UnitPrice);
        public decimal Discount => Items.Where(i => i.IsValid).Sum(i => i.Discount);
        public decimal Tax => (Subtotal - Discount) * 0.14m;
        public decimal NetTotal => Subtotal - Discount + Tax;
        public string SubtotalDisplay => Subtotal.ToString("N2");
        public string DiscountDisplay => Discount.ToString("N2");
        public string TaxDisplay => Tax.ToString("N2");
        public string NetTotalDisplay => NetTotal.ToString("N2");

        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }

        private void LoadLookups()
        {
            Suppliers.Clear();
            foreach (var supplier in _purchaseService.GetSuppliers(_currentUserService.BranchId, _currentUserService.UserRole))
                Suppliers.Add(supplier);
            SelectedSupplier = Suppliers.FirstOrDefault();

            Warehouses.Clear();
            foreach (var warehouse in _purchaseService.GetActiveWarehouses())
                Warehouses.Add(warehouse);
            SelectedWarehouse = Warehouses.FirstOrDefault(w => w.IsDefault) ?? Warehouses.FirstOrDefault();
        }

        private void NewInvoice()
        {
            InvoiceNumber = _purchaseService.GetNextInvoiceNumber();
            InvoiceDate = DateTime.Today;
            Items.Clear();
            PaidAmount = 0m;
            OnPropertyChanged(nameof(InvoiceNumber));
            RefreshTotals();
        }

        private void SaveInvoice()
        {
            var request = new PurchaseRequest
            {
                InvoiceNumber = InvoiceNumber,
                Date = InvoiceDate,
                UserId = _currentUserService.UserId == 0 ? 1 : _currentUserService.UserId,
                BranchId = _currentUserService.BranchId,
                UserRole = _currentUserService.UserRole,
                SupplierId = SelectedSupplier.Id,
                WarehouseId = SelectedWarehouse.Id,
                PaymentType = PaymentType,
                PaidAmount = PaidAmount,
                Items = Items.Where(i => i.IsValid).Select(i => new PurchaseRequestItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Discount = i.Discount
                }).ToList()
            };

            var result = _purchaseService.CreatePurchase(request);
            if (!result.Success)
            {
                DependencyInjection.GetRequiredService<INotificationService>().Show(result.ErrorMessage, NotificationType.Error);
                return;
            }

            NewInvoice();
        }

        private void RefreshTotals()
        {
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(DiscountDisplay));
            OnPropertyChanged(nameof(TaxDisplay));
            OnPropertyChanged(nameof(NetTotalDisplay));
        }
    }

    public class PurchaseInvoiceItem : BaseViewModel
    {
        private int _quantity = 1;
        private decimal _unitPrice;
        private decimal _discount;

        public int RowIndex { get; set; }
        public int ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }

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

        public decimal Discount
        {
            get => _discount;
            set
            {
                if (SetProperty(ref _discount, value < 0m ? 0m : value))
                    OnPropertyChanged(nameof(Total));
            }
        }

        public decimal Total => (Quantity * UnitPrice) - Discount;
        public bool IsValid => ProductId > 0 && Quantity > 0 && UnitPrice >= 0m;
    }
}
