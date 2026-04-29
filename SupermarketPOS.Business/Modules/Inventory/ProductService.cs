using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class ProductService
    {
        private readonly TransactionExecutor transactionExecutor;

        public ProductService(TransactionExecutor transactionExecutor)
        {
            this.transactionExecutor = transactionExecutor ?? throw new ArgumentNullException(nameof(transactionExecutor));
        }

        private const string RequiredDataMessage = "أدخل البيانات المطلوبة";
        private const string DuplicateBarcodeMessage = "الباركود موجود";
        private const string ProductNotFoundMessage = "المنتج غير موجود";

        public List<ProductCategoryDto> GetCategories()
        {
            using (var db = new AppDbContext())
            {
                return db.Categories
                    .OrderBy(c => c.Name)
                    .Select(c => new ProductCategoryDto
                    {
                        Id = c.Id,
                        Name = c.Name
                    })
                    .ToList();
            }
        }

        public List<ProductListItemDto> GetProducts(string searchText, int categoryId)
        {
            using (var db = new AppDbContext())
            {
                var query = db.Products.Include(p => p.Category).AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var normalized = searchText.Trim().ToLower();
                    query = query.Where(p => p.Name.ToLower().Contains(normalized) || p.Barcode.Contains(normalized));
                }

                if (categoryId > 0)
                {
                    query = query.Where(p => p.CategoryId == categoryId);
                }

                var products = query.OrderBy(p => p.Name).ToList();
                var productIds = products.Select(p => p.Id).ToList();
                var stockLookup = db.ProductStocks
                    .Where(s => productIds.Contains(s.ProductId))
                    .GroupBy(s => s.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

                var result = new List<ProductListItemDto>();
                foreach (var product in products)
                {
                    int stockQuantity;
                    if (!stockLookup.TryGetValue(product.Id, out stockQuantity))
                    {
                        stockQuantity = 0;
                    }

                    result.Add(new ProductListItemDto
                    {
                        Id = product.Id,
                        Name = product.Name,
                        Barcode = product.Barcode,
                        CategoryId = product.CategoryId,
                        CategoryName = product.Category != null ? product.Category.Name : string.Empty,
                        PurchasePrice = product.PurchasePrice,
                        SellingPrice = product.SellingPrice,
                        Unit = product.Unit,
                        ReorderLevel = product.ReorderLevel,
                        StockQuantity = stockQuantity
                    });
                }

                return result;
            }
        }

        public ProductSaveResult Save(ProductSaveRequest request)
        {
            using (var db = new AppDbContext())
            {
                var validation = ValidateSaveRequest(request, db);
                if (!validation.IsValid)
                {
                    return ProductSaveResult.Fail(validation.ErrorMessage);
                }

                var saveCommand = BuildSaveCommand(request);
                return CommitSave(saveCommand, db);
            }
        }

        private ProductSaveValidationResult ValidateSaveRequest(ProductSaveRequest request, AppDbContext db)
        {
            if (request == null)
            {
                return ProductSaveValidationResult.Fail(RequiredDataMessage);
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return ProductSaveValidationResult.Fail(RequiredDataMessage);
            }

            if (request.CategoryId <= 0)
            {
                return ProductSaveValidationResult.Fail(RequiredDataMessage);
            }

            if (request.PurchasePrice < 0m || request.SellingPrice < 0m || request.ReorderLevel < 0)
            {
                return ProductSaveValidationResult.Fail(RequiredDataMessage);
            }

            if (!db.Categories.Any(c => c.Id == request.CategoryId))
            {
                return ProductSaveValidationResult.Fail(RequiredDataMessage);
            }

            if (!request.ProductId.HasValue)
            {
                if (db.Products.Any(p => p.Barcode == request.Barcode))
                {
                    return ProductSaveValidationResult.Fail(DuplicateBarcodeMessage);
                }
            }
            else if (!db.Products.Any(p => p.Id == request.ProductId.Value))
            {
                return ProductSaveValidationResult.Fail(ProductNotFoundMessage);
            }

            return ProductSaveValidationResult.Valid();
        }

        private ProductSaveCommand BuildSaveCommand(ProductSaveRequest request)
        {
            return new ProductSaveCommand
            {
                ProductId = request.ProductId,
                IsCreate = !request.ProductId.HasValue,
                Name = request.Name,
                Barcode = request.Barcode,
                CategoryId = request.CategoryId,
                PurchasePrice = request.PurchasePrice,
                SellingPrice = request.SellingPrice,
                Unit = request.Unit,
                UnitId = request.UnitId,
                ReorderLevel = request.ReorderLevel,
                Units = request.Units
            };
        }

        /// <summary>
        /// Commits product save as a single atomic operation.
        /// ALL entity modifications are queued in the DbContext change tracker.
        /// Only ONE SaveChanges() at the very end commits everything together.
        /// 
        /// If any part of the operation fails, nothing is persisted.
        /// </summary>
        private ProductSaveResult CommitSave(ProductSaveCommand command, AppDbContext db)
        {
            if (command.IsCreate)
            {
                // ── QUEUE ALL OPERATIONS (no intermediate saves) ──

                // 1. Queue product creation
                var newProduct = BuildNewProductEntity(command);
                db.Products.Add(newProduct);

                // 2. Queue stock record creation (uses navigation property, EF resolves FK)
                var defaultWarehouse = db.Warehouses.FirstOrDefault(w => w.IsDefault);
                if (defaultWarehouse != null)
                {
                    db.ProductStocks.Add(new ProductStock
                    {
                        Product = newProduct,
                        WarehouseId = defaultWarehouse.Id,
                        Quantity = 0,
                        AverageCost = command.PurchasePrice
                    });
                }

                // 3. Queue unit operations
                QueueProductUnitChanges(newProduct, command.Units, db);

                // 4. Determine base unit and queue product field updates
                QueueBaseUnitSync(newProduct, command.Units, db);

                // ── SINGLE ATOMIC COMMIT ──
                db.SaveChanges();
                return ProductSaveResult.Ok(newProduct.Id);
            }
            else
            {
                // ── UPDATE PATH: queue all changes, single commit ──

                var existingProduct = db.Products.Find(command.ProductId.Value);
                ApplyProductUpdates(existingProduct, command);

                // Queue unit changes (uses productId directly since product already exists)
                QueueProductUnitChangesByProductId(existingProduct.Id, command.Units, db);

                // Queue base unit sync
                QueueBaseUnitSyncByProductId(existingProduct.Id, command.Units, db);

                // ── SINGLE ATOMIC COMMIT ──
                db.SaveChanges();
                return ProductSaveResult.Ok(existingProduct.Id);
            }
        }

        private Product BuildNewProductEntity(ProductSaveCommand command)
        {
            return new Product
            {
                Name = command.Name,
                Barcode = command.Barcode,
                CategoryId = command.CategoryId,
                PurchasePrice = command.PurchasePrice,
                SellingPrice = command.SellingPrice,
                Unit = command.Unit,
                UnitId = command.UnitId,
                ReorderLevel = command.ReorderLevel,
                IsActive = true
            };
        }

        /// <summary>
        /// Queues unit changes in the DbContext change tracker WITHOUT calling SaveChanges.
        /// For NEW products (no Id yet), uses navigation property.
        /// </summary>
        private void QueueProductUnitChanges(Product newProduct, List<ProductUnitSaveRequest> units, AppDbContext db)
        {
            if (units == null || !units.Any()) return;

            foreach (var unit in units)
            {
                db.ProductUnits.Add(new ProductUnit
                {
                    Product = newProduct,
                    UnitId = unit.UnitId,
                    ConversionFactor = unit.ConversionFactor,
                    PurchasePrice = unit.PurchasePrice,
                    SellingPrice = unit.SellingPrice,
                    IsBaseUnit = unit.IsBaseUnit,
                    Barcode = string.IsNullOrWhiteSpace(unit.Barcode) ? null : unit.Barcode.Trim()
                });
            }
        }

        /// <summary>
        /// Queues unit changes for EXISTING products using productId directly.
        /// </summary>
        private void QueueProductUnitChangesByProductId(int productId, List<ProductUnitSaveRequest> units, AppDbContext db)
        {
            if (units == null || !units.Any()) return;

            // Queue removal of existing units
            var existing = db.ProductUnits.Where(pu => pu.ProductId == productId).ToList();
            if (existing.Any())
            {
                db.ProductUnits.RemoveRange(existing);
            }

            // Queue new units
            foreach (var unit in units)
            {
                db.ProductUnits.Add(new ProductUnit
                {
                    ProductId = productId,
                    UnitId = unit.UnitId,
                    ConversionFactor = unit.ConversionFactor,
                    PurchasePrice = unit.PurchasePrice,
                    SellingPrice = unit.SellingPrice,
                    IsBaseUnit = unit.IsBaseUnit,
                    Barcode = string.IsNullOrWhiteSpace(unit.Barcode) ? null : unit.Barcode.Trim()
                });
            }
        }

        /// <summary>
        /// Queues base-unit field updates on the product entity (no SaveChanges).
        /// For NEW products — sets fields on the in-memory entity before commit.
        /// </summary>
        private void QueueBaseUnitSync(Product product, List<ProductUnitSaveRequest> units, AppDbContext db)
        {
            if (units == null || !units.Any()) return;

            var baseUnitRequest = units.FirstOrDefault(u => u.IsBaseUnit);
            if (baseUnitRequest == null) return;

            var unitEntity = db.Units.Find(baseUnitRequest.UnitId);

            product.Unit = unitEntity?.Name ?? string.Empty;
            product.UnitId = baseUnitRequest.UnitId;
            product.PurchasePrice = baseUnitRequest.PurchasePrice;
            product.SellingPrice = baseUnitRequest.SellingPrice;
        }

        /// <summary>
        /// Queues base-unit field updates for EXISTING products.
        /// </summary>
        private void QueueBaseUnitSyncByProductId(int productId, List<ProductUnitSaveRequest> units, AppDbContext db)
        {
            if (units == null || !units.Any()) return;

            var baseUnitRequest = units.FirstOrDefault(u => u.IsBaseUnit);
            if (baseUnitRequest == null) return;

            var product = db.Products.Find(productId);
            if (product == null) return;

            var unitEntity = db.Units.Find(baseUnitRequest.UnitId);

            product.Unit = unitEntity?.Name ?? string.Empty;
            product.UnitId = baseUnitRequest.UnitId;
            product.PurchasePrice = baseUnitRequest.PurchasePrice;
            product.SellingPrice = baseUnitRequest.SellingPrice;
        }

        private void ApplyProductUpdates(Product product, ProductSaveCommand command)
        {
            product.Name = command.Name;
            product.Barcode = command.Barcode;
            product.CategoryId = command.CategoryId;
            product.PurchasePrice = command.PurchasePrice;
            product.SellingPrice = command.SellingPrice;
            product.Unit = command.Unit;
            product.UnitId = command.UnitId;
            product.ReorderLevel = command.ReorderLevel;
        }

        public ProductBulkSaveResult SaveBulk(List<ProductSaveRequest> requests)
        {
            try
            {
                var bulkResult = transactionExecutor.Execute(
                    db =>
                    {
                        var validation = ValidateBulk(requests, db);
                        if (!validation.IsValid)
                        {
                            return ProductBulkSaveResult.Fail(validation.Rows);
                        }

                        var command = ProcessBulk(validation, db);
                        return CommitBulk(command, db);
                    },
                    result => result != null && result.Success);

                return bulkResult ?? ProductBulkSaveResult.Fail(new List<ProductRowError>());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "SaveBulk failed");
                return ProductBulkSaveResult.Fail(new List<ProductRowError>
                {
                    new ProductRowError { RowNumber = 0, ErrorMessage = "فشل الحفظ الجماعي" }
                });
            }
        }

        public byte[] ExportToExcel()
        {
            using (var db = new AppDbContext())
            {
                var products = db.Products.Include(p => p.Category).Include(p => p.UnitEntity).OrderBy(p => p.Name).ToList();
                using (var workbook = new XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add("Products");
                    sheet.Cell(1, 1).Value = "Name";
                    sheet.Cell(1, 2).Value = "Category";
                    sheet.Cell(1, 3).Value = "Unit";
                    sheet.Cell(1, 4).Value = "Price";
                    sheet.Cell(1, 5).Value = "Stock";
                    sheet.Cell(1, 6).Value = "Barcode";
                    sheet.Row(1).Style.Font.Bold = true;

                    for (var i = 0; i < products.Count; i++)
                    {
                        var product = products[i];
                        sheet.Cell(i + 2, 1).Value = product.Name;
                        sheet.Cell(i + 2, 2).Value = product.Category != null ? product.Category.Name : string.Empty;
                        sheet.Cell(i + 2, 3).Value = product.Unit;
                        sheet.Cell(i + 2, 4).Value = product.SellingPrice;
                        sheet.Cell(i + 2, 5).Value = db.ProductStocks.Where(s => s.ProductId == product.Id).Sum(s => (int?)s.Quantity) ?? 0;
                        sheet.Cell(i + 2, 6).Value = product.Barcode;
                    }

                    sheet.Columns().AdjustToContents();
                    using (var stream = new System.IO.MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return stream.ToArray();
                    }
                }
            }
        }

        private ProductBulkValidationResult ValidateBulk(List<ProductSaveRequest> requests, AppDbContext db)
        {
            var errors = new List<ProductRowError>();
            var safeRequests = requests ?? new List<ProductSaveRequest>();

            for (var i = 0; i < safeRequests.Count; i++)
            {
                var validation = ValidateSaveRequest(safeRequests[i], db);
                if (!validation.IsValid)
                {
                    errors.Add(new ProductRowError
                    {
                        RowNumber = i + 1,
                        ErrorMessage = validation.ErrorMessage
                    });
                }
            }

            return errors.Any()
                ? ProductBulkValidationResult.Fail(errors)
                : ProductBulkValidationResult.Valid(safeRequests);
        }

        private ProductBulkCommand ProcessBulk(ProductBulkValidationResult validation, AppDbContext db)
        {
            var commands = validation.Requests.Select(BuildSaveCommand).ToList();
            return new ProductBulkCommand
            {
                Commands = commands,
                DefaultWarehouseId = db.Warehouses.Where(w => w.IsDefault).Select(w => (int?)w.Id).FirstOrDefault()
            };
        }

        /// <summary>
        /// Commits bulk product import as a single atomic operation.
        /// ALL products are queued first, then ONE SaveChanges() commits everything.
        /// If any product fails, the entire batch is rolled back.
        /// </summary>
        private ProductBulkSaveResult CommitBulk(ProductBulkCommand command, AppDbContext db)
        {
            foreach (var item in command.Commands)
            {
                if (item.IsCreate)
                {
                    var product = BuildNewProductEntity(item);
                    db.Products.Add(product);

                    if (command.DefaultWarehouseId.HasValue)
                    {
                        db.ProductStocks.Add(new ProductStock
                        {
                            Product = product,
                            WarehouseId = command.DefaultWarehouseId.Value,
                            Quantity = 0,
                            AverageCost = item.PurchasePrice
                        });
                    }
                }
                else
                {
                    var existing = db.Products.Find(item.ProductId.Value);
                    if (existing != null)
                    {
                        ApplyProductUpdates(existing, item);
                    }
                }
            }

            // ── SINGLE ATOMIC COMMIT for entire batch ──
            db.SaveChanges();
            return ProductBulkSaveResult.Ok();
        }

        public bool Delete(int productId)
        {
            using (var db = new AppDbContext())
            {
                var product = db.Products.Find(productId);
                if (product == null)
                {
                    return false;
                }

                db.ProductStocks.RemoveRange(db.ProductStocks.Where(ps => ps.ProductId == productId));
                db.Products.Remove(product);
                db.SaveChanges();
                return true;
            }
        }
    }

    public class ProductCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class ProductListItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Barcode { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public string Unit { get; set; }
        public int ReorderLevel { get; set; }
        public int StockQuantity { get; set; }
    }

    public class ProductSaveRequest
    {
        public int? ProductId { get; set; }
        public string Name { get; set; }
        public string Barcode { get; set; }
        public int CategoryId { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public string Unit { get; set; }
        public int? UnitId { get; set; }
        public int ReorderLevel { get; set; }
        public List<ProductUnitSaveRequest> Units { get; set; }
    }

    public class ProductSaveResult
    {
        public bool Success { get; set; }
        public int? ProductId { get; set; }
        public string ErrorMessage { get; set; }

        public static ProductSaveResult Ok(int productId)
        {
            return new ProductSaveResult { Success = true, ProductId = productId };
        }

        public static ProductSaveResult Fail(string error)
        {
            return new ProductSaveResult { Success = false, ErrorMessage = error };
        }
    }

    internal class ProductSaveValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorMessage { get; private set; }

        public static ProductSaveValidationResult Valid()
        {
            return new ProductSaveValidationResult { IsValid = true };
        }

        public static ProductSaveValidationResult Fail(string errorMessage)
        {
            return new ProductSaveValidationResult { IsValid = false, ErrorMessage = errorMessage };
        }
    }

    internal class ProductSaveCommand
    {
        public int? ProductId { get; set; }
        public bool IsCreate { get; set; }
        public string Name { get; set; }
        public string Barcode { get; set; }
        public int CategoryId { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public string Unit { get; set; }
        public int? UnitId { get; set; }
        public int ReorderLevel { get; set; }
        public List<ProductUnitSaveRequest> Units { get; set; }
    }

    internal class ProductBulkValidationResult
    {
        public bool IsValid { get; private set; }
        public List<ProductSaveRequest> Requests { get; private set; }
        public List<ProductRowError> Rows { get; private set; }

        public static ProductBulkValidationResult Valid(List<ProductSaveRequest> requests)
        {
            return new ProductBulkValidationResult { IsValid = true, Requests = requests, Rows = new List<ProductRowError>() };
        }

        public static ProductBulkValidationResult Fail(List<ProductRowError> rows)
        {
            return new ProductBulkValidationResult { IsValid = false, Requests = new List<ProductSaveRequest>(), Rows = rows ?? new List<ProductRowError>() };
        }
    }

    internal class ProductBulkCommand
    {
        public List<ProductSaveCommand> Commands { get; set; }
        public int? DefaultWarehouseId { get; set; }
    }

    public class ProductBulkSaveResult
    {
        public bool Success { get; set; }
        public List<ProductRowError> Rows { get; set; }

        public static ProductBulkSaveResult Ok()
        {
            return new ProductBulkSaveResult { Success = true, Rows = new List<ProductRowError>() };
        }

        public static ProductBulkSaveResult Fail(List<ProductRowError> rows)
        {
            return new ProductBulkSaveResult { Success = false, Rows = rows ?? new List<ProductRowError>() };
        }
    }

    public class ProductRowError
    {
        public int RowNumber { get; set; }
        public string ErrorMessage { get; set; }
    }
}
