using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class JournalEntry
    {
        public int Id { get; set; }

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        [Required, MaxLength(50)]
        public string EntryNumber { get; set; } // رقم القيد (J-2025001)

        [MaxLength(500)]
        public string Description { get; set; } // سبب القيد

        public int? UserId { get; set; }
        public virtual User User { get; set; }

        public string SourceType { get; set; } // Sale, Purchase, Manual, Return...
        public int? SourceId { get; set; } // رقم الفاتورة الأصلية

        public virtual ICollection<JournalEntryLine> Lines { get; set; }
    }
}