using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public virtual ICollection<RolePermission> RolePermissions { get; set; }
        public virtual ICollection<User> Users { get; set; }
    }
}