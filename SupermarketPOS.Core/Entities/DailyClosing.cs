using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class DailyClosing
    {
        [Key]
        public int Id { get; set; }
        public DateTime ClosingDate { get; set; }
        public int ClosedById { get; set; }
        public virtual User ClosedBy { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalCashSales { get; set; }
        public decimal TotalCreditSales { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal ExpectedCashInDrawer { get; set; }
        public decimal ActualCashInDrawer { get; set; }
        public decimal CashDifference { get; set; }
        public string Notes { get; set; }
        public bool IsClosed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}