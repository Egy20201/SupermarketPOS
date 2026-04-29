using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Permission
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string GroupName { get; set; }
        public virtual ICollection<RolePermission> RolePermissions { get; set; }
    }
}