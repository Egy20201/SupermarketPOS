using SupermarketPOS.Core.Entities;
using SupermarketPOS.Core.Metadata;
using SupermarketPOS.Core.Observability;
using SupermarketPOS.Core.Security;
using System;
using System.Data.Entity;
using System.Linq;

namespace SupermarketPOS.Data
{
    public class AppDbContext : DbContext
    {
        static AppDbContext()
        {
            Database.SetInitializer<AppDbContext>(
                new MigrateDatabaseToLatestVersion<AppDbContext, SupermarketPOS.Data.Migrations.Configuration>());
        }        public DbSet<Department> Departments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<Payroll> Payrolls { get; set; }
        public DbSet<PayrollItem> PayrollItems { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<Penalty> Penalties { get; set; }
        public DbSet<BiometricLog> BiometricLogs { get; set; }
        public DbSet<Lead> Leads { get; set; }
        public DbSet<Opportunity> Opportunities { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<FollowUp> FollowUps { get; set; }
        public AppDbContext() : base("name=SupermarketDBConnection") { }
        public DbSet<SalesQuotation> SalesQuotations { get; set; }
        public DbSet<SalesQuotationItem> SalesQuotationItems { get; set; }
        public DbSet<SalesOrder> SalesOrders { get; set; }
        public DbSet<SalesOrderItem> SalesOrderItems { get; set; }
        public DbSet<Delivery> Deliveries { get; set; }
        public DbSet<DeliveryItem> DeliveryItems { get; set; }
        public DbSet<CreditNote> CreditNotes { get; set; }
        public DbSet<CreditNoteItem> CreditNoteItems { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<GoodsReceipt> GoodsReceipts { get; set; }
        public DbSet<GoodsReceiptItem> GoodsReceiptItems { get; set; }
        public DbSet<DebitNote> DebitNotes { get; set; }
        public DbSet<DebitNoteItem> DebitNoteItems { get; set; }
        public DbSet<SyncedOfflineInvoice> SyncedOfflineInvoices { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductAttribute> ProductAttributes { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<ProductStock> ProductStocks { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<StockCostHistory> StockCostHistories { get; set; }
        public DbSet<ProductUnit> ProductUnits { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<SaleInvoice> SaleInvoices { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<CustomerPayment> CustomerPayments { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
        public DbSet<PurchaseItem> PurchaseItems { get; set; }
        public DbSet<SupplierPayment> SupplierPayments { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<FiscalPeriod> FiscalPeriods { get; set; }
        public DbSet<DailyClosing> DailyClosings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<ShiftClosing> ShiftClosings { get; set; }
        public DbSet<PlatformSetting> PlatformSettings { get; set; }
        public DbSet<FeatureFlag> FeatureFlags { get; set; }
        public DbSet<BusinessTypeConfig> BusinessTypeConfigs { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<AuditLogEntry> AuditLogEntries { get; set; }
        public DbSet<Setting> Settings { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<UserBranchPermission> UserBranchPermissions { get; set; }
        public DbSet<OutboxEvent> OutboxEvents { get; set; }

        // Phase 1: Metadata Core
        public DbSet<ModuleDefinition> ModuleDefinitions { get; set; }
        public DbSet<EntityDefinition> EntityDefinitions { get; set; }
        public DbSet<FieldDefinition> FieldDefinitions { get; set; }
        public DbSet<RelationDefinition> RelationDefinitions { get; set; }
        public DbSet<FormLayoutDefinition> FormLayoutDefinitions { get; set; }
        public DbSet<GridLayoutDefinition> GridLayoutDefinitions { get; set; }
        public DbSet<ActionDefinition> ActionDefinitions { get; set; }

        // Phase 2.5: Enterprise Hardening
        public DbSet<FieldPermission> FieldPermissions { get; set; }

        // Phase 4: Workflow Engine
        public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
        public DbSet<WorkflowRule> WorkflowRules { get; set; }
        public DbSet<WorkflowAction> WorkflowActions { get; set; }
        public DbSet<WorkflowExecutionLog> WorkflowExecutionLogs { get; set; }

        // Phase 4.5: Automation Layer
        public DbSet<ScheduledJob> ScheduledJobs { get; set; }

        // Phase 5: Observability
        public DbSet<ExecutionTrace> ExecutionTraces { get; set; }
        public DbSet<DeadLetterEvent> DeadLetterEvents { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().Ignore(p => p.CurrentStock);
            modelBuilder.Entity<Product>().HasRequired(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId);
            modelBuilder.Entity<Product>().HasOptional(p => p.UnitEntity).WithMany().HasForeignKey(p => p.UnitId);
            modelBuilder.Entity<ProductAttribute>().HasRequired(pa => pa.Product).WithMany(p => p.ProductAttributes).HasForeignKey(pa => pa.ProductId);
            modelBuilder.Entity<ProductUnit>().HasRequired(pu => pu.Product).WithMany(p => p.ProductUnits).HasForeignKey(pu => pu.ProductId).WillCascadeOnDelete(true);
            modelBuilder.Entity<ProductUnit>().HasRequired(pu => pu.Unit).WithMany().HasForeignKey(pu => pu.UnitId).WillCascadeOnDelete(false);
            modelBuilder.Entity<ProductUnit>().Property(pu => pu.Barcode).HasMaxLength(50);
            modelBuilder.Entity<SaleInvoice>().HasRequired(s => s.User).WithMany().HasForeignKey(s => s.UserId);
            modelBuilder.Entity<SaleItem>().HasRequired(si => si.SaleInvoice).WithMany(s => s.Items).HasForeignKey(si => si.SaleInvoiceId);
            modelBuilder.Entity<SaleItem>().HasRequired(si => si.Product).WithMany(p => p.SaleItems).HasForeignKey(si => si.ProductId);
            modelBuilder.Entity<Customer>().HasMany(c => c.SaleInvoices).WithOptional(s => s.Customer).HasForeignKey(s => s.CustomerId).WillCascadeOnDelete(false);
            modelBuilder.Entity<Customer>().HasMany(c => c.CustomerPayments).WithRequired(p => p.Customer).HasForeignKey(p => p.CustomerId).WillCascadeOnDelete(false);
            modelBuilder.Entity<CustomerPayment>().HasOptional(cp => cp.SaleInvoice).WithMany(s => s.Payments).HasForeignKey(cp => cp.SaleInvoiceId).WillCascadeOnDelete(false);
            modelBuilder.Entity<ProductStock>().HasRequired(ps => ps.Product).WithMany(p => p.ProductStocks).HasForeignKey(ps => ps.ProductId);
            modelBuilder.Entity<ProductStock>().HasRequired(ps => ps.Warehouse).WithMany(w => w.ProductStocks).HasForeignKey(ps => ps.WarehouseId);
            modelBuilder.Entity<StockMovement>().HasRequired(sm => sm.Product).WithMany(p => p.StockMovements).HasForeignKey(sm => sm.ProductId);
            modelBuilder.Entity<StockMovement>().HasRequired(sm => sm.Warehouse).WithMany(w => w.StockMovements).HasForeignKey(sm => sm.WarehouseId);
            modelBuilder.Entity<PurchaseInvoice>().HasRequired(pi => pi.Supplier).WithMany(s => s.PurchaseInvoices).HasForeignKey(pi => pi.SupplierId);
            modelBuilder.Entity<PurchaseInvoice>().HasRequired(pi => pi.Warehouse).WithMany().HasForeignKey(pi => pi.WarehouseId);
            modelBuilder.Entity<PurchaseItem>().HasRequired(pi => pi.PurchaseInvoice).WithMany(p => p.Items).HasForeignKey(pi => pi.PurchaseInvoiceId);
            modelBuilder.Entity<PurchaseItem>().HasRequired(pi => pi.Product).WithMany(p => p.PurchaseItems).HasForeignKey(pi => pi.ProductId);
            modelBuilder.Entity<SupplierPayment>().HasRequired(sp => sp.Supplier).WithMany().HasForeignKey(sp => sp.SupplierId);
            modelBuilder.Entity<SupplierPayment>().HasOptional(sp => sp.PurchaseInvoice).WithMany(p => p.Payments).HasForeignKey(sp => sp.PurchaseInvoiceId);
            modelBuilder.Entity<Expense>().HasRequired(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedById).WillCascadeOnDelete(false);
            modelBuilder.Entity<FiscalPeriod>().HasOptional(fp => fp.LockedBy).WithMany().HasForeignKey(fp => fp.LockedById).WillCascadeOnDelete(false);
            modelBuilder.Entity<DailyClosing>().HasRequired(dc => dc.ClosedBy).WithMany().HasForeignKey(dc => dc.ClosedById).WillCascadeOnDelete(false);
            modelBuilder.Entity<AuditLog>().HasRequired(al => al.User).WithMany().HasForeignKey(al => al.UserId).WillCascadeOnDelete(false);
            modelBuilder.Entity<PlatformSetting>().Property(s => s.Key).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<FeatureFlag>().Property(f => f.Code).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<BusinessTypeConfig>().Property(b => b.Code).IsRequired().HasMaxLength(50);

            // Phase 4
            modelBuilder.Entity<RolePermission>().HasRequired(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
            modelBuilder.Entity<RolePermission>().HasRequired(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId);
            modelBuilder.Entity<User>().HasOptional(u => u.RoleEntity).WithMany(r => r.Users).HasForeignKey(u => u.RoleId);
            modelBuilder.Entity<AuditLogEntry>().HasRequired(a => a.User).WithMany().HasForeignKey(a => a.UserId).WillCascadeOnDelete(false);

            // Phase 5: Branch relationships
            modelBuilder.Entity<User>().HasOptional(u => u.Branch).WithMany(b => b.Users).HasForeignKey(u => u.BranchId);
            modelBuilder.Entity<SaleInvoice>().HasOptional(s => s.Branch).WithMany().HasForeignKey(s => s.BranchId);
            modelBuilder.Entity<PurchaseInvoice>().HasOptional(p => p.Branch).WithMany().HasForeignKey(p => p.BranchId);
            modelBuilder.Entity<StockMovement>().HasOptional(sm => sm.Branch).WithMany().HasForeignKey(sm => sm.BranchId);
            modelBuilder.Entity<Product>().HasOptional(p => p.Branch).WithMany().HasForeignKey(p => p.BranchId);

            modelBuilder.Entity<UserBranchPermission>().HasRequired(ubp => ubp.User).WithMany().HasForeignKey(ubp => ubp.UserId).WillCascadeOnDelete(false);
            modelBuilder.Entity<UserBranchPermission>().HasRequired(ubp => ubp.Branch).WithMany().HasForeignKey(ubp => ubp.BranchId).WillCascadeOnDelete(false);
            modelBuilder.Entity<UserBranchPermission>().HasRequired(ubp => ubp.Permission).WithMany().HasForeignKey(ubp => ubp.PermissionId).WillCascadeOnDelete(false);

            modelBuilder.Entity<OutboxEvent>().Property(e => e.EventType).IsRequired().HasMaxLength(200);
            modelBuilder.Entity<OutboxEvent>().Property(e => e.Payload).IsRequired();
            modelBuilder.Entity<OutboxEvent>().Property(e => e.Status).IsRequired().HasMaxLength(50);

            // Phase 7.2 HR
            modelBuilder.Entity<Employee>().HasOptional(e => e.Shift).WithMany(s => s.Employees).HasForeignKey(e => e.ShiftId);
            modelBuilder.Entity<LeaveRequest>().HasRequired(l => l.Employee).WithMany().HasForeignKey(l => l.EmployeeId);
            modelBuilder.Entity<LeaveRequest>().HasRequired(l => l.User).WithMany().HasForeignKey(l => l.UserId);
            modelBuilder.Entity<LeaveRequest>().HasOptional(l => l.Branch).WithMany().HasForeignKey(l => l.BranchId);
            modelBuilder.Entity<Penalty>().HasRequired(p => p.Employee).WithMany().HasForeignKey(p => p.EmployeeId);
            modelBuilder.Entity<Penalty>().HasOptional(p => p.Branch).WithMany().HasForeignKey(p => p.BranchId);
            modelBuilder.Entity<BiometricLog>().HasOptional(b => b.AttendanceRecord).WithMany().HasForeignKey(b => b.AttendanceRecordId);
            modelBuilder.Entity<BiometricLog>().HasOptional(b => b.Branch).WithMany().HasForeignKey(b => b.BranchId);

            // Phase 6: Workflow document relationships
            modelBuilder.Entity<SalesQuotation>().HasOptional(q => q.Customer).WithMany().HasForeignKey(q => q.CustomerId);
            modelBuilder.Entity<SalesQuotation>().HasRequired(q => q.User).WithMany().HasForeignKey(q => q.UserId);
            modelBuilder.Entity<SalesQuotation>().HasOptional(q => q.Branch).WithMany().HasForeignKey(q => q.BranchId);
            modelBuilder.Entity<SalesQuotationItem>().HasRequired(i => i.SalesQuotation).WithMany(q => q.Items).HasForeignKey(i => i.SalesQuotationId);
            modelBuilder.Entity<SalesQuotationItem>().HasRequired(i => i.Product).WithMany().HasForeignKey(i => i.ProductId);

            modelBuilder.Entity<SalesOrder>().HasOptional(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId);
            modelBuilder.Entity<SalesOrder>().HasRequired(o => o.User).WithMany().HasForeignKey(o => o.UserId);
            modelBuilder.Entity<SalesOrder>().HasOptional(o => o.Branch).WithMany().HasForeignKey(o => o.BranchId);
            modelBuilder.Entity<SalesOrder>().HasOptional(o => o.Quotation).WithMany().HasForeignKey(o => o.QuotationId);
            modelBuilder.Entity<SalesOrderItem>().HasRequired(i => i.SalesOrder).WithMany(o => o.Items).HasForeignKey(i => i.SalesOrderId);
            modelBuilder.Entity<SalesOrderItem>().HasRequired(i => i.Product).WithMany().HasForeignKey(i => i.ProductId);

            modelBuilder.Entity<Delivery>().HasOptional(d => d.SalesOrder).WithMany().HasForeignKey(d => d.SalesOrderId);
            modelBuilder.Entity<Delivery>().HasRequired(d => d.User).WithMany().HasForeignKey(d => d.UserId);
            modelBuilder.Entity<Delivery>().HasOptional(d => d.Branch).WithMany().HasForeignKey(d => d.BranchId);
            modelBuilder.Entity<Delivery>().HasRequired(d => d.Warehouse).WithMany().HasForeignKey(d => d.WarehouseId);
            modelBuilder.Entity<DeliveryItem>().HasRequired(i => i.Delivery).WithMany(d => d.Items).HasForeignKey(i => i.DeliveryId);
            modelBuilder.Entity<DeliveryItem>().HasRequired(i => i.Product).WithMany().HasForeignKey(i => i.ProductId);

            modelBuilder.Entity<CreditNote>().HasOptional(c => c.Customer).WithMany().HasForeignKey(c => c.CustomerId);
            modelBuilder.Entity<CreditNote>().HasOptional(c => c.SaleInvoice).WithMany().HasForeignKey(c => c.SaleInvoiceId);
            modelBuilder.Entity<CreditNote>().HasRequired(c => c.User).WithMany().HasForeignKey(c => c.UserId);
            modelBuilder.Entity<CreditNote>().HasOptional(c => c.Branch).WithMany().HasForeignKey(c => c.BranchId);
            modelBuilder.Entity<CreditNoteItem>().HasRequired(i => i.CreditNote).WithMany(c => c.Items).HasForeignKey(i => i.CreditNoteId);
            modelBuilder.Entity<CreditNoteItem>().HasRequired(i => i.Product).WithMany().HasForeignKey(i => i.ProductId);

            modelBuilder.Entity<PurchaseOrder>().HasRequired(o => o.Supplier).WithMany().HasForeignKey(o => o.SupplierId);
            modelBuilder.Entity<PurchaseOrder>().HasRequired(o => o.User).WithMany().HasForeignKey(o => o.UserId);
            modelBuilder.Entity<PurchaseOrder>().HasOptional(o => o.Branch).WithMany().HasForeignKey(o => o.BranchId);
            modelBuilder.Entity<PurchaseOrderItem>().HasRequired(i => i.PurchaseOrder).WithMany(o => o.Items).HasForeignKey(i => i.PurchaseOrderId);
            modelBuilder.Entity<PurchaseOrderItem>().HasRequired(i => i.Product).WithMany().HasForeignKey(i => i.ProductId);

            modelBuilder.Entity<GoodsReceipt>().HasOptional(g => g.PurchaseOrder).WithMany().HasForeignKey(g => g.PurchaseOrderId);
            modelBuilder.Entity<GoodsReceipt>().HasRequired(g => g.User).WithMany().HasForeignKey(g => g.UserId);
            modelBuilder.Entity<GoodsReceipt>().HasOptional(g => g.Branch).WithMany().HasForeignKey(g => g.BranchId);
            modelBuilder.Entity<GoodsReceipt>().HasRequired(g => g.Warehouse).WithMany().HasForeignKey(g => g.WarehouseId);
            modelBuilder.Entity<GoodsReceiptItem>().HasRequired(i => i.GoodsReceipt).WithMany(g => g.Items).HasForeignKey(i => i.GoodsReceiptId);
            modelBuilder.Entity<GoodsReceiptItem>().HasRequired(i => i.Product).WithMany().HasForeignKey(i => i.ProductId);

            modelBuilder.Entity<DebitNote>().HasOptional(d => d.Supplier).WithMany().HasForeignKey(d => d.SupplierId);
            modelBuilder.Entity<DebitNote>().HasOptional(d => d.PurchaseInvoice).WithMany().HasForeignKey(d => d.PurchaseInvoiceId);
            modelBuilder.Entity<DebitNote>().HasRequired(d => d.User).WithMany().HasForeignKey(d => d.UserId);
            modelBuilder.Entity<DebitNote>().HasOptional(d => d.Branch).WithMany().HasForeignKey(d => d.BranchId);
            modelBuilder.Entity<DebitNoteItem>().HasRequired(i => i.DebitNote).WithMany(d => d.Items).HasForeignKey(i => i.DebitNoteId);
            modelBuilder.Entity<DebitNoteItem>().HasRequired(i => i.Product).WithMany().HasForeignKey(i => i.ProductId);

            // Phase 8: CRM module
            modelBuilder.Entity<Lead>().HasOptional(l => l.AssignedTo).WithMany().HasForeignKey(l => l.AssignedToUserId);
            modelBuilder.Entity<Lead>().HasOptional(l => l.ConvertedToCustomer).WithMany().HasForeignKey(l => l.ConvertedToCustomerId);
            modelBuilder.Entity<Lead>().HasOptional(l => l.Branch).WithMany().HasForeignKey(l => l.BranchId);

            modelBuilder.Entity<Opportunity>().HasOptional(o => o.Lead).WithMany(l => l.Opportunities).HasForeignKey(o => o.LeadId);
            modelBuilder.Entity<Opportunity>().HasOptional(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId);
            modelBuilder.Entity<Opportunity>().HasOptional(o => o.AssignedTo).WithMany().HasForeignKey(o => o.AssignedToUserId);
            modelBuilder.Entity<Opportunity>().HasOptional(o => o.Branch).WithMany().HasForeignKey(o => o.BranchId);

            modelBuilder.Entity<Activity>().HasOptional(a => a.Lead).WithMany(l => l.Activities).HasForeignKey(a => a.LeadId);
            modelBuilder.Entity<Activity>().HasOptional(a => a.Opportunity).WithMany(o => o.Activities).HasForeignKey(a => a.OpportunityId);
            modelBuilder.Entity<Activity>().HasOptional(a => a.Customer).WithMany().HasForeignKey(a => a.CustomerId);
            modelBuilder.Entity<Activity>().HasRequired(a => a.CreatedBy).WithMany().HasForeignKey(a => a.CreatedByUserId);

            modelBuilder.Entity<FollowUp>().HasOptional(f => f.Lead).WithMany().HasForeignKey(f => f.LeadId);
            modelBuilder.Entity<FollowUp>().HasOptional(f => f.Opportunity).WithMany().HasForeignKey(f => f.OpportunityId);
            modelBuilder.Entity<FollowUp>().HasOptional(f => f.Customer).WithMany().HasForeignKey(f => f.CustomerId);
            modelBuilder.Entity<FollowUp>().HasRequired(f => f.CreatedBy).WithMany().HasForeignKey(f => f.CreatedByUserId);

            // Phase 1: Metadata Core table mappings
            modelBuilder.Entity<ModuleDefinition>().ToTable("Modules");
            modelBuilder.Entity<ModuleDefinition>().Property(m => m.Name).IsRequired().HasMaxLength(100);

            modelBuilder.Entity<EntityDefinition>().ToTable("Entities");
            modelBuilder.Entity<EntityDefinition>().Property(e => e.Name).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<EntityDefinition>().Property(e => e.DisplayName).HasMaxLength(200);
            modelBuilder.Entity<EntityDefinition>().Property(e => e.TableName).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<EntityDefinition>().HasIndex(e => e.Name).IsUnique().HasName("IX_Entities_Name");
            modelBuilder.Entity<EntityDefinition>()
                .HasRequired(e => e.Module)
                .WithMany(m => m.Entities)
                .HasForeignKey(e => e.ModuleId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<FieldDefinition>().ToTable("Fields");
            modelBuilder.Entity<FieldDefinition>().Property(f => f.Name).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<FieldDefinition>().Property(f => f.DataType).IsRequired().HasMaxLength(50);
            modelBuilder.Entity<FieldDefinition>().Property(f => f.DisplayName).HasMaxLength(200);
            modelBuilder.Entity<FieldDefinition>().Property(f => f.DefaultValue).HasMaxLength(500);
            modelBuilder.Entity<FieldDefinition>().Property(f => f.LookupEntity).HasMaxLength(100);
            modelBuilder.Entity<FieldDefinition>().Property(f => f.LookupDisplayField).HasMaxLength(100);
            modelBuilder.Entity<FieldDefinition>().HasIndex(f => f.EntityId).HasName("IX_Fields_EntityId");
            modelBuilder.Entity<FieldDefinition>()
                .HasRequired(f => f.Entity)
                .WithMany(e => e.Fields)
                .HasForeignKey(f => f.EntityId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<FieldDefinition>()
                .HasOptional(f => f.LookupEntityRef)
                .WithMany()
                .HasForeignKey(f => f.LookupEntityId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<RelationDefinition>().ToTable("Relations");
            modelBuilder.Entity<RelationDefinition>().Property(r => r.RelationType).IsRequired().HasMaxLength(50);
            modelBuilder.Entity<RelationDefinition>().Property(r => r.FieldName).HasMaxLength(100);
            modelBuilder.Entity<RelationDefinition>()
                .HasRequired(r => r.SourceEntity)
                .WithMany(e => e.OutgoingRelations)
                .HasForeignKey(r => r.SourceEntityId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<RelationDefinition>()
                .HasRequired(r => r.TargetEntity)
                .WithMany(e => e.IncomingRelations)
                .HasForeignKey(r => r.TargetEntityId)
                .WillCascadeOnDelete(false);

            // FormLayouts
            modelBuilder.Entity<FormLayoutDefinition>().ToTable("FormLayouts");
            modelBuilder.Entity<FormLayoutDefinition>().Property(f => f.LayoutJson).IsRequired();
            modelBuilder.Entity<FormLayoutDefinition>()
                .HasRequired(f => f.Entity)
                .WithMany(e => e.FormLayouts)
                .HasForeignKey(f => f.EntityId)
                .WillCascadeOnDelete(false);

            // GridLayouts
            modelBuilder.Entity<GridLayoutDefinition>().ToTable("GridLayouts");
            modelBuilder.Entity<GridLayoutDefinition>().Property(g => g.ColumnsJson).IsRequired();
            modelBuilder.Entity<GridLayoutDefinition>()
                .HasRequired(g => g.Entity)
                .WithMany(e => e.GridLayouts)
                .HasForeignKey(g => g.EntityId)
                .WillCascadeOnDelete(false);

            // Actions
            modelBuilder.Entity<ActionDefinition>().ToTable("Actions");
            modelBuilder.Entity<ActionDefinition>().Property(a => a.Name).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<ActionDefinition>().Property(a => a.Type).IsRequired().HasMaxLength(50);
            modelBuilder.Entity<ActionDefinition>()
                .HasRequired(a => a.Entity)
                .WithMany(e => e.Actions)
                .HasForeignKey(a => a.EntityId)
                .WillCascadeOnDelete(false);

            // Phase 2.5: FieldPermissions
            modelBuilder.Entity<FieldPermission>().ToTable("FieldPermissions");
            modelBuilder.Entity<FieldPermission>().Property(fp => fp.FieldName).IsRequired().HasMaxLength(100);
            modelBuilder.Entity<FieldPermission>()
                .HasRequired(fp => fp.Entity)
                .WithMany(e => e.FieldPermissions)
                .HasForeignKey(fp => fp.EntityId)
                .WillCascadeOnDelete(false);

            // Phase 2.5: Computed field columns
            modelBuilder.Entity<FieldDefinition>().Property(f => f.ComputedExpression).HasMaxLength(500);

            // Phase 2.5: Entity MaxRows
            modelBuilder.Entity<EntityDefinition>().Property(e => e.MaxRows).IsOptional();

            // Phase 4: Workflow Engine
            modelBuilder.Entity<WorkflowDefinition>()
                .HasRequired(w => w.Entity)
                .WithMany()
                .HasForeignKey(w => w.EntityId)
                .WillCascadeOnDelete(false);
            modelBuilder.Entity<WorkflowRule>()
                .HasRequired(r => r.Workflow)
                .WithMany(w => w.Rules)
                .HasForeignKey(r => r.WorkflowId)
                .WillCascadeOnDelete(true);
            modelBuilder.Entity<WorkflowAction>()
                .HasRequired(a => a.Rule)
                .WithMany(r => r.Actions)
                .HasForeignKey(a => a.RuleId)
                .WillCascadeOnDelete(true);
        }

        public static void SeedData()
        {
            using (var db = new AppDbContext())
            {
                // Production safety: never seed default login credentials.
                if (!db.Warehouses.Any())
                {
                    db.Warehouses.Add(new Warehouse { Name = "المخزن الرئيسي", Branch = "الفرع الرئيسي", Location = "الدور الأرضي", Manager = "أحمد", IsActive = true, IsDefault = true });
                    db.SaveChanges();
                }
            }
        }
    }
}
