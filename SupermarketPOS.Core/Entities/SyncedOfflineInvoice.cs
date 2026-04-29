using System;
using System.ComponentModel.DataAnnotations;

namespace SupermarketPOS.Core.Entities
{
    public class SyncedOfflineInvoice
    {
        [Key]
        [StringLength(50)]
        public string TemporaryId { get; set; }
        public int SyncedInvoiceId { get; set; }
        [StringLength(50)]
        public string SyncedInvoiceNumber { get; set; }
        public DateTime SyncedAt { get; set; }
        [StringLength(50)]
        public string DeviceId { get; set; }
    }
}