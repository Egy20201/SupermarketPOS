using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class PlatformSetting
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Key { get; set; }

        [MaxLength(2000)]
        public string Value { get; set; }

        [MaxLength(100)]
        public string Scope { get; set; } = "Global";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
