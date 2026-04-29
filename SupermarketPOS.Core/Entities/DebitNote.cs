using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Core.Entities
{
    public class DebitNote : BaseDocument
    {
        public override string DocumentType => "DebitNote";

        public int? SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        public int? PurchaseInvoiceId { get; set; }
        public virtual PurchaseInvoice PurchaseInvoice { get; set; }

        public string Reason { get; set; }
        public decimal TotalAmount { get; set; }

        public virtual ICollection<DebitNoteItem> Items { get; set; }

        public override void Validate()
        {
            base.Validate();

            if (!PurchaseInvoiceId.HasValue && !SupplierId.HasValue)
                throw new InvalidOperationException("Debit note must link to an invoice or supplier");

            if (Items == null || !Items.Any())
                throw new InvalidOperationException("Debit note must have at least one item");

            if (TotalAmount <= 0)
                throw new InvalidOperationException("Debit note total must be greater than zero");
        }
    }

    public class DebitNoteItem
    {
        public int Id { get; set; }

        public int DebitNoteId { get; set; }
        public virtual DebitNote DebitNote { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}