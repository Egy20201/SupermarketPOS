using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class UnitService
    {
        public List<UnitDto> GetAll()
        {
            using (var db = new AppDbContext())
            {
                return db.Units
                    .OrderBy(u => u.Name)
                    .Select(u => new UnitDto
                    {
                        Id = u.Id,
                        Name = u.Name,
                        Type = u.Type,
                        ConversionFactor = u.ConversionFactor
                    })
                    .ToList();
            }
        }

        public UnitSaveResult Save(UnitSaveRequest request)
        {
            using (var db = new AppDbContext())
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Name) || request.ConversionFactor <= 0m)
                {
                    return UnitSaveResult.Fail("#/.D 'D(J'F'* 'DE7DH()");
                }

                var unitName = request.Name.Trim();
                var duplicate = db.Units.Any(u => u.Name == unitName && u.Id != (request.UnitId ?? 0));
                if (duplicate)
                {
                    return UnitSaveResult.Fail("'DH-/) EH,H/)");
                }

                if (request.UnitId.HasValue)
                {
                    var existing = db.Units.Find(request.UnitId.Value);
                    if (existing == null)
                    {
                        return UnitSaveResult.Fail("'DH-/) :J1 EH,H/)");
                    }

                    existing.Name = unitName;
                    existing.Type = request.Type;
                    existing.ConversionFactor = request.ConversionFactor;
                    db.SaveChanges();
                    return UnitSaveResult.Ok(existing.Id);
                }

                var unit = new Unit
                {
                    Name = unitName,
                    Type = request.Type,
                    ConversionFactor = request.ConversionFactor
                };
                db.Units.Add(unit);
                db.SaveChanges();
                return UnitSaveResult.Ok(unit.Id);
            }
        }

        public bool Delete(int unitId)
        {
            using (var db = new AppDbContext())
            {
                if (db.Products.Any(p => p.UnitId == unitId))
                {
                    return false;
                }

                var unit = db.Units.Find(unitId);
                if (unit == null)
                {
                    return false;
                }

                db.Units.Remove(unit);
                db.SaveChanges();
                return true;
            }
        }
    }

    public class UnitDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public decimal ConversionFactor { get; set; }
    }

    public class UnitSaveRequest
    {
        public int? UnitId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public decimal ConversionFactor { get; set; }
    }

    public class UnitSaveResult
    {
        public bool Success { get; set; }
        public int? UnitId { get; set; }
        public string ErrorMessage { get; set; }

        public static UnitSaveResult Ok(int unitId)
        {
            return new UnitSaveResult { Success = true, UnitId = unitId };
        }

        public static UnitSaveResult Fail(string errorMessage)
        {
            return new UnitSaveResult { Success = false, ErrorMessage = errorMessage };
        }
    }
}

