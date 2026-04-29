using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class AuthorizationService
    {
        private readonly Func<AppDbContext> _dbFactory;

        public AuthorizationService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public bool HasPermission(int userId, string permissionKey, int branchId)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(permissionKey) || branchId <= 0)
                return false;

            using (var db = _dbFactory())
            {
                var user = db.Users.Where(u => u.Id == userId && u.IsActive)
                    .Select(u => new { u.Role, u.BranchId, u.RoleId }).FirstOrDefault();
                if (user == null) return false;
                if (user.Role == "Admin") return true;
                if (user.BranchId != branchId) return false;

                if (!user.RoleId.HasValue) return false;

                var hasRolePermission = db.RolePermissions
                    .Where(rp => rp.RoleId == user.RoleId.Value && rp.Role.IsActive && rp.Permission.Key == permissionKey)
                    .Any();
                if (hasRolePermission) return true;

                return db.UserBranchPermissions
                    .Join(db.Permissions, ubp => ubp.PermissionId, p => p.Id, (ubp, p) => new { ubp.UserId, ubp.BranchId, p.Key })
                    .Any(x => x.UserId == userId && x.BranchId == branchId && x.Key == permissionKey);
            }
        }

        public bool HasPermission(int userId, string permissionKey, int? branchId)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(permissionKey)) return false;
            if (branchId.HasValue && branchId.Value > 0) return HasPermission(userId, permissionKey, branchId.Value);
            return HasPermissionRoleOnly(userId, permissionKey);
        }

        public bool HasPermission(int userId, string permissionKey) => HasPermission(userId, permissionKey, null);

        private bool HasPermissionRoleOnly(int userId, string permissionKey)
        {
            using (var db = _dbFactory())
            {
                return db.Users.Where(u => u.Id == userId && u.IsActive && u.RoleId != null)
                    .Join(db.Roles, u => u.RoleId, r => r.Id, (u, r) => r).Where(r => r.IsActive)
                    .Join(db.RolePermissions, r => r.Id, rp => rp.RoleId, (r, rp) => rp)
                    .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Key)
                    .Any(key => key == permissionKey);
            }
        }

        public HashSet<string> GetUserPermissions(int userId)
        {
            if (userId <= 0) return new HashSet<string>();
            using (var db = _dbFactory())
            {
                var permissions = db.Users.Where(u => u.Id == userId && u.IsActive && u.RoleId != null)
                    .Join(db.Roles, u => u.RoleId, r => r.Id, (u, r) => r).Where(r => r.IsActive)
                    .Join(db.RolePermissions, r => r.Id, rp => rp.RoleId, (r, rp) => rp)
                    .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Key).ToList();
                return new HashSet<string>(permissions);
            }
        }

        public void DemandPermission(int userId, string permissionKey)
        {
            if (!HasPermission(userId, permissionKey))
                throw new UnauthorizedAccessException($"User {userId} lacks permission '{permissionKey}'");
        }

        public Dictionary<string, List<PermissionInfo>> GetAllPermissionsGrouped()
        {
            using (var db = _dbFactory())
            {
                return db.Permissions.OrderBy(p => p.GroupName).ThenBy(p => p.Name).ToList()
                    .GroupBy(p => p.GroupName ?? "أخرى")
                    .ToDictionary(g => g.Key, g => g.Select(p => new PermissionInfo { Id = p.Id, Key = p.Key, Name = p.Name }).ToList());
            }
        }

        public void GrantPermissionToRole(int roleId, int permissionId)
        {
            using (var db = _dbFactory())
            {
                if (!db.RolePermissions.Any(rp => rp.RoleId == roleId && rp.PermissionId == permissionId))
                {
                    db.RolePermissions.Add(new Core.Entities.RolePermission { RoleId = roleId, PermissionId = permissionId });
                    db.SaveChanges();
                }
            }
        }

        public void RevokePermissionFromRole(int roleId, int permissionId)
        {
            using (var db = _dbFactory())
            {
                var rp = db.RolePermissions.FirstOrDefault(x => x.RoleId == roleId && x.PermissionId == permissionId);
                if (rp != null) { db.RolePermissions.Remove(rp); db.SaveChanges(); }
            }
        }

        public HashSet<string> GetUserBranchPermissions(int userId, int branchId)
        {
            var permissions = new HashSet<string>();
            if (userId <= 0 || branchId <= 0) return permissions;
            using (var db = _dbFactory())
            {
                var user = db.Users.FirstOrDefault(u => u.Id == userId && u.IsActive);
                if (user == null) return permissions;
                if (user.Role == "Admin") return new HashSet<string>(db.Permissions.Select(p => p.Key));
                if (user.BranchId != branchId) return permissions;
                var rolePermissions = db.RolePermissions.Where(rp => rp.RoleId == user.RoleId && rp.Role.IsActive)
                    .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Key);
                foreach (var key in rolePermissions) permissions.Add(key);
                var branchPermissions = db.UserBranchPermissions.Where(ubp => ubp.UserId == userId && ubp.BranchId == branchId)
                    .Join(db.Permissions, ubp => ubp.PermissionId, p => p.Id, (ubp, p) => p.Key);
                foreach (var key in branchPermissions) permissions.Add(key);
            }
            return permissions;
        }
    }

    public class PermissionInfo { public int Id { get; set; } public string Key { get; set; } public string Name { get; set; } }
}