using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Core.Entities
{
    public class PurchaseOrder : BaseDocument
    {
        public override string DocumentType => "PurchaseOrder";

        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public DateTime? ExpectedDeliveryDate { get; set; }

        public virtual ICollection<PurchaseOrderItem> Items { get; set; }

        public override void Validate()
        {
            base.Validate();

            if (SupplierId <= 0)
                throw new InvalidOperationException("Purchase order must have a supplier");

            if (Items == null || !Items.Any())
                throw new InvalidOperationException("Purchase order must have at least one item");

            if (TotalAmount <= 0)
                throw new InvalidOperationException("Purchase order total must be greater than zero");
        }
    }

    public class PurchaseOrderItem
    {
        public int Id { get; set; }

        public int PurchaseOrderId { get; set; }
        public virtual PurchaseOrder PurchaseOrder { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int Quantity { get; set; }
        public int ReceivedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}