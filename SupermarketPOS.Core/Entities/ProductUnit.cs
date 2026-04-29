using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class ProductUnit
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UnitId { get; set; }
        public decimal ConversionFactor { get; set; } = 1m;
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public bool IsBaseUnit { get; set; }

        [MaxLength(50)]
        public string Barcode { get; set; }

        public virtual Product Product { get; set; }
        public virtual Unit Unit { get; set; }
    }
}
