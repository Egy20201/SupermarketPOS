using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using Newtonsoft.Json;
using System;

namespace SupermarketPOS.Business
{
    public class DomainEventService
    {
        public void OnDeliveryPosted(AppDbContext db, Delivery delivery)
        {
            if (delivery == null) throw new ArgumentNullException(nameof(delivery));
            Enqueue(db, nameof(DeliveryPostedEvent), new DeliveryPostedPayload { DeliveryId = delivery.Id });
        }

        public void OnGoodsReceiptPosted(AppDbContext db, GoodsReceipt receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            Enqueue(db, nameof(GoodsReceiptPostedEvent), new GoodsReceiptPostedPayload { ReceiptId = receipt.Id });
        }

        public void OnCreditNotePosted(AppDbContext db, CreditNote creditNote, int warehouseId)
        {
            if (creditNote == null) throw new ArgumentNullException(nameof(creditNote));
            Enqueue(db, nameof(CreditNotePostedEvent), new CreditNotePostedPayload { CreditNoteId = creditNote.Id, WarehouseId = warehouseId });
        }

        public void OnDebitNotePosted(AppDbContext db, DebitNote debitNote, int warehouseId)
        {
            if (debitNote == null) throw new ArgumentNullException(nameof(debitNote));
            Enqueue(db, nameof(DebitNotePostedEvent), new DebitNotePostedPayload { DebitNoteId = debitNote.Id, WarehouseId = warehouseId });
        }

        private static void Enqueue(AppDbContext db, string eventType, object payload)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));

            db.OutboxEvents.Add(new OutboxEvent
            {
                EventType = eventType,
                Payload = JsonConvert.SerializeObject(payload),
                CreatedAt = DateTime.UtcNow,
                Status = OutboxStatus.Pending
            });
        }
    }

    public static class OutboxStatus
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Processed = "Processed";
        public const string Failed = "Failed";
    }
}
