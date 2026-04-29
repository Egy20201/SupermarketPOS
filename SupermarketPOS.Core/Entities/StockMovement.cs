using System;

namespace SupermarketPOS.Core.Entities
{
    public class StockMovement
    {
        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }
        public int Id { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public string MovementType { get; set; } // "شراء", "بيع", "تحويل", "مرتجع", "جرد"
        public string Reference { get; set; } // رقم الفاتورة أو إذن التحويل

        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int WarehouseId { get; set; }
        public Warehouse Warehouse { get; set; }

        public int QuantityIn { get; set; } // الكمية الداخلة
        public int QuantityOut { get; set; } // الكمية الخارجة
        public decimal UnitPrice { get; set; } // سعر الوحدة (للشراء أو البيع)

        public string Notes { get; set; }
    }
}