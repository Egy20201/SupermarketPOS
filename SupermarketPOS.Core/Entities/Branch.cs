using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public bool IsActive { get; set; } = true;
        public virtual ICollection<User> Users { get; set; }
    }
}