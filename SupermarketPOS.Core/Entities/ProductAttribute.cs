using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class ProductAttribute
    {
        public int Id { get; set; }
        public int ProductId { get; set; }

        [Required, MaxLength(100)]
        public string Key { get; set; }

        [MaxLength(2000)]
        public string Value { get; set; }

        public Product Product { get; set; }
    }
}
