using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Core.Entities
{
    public class CreditNote : BaseDocument
    {
        public override string DocumentType => "CreditNote";

        public int? CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        public int? SaleInvoiceId { get; set; }
        public virtual SaleInvoice SaleInvoice { get; set; }

        public string Reason { get; set; }
        public decimal TotalAmount { get; set; }

        public virtual ICollection<CreditNoteItem> Items { get; set; }

        public override void Validate()
        {
            base.Validate();

            if (!SaleInvoiceId.HasValue && !CustomerId.HasValue)
                throw new InvalidOperationException("Credit note must link to an invoice or customer");

            if (Items == null || !Items.Any())
                throw new InvalidOperationException("Credit note must have at least one item");

            if (TotalAmount <= 0)
                throw new InvalidOperationException("Credit note total must be greater than zero");
        }
    }

    public class CreditNoteItem
    {
        public int Id { get; set; }

        public int CreditNoteId { get; set; }
        public virtual CreditNote CreditNote { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}