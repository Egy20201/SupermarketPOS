using System;

namespace SupermarketPOS.Core.Entities
{
    public class SupplierPayment
    {
        public int Id { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; }
        public int? PurchaseInvoiceId { get; set; } // ممكن تكون دفعة على فاتورة معينة
        public PurchaseInvoice PurchaseInvoice { get; set; }
        public decimal Amount { get; set; }
        public string Notes { get; set; }
    }
}