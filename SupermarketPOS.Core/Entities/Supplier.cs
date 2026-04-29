using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Supplier
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ContactPerson { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Notes { get; set; } // ملاحظات
        public decimal Balance { get; set; } // رصيد المورد (مدين/دائن) - هنحدثه تلقائياً

        public ICollection<PurchaseInvoice> PurchaseInvoices { get; set; }
    }
}