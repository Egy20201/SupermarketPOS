using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;

namespace SupermarketPOS.Business
{
    public interface IDomainEvent
    {
        AppDbContext Db { get; }
    }

    public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
    {
        void Handle(TEvent domainEvent);
    }

    public sealed class DeliveryPostedEvent : IDomainEvent
    {
        public DeliveryPostedEvent(AppDbContext db, Delivery delivery)
        {
            Db = db;
            Delivery = delivery;
        }

        public AppDbContext Db { get; }
        public Delivery Delivery { get; }
    }

    public sealed class DeliveryPostedPayload
    {
        public int DeliveryId { get; set; }
    }

    public sealed class GoodsReceiptPostedEvent : IDomainEvent
    {
        public GoodsReceiptPostedEvent(AppDbContext db, GoodsReceipt receipt)
        {
            Db = db;
            Receipt = receipt;
        }

        public AppDbContext Db { get; }
        public GoodsReceipt Receipt { get; }
    }

    public sealed class GoodsReceiptPostedPayload
    {
        public int ReceiptId { get; set; }
    }

    public sealed class CreditNotePostedEvent : IDomainEvent
    {
        public CreditNotePostedEvent(AppDbContext db, CreditNote creditNote, int warehouseId)
        {
            Db = db;
            CreditNote = creditNote;
            WarehouseId = warehouseId;
        }

        public AppDbContext Db { get; }
        public CreditNote CreditNote { get; }
        public int WarehouseId { get; }
    }

    public sealed class CreditNotePostedPayload
    {
        public int CreditNoteId { get; set; }
        public int WarehouseId { get; set; }
    }

    public sealed class DebitNotePostedEvent : IDomainEvent
    {
        public DebitNotePostedEvent(AppDbContext db, DebitNote debitNote, int warehouseId)
        {
            Db = db;
            DebitNote = debitNote;
            WarehouseId = warehouseId;
        }

        public AppDbContext Db { get; }
        public DebitNote DebitNote { get; }
        public int WarehouseId { get; }
    }

    public sealed class DebitNotePostedPayload
    {
        public int DebitNoteId { get; set; }
        public int WarehouseId { get; set; }
    }
}
