using System;
using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class PurchaseInvoice
    {

        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public decimal Discount { get; set; }
        public int? SupplierId { get; set; }
        public Supplier Supplier { get; set; }
        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal PaidAmount { get; set; } // المدفوع
        public decimal RemainingAmount => TotalAmount - PaidAmount; // المتبقي

        public string PaymentType { get; set; } // "نقدي", "آجل"
        public string Notes { get; set; }

        public ICollection<PurchaseItem> Items { get; set; }
        public ICollection<SupplierPayment> Payments { get; set; } // دفعات المورد
    }
}