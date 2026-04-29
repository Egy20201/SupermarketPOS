using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class ShiftClosing
    {
        [Key]
        public int Id { get; set; }
        public DateTime ShiftDate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int UserId { get; set; }
        public virtual User User { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalCashSales { get; set; }
        public decimal TotalCreditSales { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal ExpectedCashInDrawer { get; set; }
        public decimal ActualCashInDrawer { get; set; }
        public decimal CashDifference { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}