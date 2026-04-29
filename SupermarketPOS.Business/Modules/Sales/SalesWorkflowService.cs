using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Business
{
    public class SalesWorkflowService
    {
        private readonly Func<AppDbContext> _dbFactory;
        private readonly DocumentNumberingService _numbering;
        private readonly AuditService _auditService;
        private readonly DomainEventService _domainEvents;

        public SalesWorkflowService(Func<AppDbContext> dbFactory, DocumentNumberingService numbering, AuditService auditService, DomainEventService domainEvents)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _numbering = numbering ?? throw new ArgumentNullException(nameof(numbering));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _domainEvents = domainEvents ?? throw new ArgumentNullException(nameof(domainEvents));
        }

        // ── Quotation ──
        public SalesQuotation CreateQuotation(int userId, int? customerId, int? branchId, List<(int productId, int qty, decimal price, decimal discount)> itemsList, string notes)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var q = new SalesQuotation
                    {
                        Number = _numbering.GenerateNumber("Q-", "dbo.QuotationSeq"),
                        Date = DateTime.Now,
                        CustomerId = customerId,
                        UserId = userId,
                        BranchId = branchId,
                        Status = DocumentStatus.Draft,
                        Notes = notes,
                        ValidUntil = DateTime.Now.AddDays(30),
                        Items = itemsList.Select(i => new SalesQuotationItem { ProductId = i.productId, Quantity = i.qty, UnitPrice = i.price, Discount = i.discount, TotalPrice = (i.qty * i.price) - i.discount }).ToList()
                    };
                    q.Subtotal = q.Items.Sum(i => i.TotalPrice); q.TotalAmount = q.Subtotal;
                    q.Validate();
                    _auditService.Log("CREATE_QUOTATION", "SalesQuotation", q.Id, userId);
                    db.SaveChanges(); tx.Commit();
                    return q;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public void UpdateQuotationStatus(int quotationId, DocumentStatus newStatus, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var q = db.SalesQuotations.Find(quotationId);
                    if (q == null) throw new InvalidOperationException("Quotation not found");
                    _auditService.Log("UPDATE_QUOTATION_STATUS", "SalesQuotation", quotationId, userId);
                    db.SaveChanges(); tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
        }

        // ── Sales Order ──
        public SalesOrder CreateOrder(int userId, int? customerId, int? branchId, int? quotationId, List<(int productId, int qty, decimal price)> itemsList, string notes)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var order = new SalesOrder
                    {
                        Number = _numbering.GenerateNumber("SO-", "dbo.SalesOrderSeq"),
                        Date = DateTime.Now,
                        CustomerId = customerId,
                        UserId = userId,
                        BranchId = branchId,
                        QuotationId = quotationId,
                        Status = DocumentStatus.Draft,
                        Notes = notes,
                        Items = itemsList.Select(i => new SalesOrderItem { ProductId = i.productId, Quantity = i.qty, UnitPrice = i.price, TotalPrice = i.qty * i.price }).ToList()
                    };
                    order.Subtotal = order.Items.Sum(i => i.TotalPrice); order.TotalAmount = order.Subtotal;
                    order.Validate();
                    db.SalesOrders.Add(order);
                    if (quotationId.HasValue) { var q = db.SalesQuotations.Find(quotationId.Value); if (q != null) WorkflowEngine.Transition(q, DocumentStatus.Accepted); }
                    _auditService.Log("CREATE_SALES_ORDER", "SalesOrder", order.Id, userId);
                    db.SaveChanges(); tx.Commit();
                    return order;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public void ConfirmOrder(int orderId, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var order = db.SalesOrders.Find(orderId);
                    if (order == null) throw new InvalidOperationException("Order not found");
                    _auditService.Log("CONFIRM_ORDER", "SalesOrder", orderId, userId);
                    db.SaveChanges(); tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
        }

        // ── Delivery ──
        public Delivery CreateDelivery(int userId, int? branchId, int salesOrderId, int warehouseId, List<(int productId, int qty)> itemsList)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var delivery = new Delivery
                    {
                        Number = _numbering.GenerateNumber("DEL-", "dbo.DeliverySeq"),
                        Date = DateTime.Now,
                        SalesOrderId = salesOrderId,
                        WarehouseId = warehouseId,
                        UserId = userId,
                        BranchId = branchId,
                        Status = DocumentStatus.Draft,
                        Items = itemsList.Select(i => new DeliveryItem { ProductId = i.productId, Quantity = i.qty }).ToList()
                    };
                    delivery.Validate();
                    _auditService.Log("CREATE_DELIVERY", "Delivery", delivery.Id, userId);
                    db.SaveChanges(); tx.Commit();
                    return delivery;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public void PostDelivery(int deliveryId, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
            try
            {
                var delivery = db.Deliveries.Include(d => d.Items).FirstOrDefault(d => d.Id == deliveryId);
                if (delivery == null) throw new InvalidOperationException("Delivery not found");

                // Idempotency check: already posted?
                if (delivery.Status == DocumentStatus.Posted)
                {
                    tx.Commit(); // Nothing to do — already posted
                    return;
                }

                if (delivery.Status != DocumentStatus.Confirmed)
                    throw new InvalidOperationException($"Cannot post. Status is {delivery.Status}. Must be Confirmed.");

                // Validate stock availability
                foreach (var item in delivery.Items)
                {
                    var stock = db.ProductStocks.FirstOrDefault(
                        ps => ps.ProductId == item.ProductId && ps.WarehouseId == delivery.WarehouseId);
                    if (stock == null || stock.Quantity < item.Quantity)
                        throw new InvalidOperationException(
                            $"Insufficient stock for product {item.ProductId}. Available: {stock?.Quantity ?? 0}, Required: {item.Quantity}");
                }

                WorkflowEngine.Transition(delivery, DocumentStatus.Posted);
                _domainEvents.OnDeliveryPosted(db, delivery);
                _auditService.Log("POST_DELIVERY", "Delivery", deliveryId, userId);

                db.SaveChanges();
                tx.Commit();
            }
                catch { tx.Rollback(); throw; }
            }
        }

        // ── Credit Note ──
        public CreditNote CreateCreditNote(int userId, int? customerId, int? branchId, int? saleInvoiceId, string reason, List<(int productId, int qty, decimal price)> itemsList)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var note = new CreditNote
                    {
                        Number = _numbering.GenerateNumber("CN-", "dbo.CreditNoteSeq"),
                        Date = DateTime.Now,
                        CustomerId = customerId,
                        SaleInvoiceId = saleInvoiceId,
                        UserId = userId,
                        BranchId = branchId,
                        Status = DocumentStatus.Draft,
                        Reason = reason,
                        Items = itemsList.Select(i => new CreditNoteItem { ProductId = i.productId, Quantity = i.qty, UnitPrice = i.price, TotalPrice = i.qty * i.price }).ToList()
                    };
                    note.TotalAmount = note.Items.Sum(i => i.TotalPrice);
                    note.Validate();
                    _auditService.Log("CREATE_CREDIT_NOTE", "CreditNote", note.Id, userId);
                    db.SaveChanges(); tx.Commit();
                    return note;
                }
                catch { tx.Rollback(); throw; }
            }
        }

        public void PostCreditNote(int creditNoteId, int warehouseId, int userId)
        {
            using (var db = _dbFactory())
            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    var note = db.CreditNotes.Include(c => c.Items).FirstOrDefault(c => c.Id == creditNoteId);
                    if (note == null) throw new InvalidOperationException("Credit note not found");

                    // Idempotency check
                    if (note.Status == DocumentStatus.Posted)
                    {
                        tx.Commit();
                        return;
                    }

                    if (note.Status != DocumentStatus.Approved) throw new InvalidOperationException($"Cannot post. Status is {note.Status}. Must be Approved.");
                    WorkflowEngine.Transition(note, DocumentStatus.Posted);
                    _domainEvents.OnCreditNotePosted(db, note, warehouseId);
                    _auditService.Log("POST_CREDIT_NOTE", "CreditNote", creditNoteId, userId);
                    db.SaveChanges(); tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
        }
    }
}