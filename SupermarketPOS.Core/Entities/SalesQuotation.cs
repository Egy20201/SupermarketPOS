using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Core.Entities
{
    public class SalesQuotation : BaseDocument
    {
        public override string DocumentType => "SalesQuotation";

        public int? CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public DateTime? ValidUntil { get; set; }
        public int? ConvertedToOrderId { get; set; }

        public virtual ICollection<SalesQuotationItem> Items { get; set; }

        public override void Validate()
        {
            base.Validate();

            if (!CustomerId.HasValue)
                throw new InvalidOperationException("Quotation must have a customer");

            if (Items == null || !Items.Any())
                throw new InvalidOperationException("Quotation must have at least one item");

            if (TotalAmount <= 0)
                throw new InvalidOperationException("Quotation total must be greater than zero");
        }
    }

    public class SalesQuotationItem
    {
        public int Id { get; set; }

        public int SalesQuotationId { get; set; }
        public virtual SalesQuotation SalesQuotation { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalPrice { get; set; }
    }
}