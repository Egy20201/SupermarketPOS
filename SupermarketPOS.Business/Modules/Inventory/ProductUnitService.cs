using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class ProductUnitService
    {
        public List<ProductUnitDto> GetUnitsForProduct(int productId)
        {
            using (var db = new AppDbContext())
            {
                return db.ProductUnits
                    .Include(pu => pu.Unit)
                    .Where(pu => pu.ProductId == productId)
                    .OrderByDescending(pu => pu.IsBaseUnit)
                    .ThenBy(pu => pu.Unit.Name)
                    .Select(pu => new ProductUnitDto
                    {
                        Id = pu.Id,
                        ProductId = pu.ProductId,
                        UnitId = pu.UnitId,
                        UnitName = pu.Unit.Name,
                        ConversionFactor = pu.ConversionFactor,
                        PurchasePrice = pu.PurchasePrice,
                        SellingPrice = pu.SellingPrice,
                        IsBaseUnit = pu.IsBaseUnit,
                        Barcode = pu.Barcode
                    })
                    .ToList();
            }
        }

        public ProductUnitSaveResult SaveUnitsForProduct(int productId, List<ProductUnitSaveRequest> units)
        {
            if (units == null || !units.Any())
            {
                return ProductUnitSaveResult.Fail("يجب إضافة وحدة واحدة على الأقل");
            }

            var baseUnits = units.Count(u => u.IsBaseUnit);
            if (baseUnits == 0)
            {
                return ProductUnitSaveResult.Fail("يجب تحديد وحدة أساسية واحدة");
            }

            if (baseUnits > 1)
            {
                return ProductUnitSaveResult.Fail("يجب أن تكون هناك وحدة أساسية واحدة فقط");
            }

            var baseUnit = units.First(u => u.IsBaseUnit);
            if (baseUnit.ConversionFactor != 1m)
            {
                return ProductUnitSaveResult.Fail("معامل تحويل الوحدة الأساسية يجب أن يكون 1");
            }

            if (units.Any(u => u.ConversionFactor <= 0))
            {
                return ProductUnitSaveResult.Fail("معامل التحويل يجب أن يكون أكبر من صفر");
            }

            if (units.Any(u => u.UnitId <= 0))
            {
                return ProductUnitSaveResult.Fail("يجب اختيار الوحدة");
            }

            using (var db = new AppDbContext())
            {
                if (!db.Products.Any(p => p.Id == productId))
                {
                    return ProductUnitSaveResult.Fail("المنتج غير موجود");
                }

                // Check for duplicate barcodes across all products
                var barcodes = units.Where(u => !string.IsNullOrWhiteSpace(u.Barcode)).Select(u => u.Barcode.Trim()).ToList();
                if (barcodes.Count != barcodes.Distinct().Count())
                {
                    return ProductUnitSaveResult.Fail("لا يمكن تكرار الباركود في نفس المنتج");
                }

                foreach (var barcode in barcodes)
                {
                    var duplicateInProducts = db.Products.Any(p => p.Barcode == barcode && p.Id != productId);
                    var duplicateInUnits = db.ProductUnits.Any(pu => pu.Barcode == barcode && pu.ProductId != productId);
                    if (duplicateInProducts || duplicateInUnits)
                    {
                        return ProductUnitSaveResult.Fail($"الباركود {barcode} مستخدم في منتج آخر");
                    }
                }

                // Remove existing units for this product
                var existing = db.ProductUnits.Where(pu => pu.ProductId == productId).ToList();
                db.ProductUnits.RemoveRange(existing);

                // Add new units
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

                db.SaveChanges();

                // Update the product's base unit info
                var product = db.Products.Find(productId);
                if (product != null)
                {
                    var baseUnitEntity = db.ProductUnits
                        .Include(pu => pu.Unit)
                        .FirstOrDefault(pu => pu.ProductId == productId && pu.IsBaseUnit);
                    if (baseUnitEntity != null)
                    {
                        product.Unit = baseUnitEntity.Unit.Name;
                        product.UnitId = baseUnitEntity.UnitId;
                        product.PurchasePrice = baseUnitEntity.PurchasePrice;
                        product.SellingPrice = baseUnitEntity.SellingPrice;
                        db.SaveChanges();
                    }
                }

                return ProductUnitSaveResult.Ok();
            }
        }

        public ProductUnit FindByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;

            using (var db = new AppDbContext())
            {
                return db.ProductUnits
                    .Include(pu => pu.Product)
                    .Include(pu => pu.Unit)
                    .FirstOrDefault(pu => pu.Barcode == barcode && pu.Product.IsActive);
            }
        }
    }

    public class ProductUnitDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UnitId { get; set; }
        public string UnitName { get; set; }
        public decimal ConversionFactor { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public bool IsBaseUnit { get; set; }
        public string Barcode { get; set; }
    }

    public class ProductUnitSaveRequest
    {
        public int UnitId { get; set; }
        public string UnitName { get; set; }
        public decimal ConversionFactor { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public bool IsBaseUnit { get; set; }
        public string Barcode { get; set; }
    }

    public class ProductUnitSaveResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static ProductUnitSaveResult Ok()
        {
            return new ProductUnitSaveResult { Success = true };
        }

        public static ProductUnitSaveResult Fail(string error)
        {
            return new ProductUnitSaveResult { Success = false, ErrorMessage = error };
        }
    }
}
