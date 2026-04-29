using System;
using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class SaleInvoice
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal PaidAmount { get; set; }

        public int? WarehouseId { get; set; }
        public string Status { get; set; } = "Saved";

        public int? ShiftClosingId { get; set; }
        public virtual ShiftClosing ShiftClosing { get; set; }

        // Customer-related properties (added)
        public int? CustomerId { get; set; }
        public Customer Customer { get; set; }

        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }
        public ICollection<SaleItem> Items { get; set; }
        public ICollection<CustomerPayment> Payments { get; set; }

        public SaleInvoice()
        {
            Items = new List<SaleItem>();
            Payments = new List<CustomerPayment>();
            Date = DateTime.Now;
            PaidAmount = 0m;
        }
    }
}