using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class PurchaseWorkflowService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly DocumentNumberingService _numbering;
        private readonly AuditService _auditService;
        private readonly DomainEventService _domainEvents;

        public PurchaseWorkflowService(Func<AppDbContext> dbFactory, DocumentNumberingService numbering, AuditService auditService, DomainEventService domainEvents)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _numbering = numbering ?? throw new ArgumentNullException(nameof(numbering));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _domainEvents = domainEvents ?? throw new ArgumentNullException(nameof(domainEvents));
        }

        public PurchaseOrder CreatePurchaseOrder(int userId, int supplierId, int? branchId, List<(int productId, int qty, decimal price)> itemsList, string notes)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var order = new PurchaseOrder
                    {
                        Number = _numbering.GenerateNumber("PO-", "dbo.PurchaseOrderSeq"),
                        Date = DateTime.Now,
                        SupplierId = supplierId,
                        UserId = userId,
                        BranchId = branchId,
                        Status = DocumentStatus.Draft,
                        Notes = notes,
                        Items = itemsList.Select(i => new PurchaseOrderItem { ProductId = i.productId, Quantity = i.qty, UnitPrice = i.price, TotalPrice = i.qty * i.price }).ToList()
                    };
                    order.Subtotal = order.Items.Sum(i => i.TotalPrice); order.TotalAmount = order.Subtotal;
                    order.Validate();
                    _auditService.Log("CREATE_PURCHASE_ORDER", "PurchaseOrder", order.Id, userId);
                    db.SaveChanges(); tx.Commit();
                    return order;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public void ConfirmPurchaseOrder(int orderId, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var order = db.PurchaseOrders.Find(orderId);
                    if (order == null) throw new InvalidOperationException("Purchase order not found");
                    _auditService.Log("CONFIRM_PO", "PurchaseOrder", orderId, userId);
                    db.SaveChanges(); tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public GoodsReceipt CreateGoodsReceipt(int userId, int? branchId, int purchaseOrderId, int warehouseId, List<(int productId, int qty)> itemsList)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var receipt = new GoodsReceipt
                    {
                        Number = _numbering.GenerateNumber("GR-", "dbo.GoodsReceiptSeq"),
                        Date = DateTime.Now,
                        PurchaseOrderId = purchaseOrderId,
                        WarehouseId = warehouseId,
                        UserId = userId,
                        BranchId = branchId,
                        Status = DocumentStatus.Draft,
                        Items = itemsList.Select(i => new GoodsReceiptItem { ProductId = i.productId, Quantity = i.qty }).ToList()
                    };
                    receipt.Validate();
                    _auditService.Log("CREATE_GOODS_RECEIPT", "GoodsReceipt", receipt.Id, userId);
                    db.SaveChanges(); tx.Commit();
                    return receipt;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public void PostGoodsReceipt(int receiptId, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var receipt = db.GoodsReceipts.Include(g => g.Items).FirstOrDefault(g => g.Id == receiptId);
                    if (receipt == null) throw new InvalidOperationException("Goods receipt not found");

                    // Idempotency check
                    if (receipt.Status == DocumentStatus.Posted)
                    {
                        tx.Commit();
                        return;
                    }

                    if (receipt.Status != DocumentStatus.Confirmed) throw new InvalidOperationException($"Cannot post. Status is {receipt.Status}. Must be Confirmed.");
                    WorkflowEngine.Transition(receipt, DocumentStatus.Posted);
                    _domainEvents.OnGoodsReceiptPosted(db, receipt);
                    _auditService.Log("POST_GOODS_RECEIPT", "GoodsReceipt", receiptId, userId);
                    db.SaveChanges(); tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public DebitNote CreateDebitNote(int userId, int? supplierId, int? branchId, int? purchaseInvoiceId, string reason, List<(int productId, int qty, decimal price)> itemsList)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var note = new DebitNote
                    {
                        Number = _numbering.GenerateNumber("DN-", "dbo.DebitNoteSeq"),
                        Date = DateTime.Now,
                        SupplierId = supplierId,
                        PurchaseInvoiceId = purchaseInvoiceId,
                        UserId = userId,
                        BranchId = branchId,
                        Status = DocumentStatus.Draft,
                        Reason = reason,
                        Items = itemsList.Select(i => new DebitNoteItem { ProductId = i.productId, Quantity = i.qty, UnitPrice = i.price, TotalPrice = i.qty * i.price }).ToList()
                    };
                    note.TotalAmount = note.Items.Sum(i => i.TotalPrice);
                    note.Validate();
                    _auditService.Log("CREATE_DEBIT_NOTE", "DebitNote", note.Id, userId);
                    db.SaveChanges(); tx.Commit();
                    return note;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public void PostDebitNote(int debitNoteId, int warehouseId, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var note = db.DebitNotes.Include(d => d.Items).FirstOrDefault(d => d.Id == debitNoteId);
                    if (note == null) throw new InvalidOperationException("Debit note not found");

                    // Idempotency check
                    if (note.Status == DocumentStatus.Posted)
                    {
                        tx.Commit();
                        return;
                    }

                    if (note.Status != DocumentStatus.Approved) throw new InvalidOperationException($"Cannot post. Status is {note.Status}. Must be Approved.");
                    WorkflowEngine.Transition(note, DocumentStatus.Posted);
                    _domainEvents.OnDebitNotePosted(db, note, warehouseId);
                    _auditService.Log("POST_DEBIT_NOTE", "DebitNote", debitNoteId, userId);
                    db.SaveChanges(); tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
        }
    }
}