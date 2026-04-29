using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business.Modules
{
    public class InventoryModuleService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly IInventoryMovementService _inventoryService;
        private readonly TransactionExecutor _transactionExecutor;

        public InventoryModuleService(Func<AppDbContext> dbFactory, IInventoryMovementService inventoryService, TransactionExecutor transactionExecutor)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _transactionExecutor = transactionExecutor ?? throw new ArgumentNullException(nameof(transactionExecutor));
        }

        public List<Warehouse> GetActiveWarehouses(bool includeAll = false)
        {
            using (var db = _dbFactory())
            {
                var warehouses = db.Warehouses.AsNoTracking().Where(w => w.IsActive).OrderBy(w => w.Name).ToList();
                if (includeAll) warehouses.Insert(0, new Warehouse { Id = 0, Name = "جميع المخازن" });
                return warehouses;
            }
        }

        public List<Product> GetActiveProducts()
        {
            using (var db = _dbFactory())
            {
                return db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToList();
            }
        }

        public InventoryValuationResult GetInventoryValuation(int? warehouseId, bool showZeroStock)
        {
            using (var db = _dbFactory())
            {
                var query = db.ProductStocks.Include("Product").Include("Product.Category").Include("Warehouse").AsQueryable();
                if (warehouseId.HasValue) query = query.Where(ps => ps.WarehouseId == warehouseId.Value);
                if (!showZeroStock) query = query.Where(ps => ps.Quantity > 0);

                var result = new InventoryValuationResult();
                foreach (var stock in query.ToList())
                {
                    var averageCost = stock.AverageCost > 0 ? stock.AverageCost : (stock.Product == null ? 0 : stock.Product.PurchasePrice);
                    var value = stock.Quantity * averageCost;
                    result.Items.Add(new InventoryValuationItemDto
                    {
                        ProductId = stock.ProductId,
                        Barcode = stock.Product == null ? "-" : stock.Product.Barcode ?? "-",
                        ProductName = stock.Product == null ? "-" : stock.Product.Name,
                        CategoryName = stock.Product == null || stock.Product.Category == null ? "-" : stock.Product.Category.Name,
                        WarehouseName = stock.Warehouse == null ? "-" : stock.Warehouse.Name,
                        Quantity = stock.Quantity,
                        AverageCost = averageCost,
                        TotalValue = value
                    });
                    result.TotalValue += value;
                    result.TotalQuantity += stock.Quantity;
                }

                result.TotalItems = result.Items.Count;
                return result;
            }
        }

        public List<OpeningInventoryItemDto> GetOpeningInventory(int warehouseId)
        {
            using (var db = _dbFactory())
            {
                var products = db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToList();
                var stockByProduct = db.ProductStocks.AsNoTracking().Where(ps => ps.WarehouseId == warehouseId).ToList().ToDictionary(ps => ps.ProductId);
                return products.Select(p =>
                {
                    ProductStock stock;
                    stockByProduct.TryGetValue(p.Id, out stock);
                    return new OpeningInventoryItemDto
                    {
                        ProductId = p.Id,
                        ProductName = p.Name,
                        Barcode = p.Barcode ?? "-",
                        Quantity = stock == null ? 0 : stock.Quantity,
                        UnitCost = stock == null ? p.PurchasePrice : stock.AverageCost
                    };
                }).ToList();
            }
        }

        public void SaveOpeningInventory(int warehouseId, IEnumerable<OpeningInventorySaveItemDto> items)
        {
            var validItems = (items ?? Enumerable.Empty<OpeningInventorySaveItemDto>()).Where(i => i.Quantity > 0).ToList();
            if (!validItems.Any()) throw new InvalidOperationException("لا توجد أرصدة");

            _transactionExecutor.Execute(db =>
            {
                var reference = string.Format("OPEN-{0:yyyyMMdd-HHmmss}", DateTime.Now);
                foreach (var item in validItems)
                {
                    string error;
                    if (!_inventoryService.SetStockAndRecordMovement(db, item.ProductId, warehouseId, item.Quantity, item.UnitCost, reference, "رصيد افتتاحي", out error))
                        throw new InvalidOperationException(error);
                }
                db.SaveChanges();
                return true;
            }, committed => committed);
        }

        public List<StockTakeRowDto> GetStockTakeRows(int warehouseId)
        {
            using (var db = _dbFactory())
            {
                var products = db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToList();
                var stockByProduct = db.ProductStocks.AsNoTracking().Where(ps => ps.WarehouseId == warehouseId).ToList().ToDictionary(ps => ps.ProductId);
                return products.Select(p =>
                {
                    ProductStock stock;
                    stockByProduct.TryGetValue(p.Id, out stock);
                    var quantity = stock == null ? 0 : stock.Quantity;
                    return new StockTakeRowDto
                    {
                        ProductId = p.Id,
                        Barcode = p.Barcode,
                        ProductName = p.Name,
                        SystemQuantity = quantity,
                        ActualQuantity = quantity,
                        PurchasePrice = p.PurchasePrice
                    };
                }).ToList();
            }
        }

        public void SaveStockTake(int warehouseId, IEnumerable<StockTakeSaveItemDto> items)
        {
            var differences = (items ?? Enumerable.Empty<StockTakeSaveItemDto>()).Where(i => i.ActualQuantity != i.SystemQuantity).ToList();
            if (!differences.Any()) throw new InvalidOperationException("لا توجد فروقات");

            _transactionExecutor.Execute(db =>
            {
                var reference = string.Format("ST-{0:yyyyMMdd-HHmmss}", DateTime.Now);
                foreach (var item in differences)
                {
                    string error;
                    if (!_inventoryService.SetStockAndRecordMovement(db, item.ProductId, warehouseId, item.ActualQuantity, item.PurchasePrice, reference, "جرد", out error))
                        throw new InvalidOperationException(error);
                }
                db.SaveChanges();
                return true;
            }, committed => committed);
        }

        public int GetAvailableQuantity(int productId, int warehouseId)
        {
            using (var db = _dbFactory())
            {
                var stock = db.ProductStocks.AsNoTracking().FirstOrDefault(ps => ps.ProductId == productId && ps.WarehouseId == warehouseId);
                return stock == null ? 0 : stock.Quantity;
            }
        }

        public ProductStockInfoDto GetProductStockInfo(int productId, int warehouseId)
        {
            using (var db = _dbFactory())
            {
                var product = db.Products.AsNoTracking().FirstOrDefault(p => p.Id == productId);
                var stock = db.ProductStocks.AsNoTracking().FirstOrDefault(ps => ps.ProductId == productId && ps.WarehouseId == warehouseId);
                return new ProductStockInfoDto
                {
                    ProductId = productId,
                    CurrentStock = stock == null ? 0 : stock.Quantity,
                    Cost = product == null ? 0 : product.PurchasePrice
                };
            }
        }

        public void SaveStockLoss(int warehouseId, IEnumerable<StockLossSaveItemDto> items, string notes)
        {
            var validItems = (items ?? Enumerable.Empty<StockLossSaveItemDto>()).Where(i => i.Quantity > 0).ToList();
            if (!validItems.Any()) throw new InvalidOperationException("لا توجد أصناف");

            _transactionExecutor.Execute(db =>
            {
                var reference = string.Format("LOSS-{0:yyyyMMdd-HHmmss}", DateTime.Now);
                foreach (var item in validItems)
                {
                    string error;
                    if (!_inventoryService.DecreaseStockAndRecordMovement(db, item.ProductId, warehouseId, item.Quantity, item.Cost, reference, "تالف", out error))
                        throw new InvalidOperationException(error + ": " + item.ProductName);
                }
                db.SaveChanges();
                return true;
            }, committed => committed);
        }

        public string GetNextTransferNumber()
        {
            using (var db = _dbFactory())
            {
                var lastId = db.StockMovements.Where(m => m.MovementType == "تحويل").OrderByDescending(m => m.Id).Select(m => (int?)m.Id).FirstOrDefault() ?? 0;
                return (lastId + 1).ToString();
            }
        }

        public void SaveStockTransfer(int fromWarehouseId, int toWarehouseId, string transferNumber, IEnumerable<StockTransferSaveItemDto> items)
        {
            var validItems = (items ?? Enumerable.Empty<StockTransferSaveItemDto>()).Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();
            if (!validItems.Any() || fromWarehouseId <= 0 || toWarehouseId <= 0 || fromWarehouseId == toWarehouseId)
                throw new InvalidOperationException("بيانات غير صحيحة");

            _transactionExecutor.Execute(db =>
            {
                var reference = string.Format("TR-{0}-{1:yyyyMMdd}", transferNumber, DateTime.Now);
                foreach (var item in validItems)
                {
                    string error;
                    if (!_inventoryService.TransferStock(db, item.ProductId, fromWarehouseId, toWarehouseId, item.Quantity, reference, out error))
                        throw new InvalidOperationException(error + ": " + item.ProductName);
                }
                db.SaveChanges();
                return true;
            }, committed => committed);
        }

        public StockCardResult GetStockCard(int productId, int? warehouseId, DateTime from, DateTime toExclusive)
        {
            using (var db = _dbFactory())
            {
                var query = db.StockCostHistories.AsNoTracking().Where(h => h.ProductId == productId && h.MovementDate >= from && h.MovementDate < toExclusive);
                if (warehouseId.HasValue) query = query.Where(h => h.WarehouseId == warehouseId.Value);
                var movements = query.OrderBy(h => h.MovementDate).ToList();
                var warehouseNames = db.Warehouses.AsNoTracking().ToDictionary(w => w.Id, w => w.Name);
                var result = new StockCardResult();
                var runningValue = 0m;

                foreach (var movement in movements)
                {
                    string warehouseName;
                    if (!warehouseNames.TryGetValue(movement.WarehouseId, out warehouseName)) warehouseName = "-";
                    var row = new StockCardRowDto
                    {
                        Date = movement.MovementDate,
                        MovementType = movement.MovementType,
                        Reference = movement.Reference,
                        WarehouseName = warehouseName,
                        UnitCost = movement.UnitCost
                    };
                    if (movement.QuantityChange > 0)
                    {
                        row.QuantityIn = movement.QuantityChange;
                        row.MovementValue = movement.QuantityChange * movement.UnitCost;
                        runningValue += row.MovementValue;
                        result.TotalIn += movement.QuantityChange;
                    }
                    else
                    {
                        row.QuantityOut = Math.Abs(movement.QuantityChange);
                        row.UnitCost = movement.AverageCostBefore;
                        row.MovementValue = Math.Abs(movement.QuantityChange) * movement.AverageCostBefore;
                        runningValue -= row.MovementValue;
                        result.TotalOut += Math.Abs(movement.QuantityChange);
                    }

                    row.RunningBalance = movement.QuantityAfter;
                    row.RunningBalanceValue = runningValue;
                    result.Balance = movement.QuantityAfter;
                    result.BalanceValue = runningValue;
                    result.Rows.Add(row);
                }

                return result;
            }
        }
    }

    public class InventoryValuationResult
    {
        public List<InventoryValuationItemDto> Items { get; set; } = new List<InventoryValuationItemDto>();
        public int TotalItems { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class InventoryValuationItemDto { public int ProductId { get; set; } public string Barcode { get; set; } public string ProductName { get; set; } public string CategoryName { get; set; } public string WarehouseName { get; set; } public int Quantity { get; set; } public decimal AverageCost { get; set; } public decimal TotalValue { get; set; } }

    public class OpeningInventoryItemDto { public int ProductId { get; set; } public string ProductName { get; set; } public string Barcode { get; set; } public int Quantity { get; set; } public decimal UnitCost { get; set; } public decimal TotalValue { get { return Quantity * UnitCost; } } }
    public class OpeningInventorySaveItemDto { public int ProductId { get; set; } public int Quantity { get; set; } public decimal UnitCost { get; set; } }

    public class StockTakeRowDto { public int ProductId { get; set; } public string Barcode { get; set; } public string ProductName { get; set; } public int SystemQuantity { get; set; } public int ActualQuantity { get; set; } public decimal PurchasePrice { get; set; } public int Difference { get { return ActualQuantity - SystemQuantity; } } public decimal DifferenceValue { get { return Difference * PurchasePrice; } } }
    public class StockTakeSaveItemDto { public int ProductId { get; set; } public int SystemQuantity { get; set; } public int ActualQuantity { get; set; } public decimal PurchasePrice { get; set; } }

    public class ProductStockInfoDto { public int ProductId { get; set; } public int CurrentStock { get; set; } public decimal Cost { get; set; } }
    public class StockLossSaveItemDto { public int ProductId { get; set; } public string ProductName { get; set; } public int Quantity { get; set; } public decimal Cost { get; set; } }
    public class StockTransferSaveItemDto { public int ProductId { get; set; } public string ProductName { get; set; } public int Quantity { get; set; } }

    public class StockCardResult
    {
        public List<StockCardRowDto> Rows { get; set; } = new List<StockCardRowDto>();
        public int TotalIn { get; set; }
        public int TotalOut { get; set; }
        public int Balance { get; set; }
        public decimal BalanceValue { get; set; }
    }

    public class StockCardRowDto { public DateTime Date { get; set; } public string MovementType { get; set; } public string Reference { get; set; } public string WarehouseName { get; set; } public int QuantityIn { get; set; } public int QuantityOut { get; set; } public int RunningBalance { get; set; } public decimal UnitCost { get; set; } public decimal MovementValue { get; set; } public decimal RunningBalanceValue { get; set; } }
}
