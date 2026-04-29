using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public virtual ICollection<Employee> Employees { get; set; }
    }
}