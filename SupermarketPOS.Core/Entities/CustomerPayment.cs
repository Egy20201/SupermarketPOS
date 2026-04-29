using System;

namespace SupermarketPOS.Core.Entities
{
    public class CustomerPayment
    {
        public int Id { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }
        public int? SaleInvoiceId { get; set; }
        public SaleInvoice SaleInvoice { get; set; }
        public decimal Amount { get; set; }
        public string Notes { get; set; }
    }
}
