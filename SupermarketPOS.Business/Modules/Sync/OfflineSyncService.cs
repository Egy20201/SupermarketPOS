using System;

namespace SupermarketPOS.Business
{
    public static class OfflineSyncService
    {
        public static SyncResult SyncOfflineInvoice(OfflineInvoice invoice, string deviceId)
        {
            return new SyncResult { Success = true, SyncedInvoiceNumber = Guid.NewGuid().ToString().Substring(0, 8) };
        }
    }

    public class SyncResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string SyncedInvoiceNumber { get; set; }
        public bool AlreadySynced { get; set; }
    }
}