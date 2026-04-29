using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class InventoryService : IInventoryMovementService
    {
        private readonly FeatureFlagService _featureFlagService;
        private readonly AuditService _auditService;

        public InventoryService(FeatureFlagService featureFlagService, AuditService auditService)
        {
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public bool DecreaseStockAndRecordMovement(AppDbContext db, int productId, int warehouseId, int quantity, decimal unitPrice, string reference, string movementType, out string errorMessage)
        {
            if (!_featureFlagService.IsEnabled("inventory")) return Fail("تم تعطيل المخزون", out errorMessage);
            errorMessage = null;
            if (!ValidateInput(db, productId, warehouseId, quantity, unitPrice, reference, movementType, out errorMessage)) return false;
            try
            {
                int rowsAffected = db.Database.ExecuteSqlCommand(@"UPDATE ProductStocks SET Quantity = Quantity - @p0 WHERE ProductId = @p1 AND WarehouseId = @p2 AND Quantity >= @p3", quantity, productId, warehouseId, quantity);
                if (rowsAffected == 0) { var currentStock = db.ProductStocks.Where(s => s.ProductId == productId && s.WarehouseId == warehouseId).Select(s => (int?)s.Quantity).FirstOrDefault(); if (currentStock == null) return Fail("المخزون غير موجود", out errorMessage); if (currentStock.Value < quantity) return Fail("المخزون غير كافٍ", out errorMessage); return Fail("فشل تحديث المخزون", out errorMessage); }
                int movementInserted = db.Database.ExecuteSqlCommand(@"INSERT INTO StockMovements (ProductId, WarehouseId, MovementType, Reference, QuantityOut, QuantityIn, UnitPrice, Date) SELECT @p0, @p1, @p2, @p3, @p4, 0, @p5, GETDATE() WHERE NOT EXISTS (SELECT 1 FROM StockMovements WHERE ProductId = @p0 AND WarehouseId = @p1 AND Reference = @p3 AND MovementType = @p2 AND QuantityOut = @p4 AND QuantityIn = 0)", productId, warehouseId, movementType, reference, quantity, unitPrice);
                if (movementInserted == 0) return Fail("حركة مخزون مكررة", out errorMessage);
                _auditService.Log("STOCK_DECREASE", "ProductStock", productId, 0);
                Logger.Info($"Stock decreased. Product={productId}, Warehouse={warehouseId}, Qty={quantity}, Ref={reference}");
                return true;
            }
            catch (Exception ex) { Logger.Error(ex, "DecreaseStockAndRecordMovement failed"); throw new InvalidOperationException("فشل تنفيذ حركة خصم المخزون", ex); }
        }

        public bool IncreaseStockAndRecordMovement(AppDbContext db, int productId, int warehouseId, int quantity, decimal unitPrice, string reference, string movementType, out string errorMessage)
        {
            if (!_featureFlagService.IsEnabled("inventory")) return Fail("تم تعطيل المخزون", out errorMessage);
            errorMessage = null;
            if (!ValidateInput(db, productId, warehouseId, quantity, unitPrice, reference, movementType, out errorMessage)) return false;
            try
            {
                db.Database.ExecuteSqlCommand(@"MERGE ProductStocks AS target USING (SELECT @p0 AS ProductId, @p1 AS WarehouseId, @p2 AS Quantity, @p3 AS AverageCost) AS source ON (target.ProductId = source.ProductId AND target.WarehouseId = source.WarehouseId) WHEN MATCHED THEN UPDATE SET Quantity = target.Quantity + source.Quantity WHEN NOT MATCHED THEN INSERT (ProductId, WarehouseId, Quantity, AverageCost) VALUES (source.ProductId, source.WarehouseId, source.Quantity, source.AverageCost);", productId, warehouseId, quantity, unitPrice);
                int movementInserted = db.Database.ExecuteSqlCommand(@"INSERT INTO StockMovements (ProductId, WarehouseId, MovementType, Reference, QuantityIn, QuantityOut, UnitPrice, Date) SELECT @p0, @p1, @p2, @p3, @p4, 0, @p5, GETDATE() WHERE NOT EXISTS (SELECT 1 FROM StockMovements WHERE ProductId = @p0 AND WarehouseId = @p1 AND Reference = @p3 AND MovementType = @p2 AND QuantityIn = @p4 AND QuantityOut = 0)", productId, warehouseId, movementType, reference, quantity, unitPrice);
                if (movementInserted == 0) return Fail("حركة مخزون مكررة", out errorMessage);
                _auditService.Log("STOCK_INCREASE", "ProductStock", productId, 0);
                Logger.Info($"Stock increased. Product={productId}, Warehouse={warehouseId}, Qty={quantity}, Ref={reference}");
                return true;
            }
            catch (Exception ex) { Logger.Error(ex, "IncreaseStockAndRecordMovement failed"); throw new InvalidOperationException("فشل تنفيذ حركة إضافة المخزون", ex); }
        }

        public bool SetStockAndRecordMovement(AppDbContext db, int productId, int warehouseId, int quantity, decimal unitPrice, string reference, string movementType, out string errorMessage)
        {
            if (!_featureFlagService.IsEnabled("inventory")) return Fail("تم تعطيل المخزون", out errorMessage);
            errorMessage = null;
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (productId <= 0 || warehouseId <= 0 || quantity < 0) { errorMessage = "بيانات المخزون غير صالحة"; return false; }
            if (unitPrice < 0) { errorMessage = "سعر الوحدة غير صالح"; return false; }
            if (string.IsNullOrWhiteSpace(reference)) { errorMessage = "مرجع الحركة غير صالح"; return false; }
            if (!db.Products.Any(p => p.Id == productId)) { errorMessage = "المنتج غير موجود"; return false; }
            if (!db.Warehouses.Any(w => w.Id == warehouseId)) { errorMessage = "المخزن غير موجود"; return false; }

            var stock = db.ProductStocks.FirstOrDefault(ps => ps.ProductId == productId && ps.WarehouseId == warehouseId);
            var oldQuantity = stock == null ? 0 : stock.Quantity;
            if (stock == null)
            {
                stock = new ProductStock { ProductId = productId, WarehouseId = warehouseId, Quantity = quantity, AverageCost = unitPrice };
                db.ProductStocks.Add(stock);
            }
            else
            {
                stock.Quantity = quantity;
                stock.AverageCost = unitPrice;
            }

            var diff = quantity - oldQuantity;
            db.StockMovements.Add(new StockMovement
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                MovementType = movementType,
                Reference = reference,
                QuantityIn = diff > 0 ? diff : 0,
                QuantityOut = diff < 0 ? -diff : 0,
                UnitPrice = unitPrice,
                Date = DateTime.Now
            });

            return true;
        }

        public bool TransferStock(AppDbContext db, int productId, int fromWarehouseId, int toWarehouseId, int quantity, string reference, out string errorMessage)
        {
            if (!_featureFlagService.IsEnabled("inventory")) return Fail("تم تعطيل المخزون", out errorMessage);
            errorMessage = null;
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (productId <= 0 || fromWarehouseId <= 0 || toWarehouseId <= 0 || quantity <= 0 || fromWarehouseId == toWarehouseId)
            {
                errorMessage = "بيانات التحويل غير صالحة";
                return false;
            }

            var fromStock = db.ProductStocks.FirstOrDefault(ps => ps.ProductId == productId && ps.WarehouseId == fromWarehouseId);
            if (fromStock == null || fromStock.Quantity < quantity)
            {
                errorMessage = "المخزون غير كاف";
                return false;
            }

            var averageCost = fromStock.AverageCost;
            fromStock.Quantity -= quantity;

            var toStock = db.ProductStocks.FirstOrDefault(ps => ps.ProductId == productId && ps.WarehouseId == toWarehouseId);
            if (toStock == null)
            {
                db.ProductStocks.Add(new ProductStock { ProductId = productId, WarehouseId = toWarehouseId, Quantity = quantity, AverageCost = averageCost });
            }
            else
            {
                toStock.Quantity += quantity;
                if (toStock.AverageCost <= 0) toStock.AverageCost = averageCost;
            }

            db.StockMovements.Add(new StockMovement { ProductId = productId, WarehouseId = fromWarehouseId, MovementType = "تحويل", Reference = reference, QuantityOut = quantity, UnitPrice = averageCost, Date = DateTime.Now });
            db.StockMovements.Add(new StockMovement { ProductId = productId, WarehouseId = toWarehouseId, MovementType = "تحويل", Reference = reference, QuantityIn = quantity, UnitPrice = averageCost, Date = DateTime.Now });
            return true;
        }

        private static bool ValidateInput(AppDbContext db, int productId, int warehouseId, int quantity, decimal unitPrice, string reference, string movementType, out string errorMessage)
        {
            errorMessage = null;
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (productId <= 0 || warehouseId <= 0 || quantity <= 0) { errorMessage = "بيانات المخزون غير صالحة"; return false; }
            if (unitPrice < 0) { errorMessage = "سعر الوحدة غير صالح"; return false; }
            if (string.IsNullOrWhiteSpace(reference)) { errorMessage = "مرجع الحركة غير صالح"; return false; }
            if (string.IsNullOrWhiteSpace(movementType)) { errorMessage = "نوع الحركة غير صالح"; return false; }
            if (!db.Products.Any(p => p.Id == productId)) { errorMessage = "المنتج غير موجود"; return false; }
            if (!db.Warehouses.Any(w => w.Id == warehouseId)) { errorMessage = "المخزن غير موجود"; return false; }
            return true;
        }

        private static bool Fail(string message, out string errorMessage) { errorMessage = message; Logger.Info("Inventory operation failed: " + message); return false; }
    }
}
