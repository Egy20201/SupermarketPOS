using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Core.Entities
{
    public class Delivery : BaseDocument
    {
        public override string DocumentType => "Delivery";

        public int? SalesOrderId { get; set; }
        public virtual SalesOrder SalesOrder { get; set; }

        public int WarehouseId { get; set; }
        public virtual Warehouse Warehouse { get; set; }

        public virtual ICollection<DeliveryItem> Items { get; set; }

        public override void Validate()
        {
            base.Validate();

            if (!SalesOrderId.HasValue)
                throw new InvalidOperationException("Delivery must be linked to a sales order");

            if (WarehouseId <= 0)
                throw new InvalidOperationException("Delivery must have a warehouse");

            if (Items == null || !Items.Any())
                throw new InvalidOperationException("Delivery must have at least one item");
        }
    }

    public class DeliveryItem
    {
        public int Id { get; set; }

        public int DeliveryId { get; set; }
        public virtual Delivery Delivery { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int Quantity { get; set; }
    }
}