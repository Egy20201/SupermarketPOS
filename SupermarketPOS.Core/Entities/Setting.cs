using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class Setting
    {
        public int Id { get; set; }
        [Required, MaxLength(100)]
        public string Key { get; set; }
        public string Value { get; set; }
        [MaxLength(20)]
        public string DataType { get; set; } = "string";
        [MaxLength(100)]
        public string GroupName { get; set; }
        [MaxLength(500)]
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}