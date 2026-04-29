using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class JournalEntryLine
    {
        public int Id { get; set; }

        [Required]
        public int JournalEntryId { get; set; }
        public virtual JournalEntry JournalEntry { get; set; }

        [Required]
        public int AccountId { get; set; }
        public virtual Account Account { get; set; }

        [MaxLength(200)]
        public string Description { get; set; }

        public decimal Debit { get; set; } // المبلغ المدين
        public decimal Credit { get; set; } // المبلغ الدائن
    }
}