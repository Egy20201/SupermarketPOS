using System;

namespace SupermarketPOS.Core.Entities
{
    public class StockCostHistory
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public DateTime MovementDate { get; set; }
        public string MovementType { get; set; }
        public string Reference { get; set; }
        public int QuantityChange { get; set; }
        public decimal UnitCost { get; set; }
        public int QuantityBefore { get; set; }
        public int QuantityAfter { get; set; }
        public decimal AverageCostBefore { get; set; }
        public decimal AverageCostAfter { get; set; }
    }
}