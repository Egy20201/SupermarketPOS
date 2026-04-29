using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Core.Entities
{
    public class GoodsReceipt : BaseDocument
    {
        public override string DocumentType => "GoodsReceipt";

        public int? PurchaseOrderId { get; set; }
        public virtual PurchaseOrder PurchaseOrder { get; set; }

        public int WarehouseId { get; set; }
        public virtual Warehouse Warehouse { get; set; }

        public virtual ICollection<GoodsReceiptItem> Items { get; set; }

        public override void Validate()
        {
            base.Validate();

            if (!PurchaseOrderId.HasValue)
                throw new InvalidOperationException("Goods receipt must be linked to a purchase order");

            if (WarehouseId <= 0)
                throw new InvalidOperationException("Goods receipt must have a warehouse");

            if (Items == null || !Items.Any())
                throw new InvalidOperationException("Goods receipt must have at least one item");
        }
    }

    public class GoodsReceiptItem
    {
        public int Id { get; set; }

        public int GoodsReceiptId { get; set; }
        public virtual GoodsReceipt GoodsReceipt { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int Quantity { get; set; }
    }
}