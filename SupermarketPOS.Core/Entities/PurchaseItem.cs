namespace SupermarketPOS.Core.Entities
{
    public class PurchaseItem
    {
        public int Id { get; set; }
        public int PurchaseInvoiceId { get; set; }
        public PurchaseInvoice PurchaseInvoice { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        public int? ProductUnitId { get; set; }
        public string UnitName { get; set; }
        public decimal ConversionFactor { get; set; } = 1m;
        public int BaseQuantity { get; set; }
    }
}