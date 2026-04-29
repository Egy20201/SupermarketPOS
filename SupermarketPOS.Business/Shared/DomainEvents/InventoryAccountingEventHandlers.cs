using SupermarketPOS.Core.Entities;
using SupermarketPOS.Data;
using System;
using System.Linq;

namespace SupermarketPOS.Business
{
    public sealed class DeliveryPostedHandler : IDomainEventHandler<DeliveryPostedEvent>
    {
        private readonly IInventoryMovementService _inventoryService;
        private readonly AuditService _auditService;
        private readonly SettingsService _settingsService;

        public DeliveryPostedHandler(IInventoryMovementService inventoryService, AuditService auditService, SettingsService settingsService)
        {
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public void Handle(DeliveryPostedEvent domainEvent)
        {
            var db = domainEvent.Db;
            var delivery = domainEvent.Delivery;
            if (delivery == null) throw new ArgumentNullException(nameof(domainEvent.Delivery));
            if (delivery.Status != DocumentStatus.Posted) throw new InvalidOperationException("Only posted deliveries can affect inventory");
            if (db.JournalEntries.Any(j => j.SourceType == "Delivery" && j.SourceId == delivery.Id)) return;

            foreach (var item in delivery.Items)
            {
                string error;
                if (!_inventoryService.DecreaseStockAndRecordMovement(db, item.ProductId, delivery.WarehouseId, item.Quantity, 0, delivery.Number, "تسليم", out error))
                    throw new InvalidOperationException($"Stock deduction failed for product {item.ProductId}: {error}");
            }

            CreateDeliveryJournalEntry(db, delivery);
            _auditService.Log("POST_DELIVERY", "Delivery", delivery.Id, delivery.UserId);
        }

        private void CreateDeliveryJournalEntry(AppDbContext db, Delivery delivery)
        {
            var cogsId = GetAccountId(db, "cogs", "5.1.1");
            var invId = GetAccountId(db, "inventory", "1.1.3");
            var productIds = delivery.Items.Select(i => i.ProductId).Distinct().ToList();
            var productsDict = db.Products.Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id);
            var totalCost = delivery.Items.Sum(item => productsDict.TryGetValue(item.ProductId, out var product) ? item.Quantity * product.PurchasePrice : 0m);
            var journal = new JournalEntry { Date = DateTime.UtcNow, EntryNumber = $"DEL-{delivery.Number}", Description = $"تسليم بضاعة - {delivery.Number}", SourceType = "Delivery", SourceId = delivery.Id, UserId = delivery.UserId };
            db.JournalEntries.Add(journal);
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, AccountId = cogsId, Description = "تكلفة البضاعة المباعة", Debit = totalCost, Credit = 0 });
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, AccountId = invId, Description = "تخفيض المخزون", Debit = 0, Credit = totalCost });
        }

        private int GetAccountId(AppDbContext db, string settingKey, string defaultCode)
        {
            var code = _settingsService.Get($"account.{settingKey}", null) ?? defaultCode;
            var id = db.Accounts.Where(a => a.Code == code).Select(a => (int?)a.Id).FirstOrDefault();
            if (!id.HasValue) throw new InvalidOperationException($"Account with code '{code}' not found. Configure via Settings.");
            return id.Value;
        }
    }

    public sealed class GoodsReceiptPostedHandler : IDomainEventHandler<GoodsReceiptPostedEvent>
    {
        private readonly IInventoryMovementService _inventoryService;
        private readonly AuditService _auditService;
        private readonly SettingsService _settingsService;

        public GoodsReceiptPostedHandler(IInventoryMovementService inventoryService, AuditService auditService, SettingsService settingsService)
        {
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public void Handle(GoodsReceiptPostedEvent domainEvent)
        {
            var db = domainEvent.Db;
            var receipt = domainEvent.Receipt;
            if (receipt == null) throw new ArgumentNullException(nameof(domainEvent.Receipt));
            if (receipt.Status != DocumentStatus.Posted) throw new InvalidOperationException("Only posted goods receipts can affect inventory");
            if (db.JournalEntries.Any(j => j.SourceType == "GoodsReceipt" && j.SourceId == receipt.Id)) return;

            foreach (var item in receipt.Items)
            {
                string error;
                if (!_inventoryService.IncreaseStockAndRecordMovement(db, item.ProductId, receipt.WarehouseId, item.Quantity, 0, receipt.Number, "استلام", out error))
                    throw new InvalidOperationException($"Stock increase failed for product {item.ProductId}: {error}");
            }

            CreateGoodsReceiptJournalEntry(db, receipt);
            _auditService.Log("POST_GOODS_RECEIPT", "GoodsReceipt", receipt.Id, receipt.UserId);
        }

        private void CreateGoodsReceiptJournalEntry(AppDbContext db, GoodsReceipt receipt)
        {
            var invId = GetAccountId(db, "inventory", "1.1.3");
            var payId = GetAccountId(db, "payable", "2.1.1");
            var productIds = receipt.Items.Select(i => i.ProductId).Distinct().ToList();
            var productsDict = db.Products.Where(p => productIds.Contains(p.Id)).ToDictionary(p => p.Id);
            var totalCost = receipt.Items.Sum(item => productsDict.TryGetValue(item.ProductId, out var product) ? item.Quantity * product.PurchasePrice : 0m);
            var journal = new JournalEntry { Date = DateTime.UtcNow, EntryNumber = $"GR-{receipt.Number}", Description = $"استلام بضاعة - {receipt.Number}", SourceType = "GoodsReceipt", SourceId = receipt.Id, UserId = receipt.UserId };
            db.JournalEntries.Add(journal);
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, AccountId = invId, Description = "زيادة المخزون", Debit = totalCost, Credit = 0 });
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, AccountId = payId, Description = "ذمم موردين", Debit = 0, Credit = totalCost });
        }

        private int GetAccountId(AppDbContext db, string settingKey, string defaultCode)
        {
            var code = _settingsService.Get($"account.{settingKey}", null) ?? defaultCode;
            var id = db.Accounts.Where(a => a.Code == code).Select(a => (int?)a.Id).FirstOrDefault();
            if (!id.HasValue) throw new InvalidOperationException($"Account with code '{code}' not found. Configure via Settings.");
            return id.Value;
        }
    }

    public sealed class CreditNotePostedHandler : IDomainEventHandler<CreditNotePostedEvent>
    {
        private readonly IInventoryMovementService _inventoryService;
        private readonly AuditService _auditService;
        private readonly SettingsService _settingsService;

        public CreditNotePostedHandler(IInventoryMovementService inventoryService, AuditService auditService, SettingsService settingsService)
        {
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public void Handle(CreditNotePostedEvent domainEvent)
        {
            var db = domainEvent.Db;
            var creditNote = domainEvent.CreditNote;
            if (creditNote == null) throw new ArgumentNullException(nameof(domainEvent.CreditNote));
            if (creditNote.Status != DocumentStatus.Posted) throw new InvalidOperationException("Only posted credit notes can affect accounting");
            if (db.JournalEntries.Any(j => j.SourceType == "CreditNote" && j.SourceId == creditNote.Id)) return;

            foreach (var item in creditNote.Items)
            {
                string error;
                if (!_inventoryService.IncreaseStockAndRecordMovement(db, item.ProductId, domainEvent.WarehouseId, item.Quantity, item.UnitPrice, creditNote.Number, "مرتجع مبيعات", out error))
                    throw new InvalidOperationException($"Stock return failed for product {item.ProductId}: {error}");
            }

            if (creditNote.CustomerId.HasValue)
            {
                var customer = db.Customers.Find(creditNote.CustomerId.Value);
                if (customer != null) customer.Balance -= creditNote.TotalAmount;
            }

            var journal = new JournalEntry { Date = DateTime.UtcNow, EntryNumber = $"CN-{creditNote.Number}", Description = $"اشعار دائن - {creditNote.Number}", SourceType = "CreditNote", SourceId = creditNote.Id, UserId = creditNote.UserId };
            db.JournalEntries.Add(journal);
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, AccountId = GetAccountId(db, "revenue", "4.1.1"), Description = "مردودات مبيعات", Debit = creditNote.TotalAmount, Credit = 0 });
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, AccountId = GetAccountId(db, "receivable", "1.1.2"), Description = "تخفيض حساب العميل", Debit = 0, Credit = creditNote.TotalAmount });
            _auditService.Log("POST_CREDIT_NOTE", "CreditNote", creditNote.Id, creditNote.UserId);
        }

        private int GetAccountId(AppDbContext db, string settingKey, string defaultCode)
        {
            var code = _settingsService.Get($"account.{settingKey}", null) ?? defaultCode;
            var id = db.Accounts.Where(a => a.Code == code).Select(a => (int?)a.Id).FirstOrDefault();
            if (!id.HasValue) throw new InvalidOperationException($"Account with code '{code}' not found. Configure via Settings.");
            return id.Value;
        }
    }

    public sealed class DebitNotePostedHandler : IDomainEventHandler<DebitNotePostedEvent>
    {
        private readonly IInventoryMovementService _inventoryService;
        private readonly AuditService _auditService;
        private readonly SettingsService _settingsService;

        public DebitNotePostedHandler(IInventoryMovementService inventoryService, AuditService auditService, SettingsService settingsService)
        {
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public void Handle(DebitNotePostedEvent domainEvent)
        {
            var db = domainEvent.Db;
            var debitNote = domainEvent.DebitNote;
            if (debitNote == null) throw new ArgumentNullException(nameof(domainEvent.DebitNote));
            if (debitNote.Status != DocumentStatus.Posted) throw new InvalidOperationException("Only posted debit notes can affect accounting");
            if (db.JournalEntries.Any(j => j.SourceType == "DebitNote" && j.SourceId == debitNote.Id)) return;

            foreach (var item in debitNote.Items)
            {
                string error;
                if (!_inventoryService.DecreaseStockAndRecordMovement(db, item.ProductId, domainEvent.WarehouseId, item.Quantity, item.UnitPrice, debitNote.Number, "مرتجع مشتريات", out error))
                    throw new InvalidOperationException($"Stock deduction failed for product {item.ProductId}: {error}");
            }

            if (debitNote.SupplierId.HasValue)
            {
                var supplier = db.Suppliers.Find(debitNote.SupplierId.Value);
                if (supplier != null) supplier.Balance -= debitNote.TotalAmount;
            }

            var journal = new JournalEntry { Date = DateTime.UtcNow, EntryNumber = $"DN-{debitNote.Number}", Description = $"اشعار مدين - {debitNote.Number}", SourceType = "DebitNote", SourceId = debitNote.Id, UserId = debitNote.UserId };
            db.JournalEntries.Add(journal);
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, AccountId = GetAccountId(db, "payable", "2.1.1"), Description = "تخفيض حساب المورد", Debit = debitNote.TotalAmount, Credit = 0 });
            db.JournalEntryLines.Add(new JournalEntryLine { JournalEntry = journal, AccountId = GetAccountId(db, "expenses", "5.2.1"), Description = "مردودات مشتريات", Debit = 0, Credit = debitNote.TotalAmount });
            _auditService.Log("POST_DEBIT_NOTE", "DebitNote", debitNote.Id, debitNote.UserId);
        }

        private int GetAccountId(AppDbContext db, string settingKey, string defaultCode)
        {
            var code = _settingsService.Get($"account.{settingKey}", null) ?? defaultCode;
            var id = db.Accounts.Where(a => a.Code == code).Select(a => (int?)a.Id).FirstOrDefault();
            if (!id.HasValue) throw new InvalidOperationException($"Account with code '{code}' not found. Configure via Settings.");
            return id.Value;
        }
    }
}
