using System;
using System.Collections.Generic;

namespace SupermarketPOS.Core.Entities
{
    public class Shift
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public decimal GraceMinutes { get; set; } = 15;
        public bool IsActive { get; set; } = true;
        public virtual ICollection<Employee> Employees { get; set; }
    }
}
