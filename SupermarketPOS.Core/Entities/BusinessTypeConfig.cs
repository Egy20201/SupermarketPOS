using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class BusinessTypeConfig
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        public bool IsDefault { get; set; } = false;

        [MaxLength(2000)]
        public string Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
