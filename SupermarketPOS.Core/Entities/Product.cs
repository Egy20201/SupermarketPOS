using System;
using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Product
    {
        public int? BranchId { get; set; }
        public virtual Branch Branch { get; set; }
        public int Id { get; set; }
        public string Barcode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Unit { get; set; } = "قطعة";
        public int? UnitId { get; set; }

        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int ReorderLevel { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; } = true;

        public int CategoryId { get; set; }
        public Category Category { get; set; }
        public Unit UnitEntity { get; set; }

        public ICollection<ProductStock> ProductStocks { get; set; }
        public ICollection<StockMovement> StockMovements { get; set; }
        public ICollection<SaleItem> SaleItems { get; set; }
        public ICollection<PurchaseItem> PurchaseItems { get; set; }
        public ICollection<ProductAttribute> ProductAttributes { get; set; }
        public ICollection<ProductUnit> ProductUnits { get; set; }

        public int CurrentStock { get; set; }
    }
}