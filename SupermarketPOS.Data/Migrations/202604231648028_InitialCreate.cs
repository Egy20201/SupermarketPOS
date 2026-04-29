namespace SupermarketPOS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Accounts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 20),
                        Name = c.String(nullable: false, maxLength: 100),
                        AccountType = c.String(maxLength: 50),
                        SubType = c.String(maxLength: 50),
                        IsActive = c.Boolean(nullable: false),
                        IsParent = c.Boolean(nullable: false),
                        ParentId = c.Int(),
                        OpeningBalance = c.Decimal(nullable: false, precision: 18, scale: 2),
                        BalanceType = c.String(),
                        CurrentBalance = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Accounts", t => t.ParentId)
                .Index(t => t.ParentId);
            
            CreateTable(
                "dbo.AuditLogs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Timestamp = c.DateTime(nullable: false),
                        ActionType = c.String(),
                        EntityName = c.String(),
                        EntityId = c.Int(),
                        Description = c.String(),
                        UserId = c.Int(nullable: false),
                        UserName = c.String(),
                        IPAddress = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Users",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Username = c.String(),
                        Password = c.String(),
                        FullName = c.String(),
                        Role = c.String(),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.BusinessTypeConfigs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 50),
                        Name = c.String(nullable: false, maxLength: 100),
                        IsDefault = c.Boolean(nullable: false),
                        Notes = c.String(maxLength: 2000),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Categories",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        Description = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Products",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Barcode = c.String(),
                        Name = c.String(),
                        Description = c.String(),
                        Unit = c.String(),
                        PurchasePrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                        SellingPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ReorderLevel = c.Int(nullable: false),
                        ExpiryDate = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                        CategoryId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Categories", t => t.CategoryId, cascadeDelete: true)
                .Index(t => t.CategoryId);
            
            CreateTable(
                "dbo.ProductAttributes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ProductId = c.Int(nullable: false),
                        Key = c.String(nullable: false, maxLength: 100),
                        Value = c.String(maxLength: 2000),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.ProductStocks",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ProductId = c.Int(nullable: false),
                        WarehouseId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        AverageCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .ForeignKey("dbo.Warehouses", t => t.WarehouseId, cascadeDelete: true)
                .Index(t => t.ProductId)
                .Index(t => t.WarehouseId);
            
            CreateTable(
                "dbo.Warehouses",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        Branch = c.String(),
                        Location = c.String(),
                        Manager = c.String(),
                        IsActive = c.Boolean(nullable: false),
                        IsDefault = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.StockMovements",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Date = c.DateTime(nullable: false),
                        MovementType = c.String(),
                        Reference = c.String(),
                        ProductId = c.Int(nullable: false),
                        WarehouseId = c.Int(nullable: false),
                        QuantityIn = c.Int(nullable: false),
                        QuantityOut = c.Int(nullable: false),
                        UnitPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Notes = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .ForeignKey("dbo.Warehouses", t => t.WarehouseId, cascadeDelete: true)
                .Index(t => t.ProductId)
                .Index(t => t.WarehouseId);
            
            CreateTable(
                "dbo.PurchaseItems",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        PurchaseInvoiceId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        UnitPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .ForeignKey("dbo.PurchaseInvoices", t => t.PurchaseInvoiceId, cascadeDelete: true)
                .Index(t => t.PurchaseInvoiceId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.PurchaseInvoices",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        InvoiceNumber = c.String(),
                        Date = c.DateTime(nullable: false),
                        Discount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        SupplierId = c.Int(nullable: false),
                        WarehouseId = c.Int(nullable: false),
                        TotalAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PaidAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PaymentType = c.String(),
                        Notes = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId, cascadeDelete: true)
                .ForeignKey("dbo.Warehouses", t => t.WarehouseId, cascadeDelete: true)
                .Index(t => t.SupplierId)
                .Index(t => t.WarehouseId);
            
            CreateTable(
                "dbo.SupplierPayments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Date = c.DateTime(nullable: false),
                        SupplierId = c.Int(nullable: false),
                        PurchaseInvoiceId = c.Int(),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Notes = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.PurchaseInvoices", t => t.PurchaseInvoiceId)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId, cascadeDelete: true)
                .Index(t => t.SupplierId)
                .Index(t => t.PurchaseInvoiceId);
            
            CreateTable(
                "dbo.Suppliers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        ContactPerson = c.String(),
                        Phone = c.String(),
                        Email = c.String(),
                        Address = c.String(),
                        Notes = c.String(),
                        Balance = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.SaleItems",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SaleInvoiceId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        UnitPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .ForeignKey("dbo.SaleInvoices", t => t.SaleInvoiceId, cascadeDelete: true)
                .Index(t => t.SaleInvoiceId)
                .Index(t => t.ProductId);
            
            CreateTable(
                "dbo.SaleInvoices",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        InvoiceNumber = c.String(),
                        Date = c.DateTime(nullable: false),
                        TotalAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Discount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        NetAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PaidAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        WarehouseId = c.Int(),
                        Status = c.String(),
                        ShiftClosingId = c.Int(),
                        CustomerId = c.Int(),
                        UserId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Customers", t => t.CustomerId)
                .ForeignKey("dbo.ShiftClosings", t => t.ShiftClosingId)
                .ForeignKey("dbo.Users", t => t.UserId, cascadeDelete: true)
                .Index(t => t.ShiftClosingId)
                .Index(t => t.CustomerId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Customers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        Phone = c.String(),
                        Email = c.String(),
                        Address = c.String(),
                        Notes = c.String(),
                        Balance = c.Decimal(nullable: false, precision: 18, scale: 2),
                        LoyaltyPoints = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.CustomerPayments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Date = c.DateTime(nullable: false),
                        CustomerId = c.Int(nullable: false),
                        SaleInvoiceId = c.Int(),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Notes = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.SaleInvoices", t => t.SaleInvoiceId)
                .ForeignKey("dbo.Customers", t => t.CustomerId)
                .Index(t => t.CustomerId)
                .Index(t => t.SaleInvoiceId);
            
            CreateTable(
                "dbo.ShiftClosings",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ShiftDate = c.DateTime(nullable: false),
                        StartTime = c.DateTime(nullable: false),
                        EndTime = c.DateTime(nullable: false),
                        UserId = c.Int(nullable: false),
                        TotalInvoices = c.Int(nullable: false),
                        TotalSales = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalCashSales = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalCreditSales = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalDiscounts = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ExpectedCashInDrawer = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ActualCashInDrawer = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CashDifference = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Notes = c.String(),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.DailyClosings",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ClosingDate = c.DateTime(nullable: false),
                        ClosedById = c.Int(nullable: false),
                        TotalInvoices = c.Int(nullable: false),
                        TotalSales = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalCashSales = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalCreditSales = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalDiscounts = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ExpectedCashInDrawer = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ActualCashInDrawer = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CashDifference = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Notes = c.String(),
                        IsClosed = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.ClosedById)
                .Index(t => t.ClosedById);
            
            CreateTable(
                "dbo.Expenses",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ExpenseDate = c.DateTime(nullable: false),
                        Description = c.String(),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        ExpenseType = c.String(),
                        ReferenceNumber = c.String(),
                        Notes = c.String(),
                        CreatedById = c.Int(nullable: false),
                        JournalEntryId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.CreatedById)
                .ForeignKey("dbo.JournalEntries", t => t.JournalEntryId)
                .Index(t => t.CreatedById)
                .Index(t => t.JournalEntryId);
            
            CreateTable(
                "dbo.JournalEntries",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Date = c.DateTime(nullable: false),
                        EntryNumber = c.String(nullable: false, maxLength: 50),
                        Description = c.String(maxLength: 500),
                        UserId = c.Int(),
                        SourceType = c.String(),
                        SourceId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.JournalEntryLines",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        JournalEntryId = c.Int(nullable: false),
                        AccountId = c.Int(nullable: false),
                        Description = c.String(maxLength: 200),
                        Debit = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Credit = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Accounts", t => t.AccountId, cascadeDelete: true)
                .ForeignKey("dbo.JournalEntries", t => t.JournalEntryId, cascadeDelete: true)
                .Index(t => t.JournalEntryId)
                .Index(t => t.AccountId);
            
            CreateTable(
                "dbo.FeatureFlags",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 100),
                        Name = c.String(nullable: false, maxLength: 200),
                        IsEnabled = c.Boolean(nullable: false),
                        Description = c.String(maxLength: 2000),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.FiscalPeriods",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Year = c.Int(nullable: false),
                        Month = c.Int(nullable: false),
                        StartDate = c.DateTime(nullable: false),
                        EndDate = c.DateTime(nullable: false),
                        IsLocked = c.Boolean(nullable: false),
                        LockedAt = c.DateTime(),
                        LockedById = c.Int(),
                        LockReason = c.String(),
                        PeriodDisplay = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.LockedById)
                .Index(t => t.LockedById);
            
            CreateTable(
                "dbo.PlatformSettings",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Key = c.String(nullable: false, maxLength: 100),
                        Value = c.String(maxLength: 2000),
                        Scope = c.String(maxLength: 100),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.StockCostHistories",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ProductId = c.Int(nullable: false),
                        WarehouseId = c.Int(nullable: false),
                        MovementDate = c.DateTime(nullable: false),
                        MovementType = c.String(),
                        Reference = c.String(),
                        QuantityChange = c.Int(nullable: false),
                        UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                        QuantityBefore = c.Int(nullable: false),
                        QuantityAfter = c.Int(nullable: false),
                        AverageCostBefore = c.Decimal(nullable: false, precision: 18, scale: 2),
                        AverageCostAfter = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.SyncedOfflineInvoices",
                c => new
                    {
                        TemporaryId = c.String(nullable: false, maxLength: 50),
                        SyncedInvoiceId = c.Int(nullable: false),
                        SyncedInvoiceNumber = c.String(maxLength: 50),
                        SyncedAt = c.DateTime(nullable: false),
                        DeviceId = c.String(maxLength: 50),
                    })
                .PrimaryKey(t => t.TemporaryId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.FiscalPeriods", "LockedById", "dbo.Users");
            DropForeignKey("dbo.Expenses", "JournalEntryId", "dbo.JournalEntries");
            DropForeignKey("dbo.JournalEntries", "UserId", "dbo.Users");
            DropForeignKey("dbo.JournalEntryLines", "JournalEntryId", "dbo.JournalEntries");
            DropForeignKey("dbo.JournalEntryLines", "AccountId", "dbo.Accounts");
            DropForeignKey("dbo.Expenses", "CreatedById", "dbo.Users");
            DropForeignKey("dbo.DailyClosings", "ClosedById", "dbo.Users");
            DropForeignKey("dbo.SaleItems", "SaleInvoiceId", "dbo.SaleInvoices");
            DropForeignKey("dbo.SaleInvoices", "UserId", "dbo.Users");
            DropForeignKey("dbo.SaleInvoices", "ShiftClosingId", "dbo.ShiftClosings");
            DropForeignKey("dbo.ShiftClosings", "UserId", "dbo.Users");
            DropForeignKey("dbo.SaleInvoices", "CustomerId", "dbo.Customers");
            DropForeignKey("dbo.CustomerPayments", "CustomerId", "dbo.Customers");
            DropForeignKey("dbo.CustomerPayments", "SaleInvoiceId", "dbo.SaleInvoices");
            DropForeignKey("dbo.SaleItems", "ProductId", "dbo.Products");
            DropForeignKey("dbo.PurchaseItems", "PurchaseInvoiceId", "dbo.PurchaseInvoices");
            DropForeignKey("dbo.PurchaseInvoices", "WarehouseId", "dbo.Warehouses");
            DropForeignKey("dbo.PurchaseInvoices", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierPayments", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.SupplierPayments", "PurchaseInvoiceId", "dbo.PurchaseInvoices");
            DropForeignKey("dbo.PurchaseItems", "ProductId", "dbo.Products");
            DropForeignKey("dbo.ProductStocks", "WarehouseId", "dbo.Warehouses");
            DropForeignKey("dbo.StockMovements", "WarehouseId", "dbo.Warehouses");
            DropForeignKey("dbo.StockMovements", "ProductId", "dbo.Products");
            DropForeignKey("dbo.ProductStocks", "ProductId", "dbo.Products");
            DropForeignKey("dbo.ProductAttributes", "ProductId", "dbo.Products");
            DropForeignKey("dbo.Products", "CategoryId", "dbo.Categories");
            DropForeignKey("dbo.AuditLogs", "UserId", "dbo.Users");
            DropForeignKey("dbo.Accounts", "ParentId", "dbo.Accounts");
            DropIndex("dbo.FiscalPeriods", new[] { "LockedById" });
            DropIndex("dbo.JournalEntryLines", new[] { "AccountId" });
            DropIndex("dbo.JournalEntryLines", new[] { "JournalEntryId" });
            DropIndex("dbo.JournalEntries", new[] { "UserId" });
            DropIndex("dbo.Expenses", new[] { "JournalEntryId" });
            DropIndex("dbo.Expenses", new[] { "CreatedById" });
            DropIndex("dbo.DailyClosings", new[] { "ClosedById" });
            DropIndex("dbo.ShiftClosings", new[] { "UserId" });
            DropIndex("dbo.CustomerPayments", new[] { "SaleInvoiceId" });
            DropIndex("dbo.CustomerPayments", new[] { "CustomerId" });
            DropIndex("dbo.SaleInvoices", new[] { "UserId" });
            DropIndex("dbo.SaleInvoices", new[] { "CustomerId" });
            DropIndex("dbo.SaleInvoices", new[] { "ShiftClosingId" });
            DropIndex("dbo.SaleItems", new[] { "ProductId" });
            DropIndex("dbo.SaleItems", new[] { "SaleInvoiceId" });
            DropIndex("dbo.SupplierPayments", new[] { "PurchaseInvoiceId" });
            DropIndex("dbo.SupplierPayments", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseInvoices", new[] { "WarehouseId" });
            DropIndex("dbo.PurchaseInvoices", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseItems", new[] { "ProductId" });
            DropIndex("dbo.PurchaseItems", new[] { "PurchaseInvoiceId" });
            DropIndex("dbo.StockMovements", new[] { "WarehouseId" });
            DropIndex("dbo.StockMovements", new[] { "ProductId" });
            DropIndex("dbo.ProductStocks", new[] { "WarehouseId" });
            DropIndex("dbo.ProductStocks", new[] { "ProductId" });
            DropIndex("dbo.ProductAttributes", new[] { "ProductId" });
            DropIndex("dbo.Products", new[] { "CategoryId" });
            DropIndex("dbo.AuditLogs", new[] { "UserId" });
            DropIndex("dbo.Accounts", new[] { "ParentId" });
            DropTable("dbo.SyncedOfflineInvoices");
            DropTable("dbo.StockCostHistories");
            DropTable("dbo.PlatformSettings");
            DropTable("dbo.FiscalPeriods");
            DropTable("dbo.FeatureFlags");
            DropTable("dbo.JournalEntryLines");
            DropTable("dbo.JournalEntries");
            DropTable("dbo.Expenses");
            DropTable("dbo.DailyClosings");
            DropTable("dbo.ShiftClosings");
            DropTable("dbo.CustomerPayments");
            DropTable("dbo.Customers");
            DropTable("dbo.SaleInvoices");
            DropTable("dbo.SaleItems");
            DropTable("dbo.Suppliers");
            DropTable("dbo.SupplierPayments");
            DropTable("dbo.PurchaseInvoices");
            DropTable("dbo.PurchaseItems");
            DropTable("dbo.StockMovements");
            DropTable("dbo.Warehouses");
            DropTable("dbo.ProductStocks");
            DropTable("dbo.ProductAttributes");
            DropTable("dbo.Products");
            DropTable("dbo.Categories");
            DropTable("dbo.BusinessTypeConfigs");
            DropTable("dbo.Users");
            DropTable("dbo.AuditLogs");
            DropTable("dbo.Accounts");
        }
    }
}
