using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class Expense
    {
        [Key]
        public int Id { get; set; }
        public DateTime ExpenseDate { get; set; } = DateTime.Now;
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string ExpenseType { get; set; }
        public string ReferenceNumber { get; set; }
        public string Notes { get; set; }
        public int CreatedById { get; set; }
        public virtual User CreatedBy { get; set; }
        public int? JournalEntryId { get; set; }
        public virtual JournalEntry JournalEntry { get; set; }
    }
}