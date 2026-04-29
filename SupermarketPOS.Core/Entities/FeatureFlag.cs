using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class FeatureFlag
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Code { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; }

        public bool IsEnabled { get; set; } = true;

        [MaxLength(2000)]
        public string Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
