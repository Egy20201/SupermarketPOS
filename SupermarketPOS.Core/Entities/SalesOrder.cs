using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Core.Entities
{
    public class SalesOrder : BaseDocument
    {
        public override string DocumentType => "SalesOrder";

        public int? CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        public int? QuotationId { get; set; }
        public virtual SalesQuotation Quotation { get; set; }

        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public DateTime? ExpectedDeliveryDate { get; set; }

        public virtual ICollection<SalesOrderItem> Items { get; set; }

        public override void Validate()
        {
            base.Validate();

            if (!CustomerId.HasValue)
                throw new InvalidOperationException("Sales order must have a customer");

            if (Items == null || !Items.Any())
                throw new InvalidOperationException("Sales order must have at least one item");

            if (TotalAmount <= 0)
                throw new InvalidOperationException("Sales order total must be greater than zero");

            if (Status == DocumentStatus.Confirmed && Items.Any(i => i.Quantity <= 0))
                throw new InvalidOperationException("Cannot confirm order with zero-quantity items");
        }
    }

    public class SalesOrderItem
    {
        public int Id { get; set; }

        public int SalesOrderId { get; set; }
        public virtual SalesOrder SalesOrder { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int Quantity { get; set; }
        public int DeliveredQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalPrice { get; set; }
    }
}