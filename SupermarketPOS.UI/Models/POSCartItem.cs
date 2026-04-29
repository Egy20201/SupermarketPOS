using SupermarketPOS.Business;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SupermarketPOS.UI.Models
{
    public class POSCartItem : INotifyPropertyChanged
    {
        private int _productId, _quantity = 1;
        private string _productCode, _productName;
        private decimal _unitPrice, _discount;

        public event PropertyChangedEventHandler PropertyChanged;

        public int ProductId { get => _productId; set { _productId = value; OnPropertyChanged(); } }
        public string ProductCode { get => _productCode; set { _productCode = value; OnPropertyChanged(); } }
        public string ProductName { get => _productName; set { _productName = value; OnPropertyChanged(); } }
        public int Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); } }
        public decimal UnitPrice { get => _unitPrice; set { _unitPrice = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); } }
        public decimal Discount { get => _discount; set { _discount = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); } }
        public decimal Total => (Quantity * UnitPrice) - Discount;
        
        private bool _isReturn;
        public bool IsReturn { get => _isReturn; set { _isReturn = value; OnPropertyChanged(); } }
        
        public int? ProductUnitId { get; set; }
        public string UnitName { get; set; }
        public decimal ConversionFactor { get; set; } = 1m;
        public List<SaleProductUnitOption> AvailableUnits { get; set; }
        public string SelectedUnitDisplay { get; set; }
        public int RowIndex { get; set; }

        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}