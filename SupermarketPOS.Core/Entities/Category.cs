using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public virtual ICollection<Product> Products { get; set; }
    }
}