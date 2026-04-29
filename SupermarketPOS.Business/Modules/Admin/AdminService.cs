using SupermarketPOS.Core.Entities;
using SupermarketPOS.Core.Security;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business.Modules
{
    public class AdminService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly ConfigurationService _configurationService;

        public AdminService(Func<AppDbContext> dbFactory, ConfigurationService configurationService)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        }

        public List<AuditLog> GetAuditLogs(DateTime from, DateTime toExclusive, string actionType, string search)
        {
            search = (search ?? string.Empty).Trim().ToLower();
            using (var db = _dbFactory())
            {
                var query = db.AuditLogs.Where(l => l.Timestamp >= from && l.Timestamp < toExclusive);
                if (!string.IsNullOrWhiteSpace(actionType) && actionType != "الكل")
                    query = query.Where(l => l.ActionType == actionType);
                if (!string.IsNullOrWhiteSpace(search))
                    query = query.Where(l => l.Description.Contains(search) || l.UserName.Contains(search));
                return query.OrderByDescending(l => l.Timestamp).Take(500).ToList();
            }
        }

        public List<UserAdminDto> GetUsers()
        {
            using (var db = _dbFactory())
            {
                return db.Users.OrderBy(u => u.Username)
                    .Select(u => new UserAdminDto
                    {
                        Id = u.Id,
                        Username = u.Username,
                        FullName = u.FullName,
                        Role = u.Role,
                        IsActive = u.IsActive,
                        Status = u.IsActive ? "✅ نشط" : "❌ غير نشط"
                    })
                    .ToList();
            }
        }

        public void SaveUser(UserAdminSaveRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            using (var db = _dbFactory())
            {
                if (request.Id.HasValue)
                {
                    var user = db.Users.Find(request.Id.Value);
                    if (user == null) return;
                    user.Username = request.Username;
                    if (!string.IsNullOrEmpty(request.Password))
                        user.Password = PasswordHasher.HashPassword(request.Password);
                    user.FullName = request.FullName;
                    user.Role = request.Role;
                    user.IsActive = request.IsActive;
                }
                else
                {
                    if (db.Users.Any(u => u.Username == request.Username))
                        throw new InvalidOperationException("اسم المستخدم موجود");
                    if (string.IsNullOrWhiteSpace(request.Password))
                        throw new InvalidOperationException("أدخل كلمة المرور");

                    db.Users.Add(new User
                    {
                        Username = request.Username,
                        Password = PasswordHasher.HashPassword(request.Password),
                        FullName = request.FullName,
                        Role = request.Role,
                        IsActive = request.IsActive
                    });
                }

                db.SaveChanges();
            }
        }

        public void DeleteUser(int userId)
        {
            using (var db = _dbFactory())
            {
                var user = db.Users.Find(userId);
                if (user == null) return;
                db.Users.Remove(user);
                db.SaveChanges();
            }
        }

        public string GetBusinessType()
        {
            using (var db = _dbFactory())
            {
                return db.PlatformSettings
                    .Where(s => s.Key == "business.type" && s.IsActive)
                    .OrderByDescending(s => s.Id)
                    .Select(s => s.Value)
                    .FirstOrDefault() ?? "supermarket";
            }
        }

        public void SaveBusinessType(string selectedValue)
        {
            if (string.IsNullOrWhiteSpace(selectedValue)) throw new ArgumentException("Business type is required.", nameof(selectedValue));
            using (var db = _dbFactory())
            {
                var existing = db.PlatformSettings.FirstOrDefault(s => s.Key == "business.type");
                if (existing == null)
                {
                    db.PlatformSettings.Add(new PlatformSetting
                    {
                        Key = "business.type",
                        Value = selectedValue,
                        Scope = "Global",
                        IsActive = true
                    });
                }
                else
                {
                    existing.Value = selectedValue;
                    existing.IsActive = true;
                }

                db.SaveChanges();
            }

            _configurationService.RefreshConfiguration();
        }

        public List<Role> GetActiveRoles()
        {
            using (var db = _dbFactory())
            {
                return db.Roles.Where(r => r.IsActive).OrderBy(r => r.Name).ToList();
            }
        }

        public List<int> GetRolePermissionIds(int roleId)
        {
            using (var db = _dbFactory())
            {
                return db.RolePermissions.Where(rp => rp.RoleId == roleId).Select(rp => rp.PermissionId).ToList();
            }
        }

        public void AddRole(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName)) return;
            using (var db = _dbFactory())
            {
                db.Roles.Add(new Role { Name = roleName.Trim(), IsActive = true });
                db.SaveChanges();
            }
        }
    }

    public class UserAdminDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public string Status { get; set; }
    }

    public class UserAdminSaveRequest
    {
        public int? Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
    }
}
