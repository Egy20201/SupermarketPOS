using System;
using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Notes { get; set; }
        public decimal Balance { get; set; }
        public int LoyaltyPoints { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public virtual ICollection<SaleInvoice> SaleInvoices { get; set; }
        public virtual ICollection<CustomerPayment> CustomerPayments { get; set; }
    }
}