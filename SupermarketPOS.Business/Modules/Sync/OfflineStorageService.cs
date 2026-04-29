using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class OfflineInvoice
    {
        public string TemporaryId { get; set; }
        public DateTime Date { get; set; }
        public int WarehouseId { get; set; }
        public int UserId { get; set; }
        public int? CustomerId { get; set; }
        public List<OfflineInvoiceItem> Items { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal NetAmount { get; set; }
        public string Status { get; set; }
        public string DeviceId { get; set; }
    }

    public class OfflineInvoiceItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class PendingInvoice
    {
        public OfflineInvoice Invoice { get; set; }
        public string FilePath { get; set; }
        public string TemporaryId { get; set; }
    }

    public static class OfflineStorageService
    {
        private static readonly string StoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SupermarketERP", "OfflineQueue");

        public static List<PendingInvoice> GetPendingInvoicesWithPaths()
        {
            return new List<PendingInvoice>();
        }

        public static void DeleteSyncedInvoices(List<string> ids)
        {
            // التنفيذ
        }
    }
}