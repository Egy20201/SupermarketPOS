using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class CategoryService
    {
        public List<CategoryDto> GetAll()
        {
            using (var db = new AppDbContext())
            {
                return db.Categories
                    .OrderBy(c => c.Name)
                    .Select(c => new CategoryDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Description = c.Description,
                        IsActive = c.IsActive
                    })
                    .ToList();
            }
        }

        public CategorySaveResult Save(CategorySaveRequest request)
        {
            using (var db = new AppDbContext())
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Name))
                {
                    return CategorySaveResult.Fail("#/.D 'D(J'F'* 'DE7DH()");
                }

                var name = request.Name.Trim();
                var duplicate = db.Categories.Any(c => c.Name == name && c.Id != (request.CategoryId ?? 0));
                if (duplicate)
                {
                    return CategorySaveResult.Fail("'D'3E EH,H/");
                }

                if (request.CategoryId.HasValue)
                {
                    var existing = db.Categories.Find(request.CategoryId.Value);
                    if (existing == null)
                    {
                        return CategorySaveResult.Fail("'DA&) :J1 EH,H/)");
                    }

                    existing.Name = name;
                    existing.Description = request.Description;
                    existing.IsActive = request.IsActive;
                    db.SaveChanges();
                    return CategorySaveResult.Ok(existing.Id);
                }

                var category = new Category
                {
                    Name = name,
                    Description = request.Description,
                    IsActive = request.IsActive
                };
                db.Categories.Add(category);
                db.SaveChanges();
                return CategorySaveResult.Ok(category.Id);
            }
        }

        public CategorySaveResult ToggleActive(int categoryId)
        {
            using (var db = new AppDbContext())
            {
                var category = db.Categories.Find(categoryId);
                if (category == null)
                {
                    return CategorySaveResult.Fail("'DA&) :J1 EH,H/)");
                }

                category.IsActive = !category.IsActive;
                db.SaveChanges();
                return CategorySaveResult.Ok(category.Id);
            }
        }

        public bool Delete(int categoryId)
        {
            using (var db = new AppDbContext())
            {
                if (db.Products.Any(p => p.CategoryId == categoryId))
                {
                    return false;
                }

                var category = db.Categories.Find(categoryId);
                if (category == null)
                {
                    return false;
                }

                db.Categories.Remove(category);
                db.SaveChanges();
                return true;
            }
        }
    }

    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
    }

    public class CategorySaveRequest
    {
        public int? CategoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CategorySaveResult
    {
        public bool Success { get; set; }
        public int? CategoryId { get; set; }
        public string ErrorMessage { get; set; }

        public static CategorySaveResult Ok(int categoryId)
        {
            return new CategorySaveResult { Success = true, CategoryId = categoryId };
        }

        public static CategorySaveResult Fail(string errorMessage)
        {
            return new CategorySaveResult { Success = false, ErrorMessage = errorMessage };
        }
    }
}

