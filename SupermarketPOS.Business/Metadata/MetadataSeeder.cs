using SupermarketPOS.Core.Metadata;
using SupermarketPOS.Data;
using System;
using System.Linq;

namespace SupermarketPOS.Business.Metadata
{
    /// <summary>
    /// Seeds the metadata tables with initial entity/field/relation definitions.
    /// Idempotent: checks for existing data before inserting.
    /// </summary>
    public class MetadataSeeder
    {
        private readonly Func<AppDbContext> _dbFactory;

        public MetadataSeeder(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        public void SeedIfEmpty()
        {
            using (var db = _dbFactory())
            {
                if (db.ModuleDefinitions.Any())
                    return;

                SeedModules(db);
                db.SaveChanges();

                SeedEntities(db);
                db.SaveChanges();

                SeedFields(db);
                db.SaveChanges();

                SeedRelations(db);
                db.SaveChanges();

                SeedFormLayouts(db);
                db.SaveChanges();

                SeedGridLayouts(db);
                db.SaveChanges();

                SeedActions(db);
                db.SaveChanges();

                Logger.Info("Metadata seed completed: modules, entities, fields, relations, layouts, actions");
            }
        }

        private static void SeedModules(AppDbContext db)
        {
            db.ModuleDefinitions.Add(new ModuleDefinition { Name = "Inventory", IsActive = true });
            db.ModuleDefinitions.Add(new ModuleDefinition { Name = "Sales", IsActive = true });
            db.ModuleDefinitions.Add(new ModuleDefinition { Name = "Customers", IsActive = true });
        }

        private static void SeedEntities(AppDbContext db)
        {
            var inventory = db.ModuleDefinitions.Local.First(m => m.Name == "Inventory");
            var sales = db.ModuleDefinitions.Local.First(m => m.Name == "Sales");
            var customers = db.ModuleDefinitions.Local.First(m => m.Name == "Customers");

            db.EntityDefinitions.Add(new EntityDefinition
            {
                Name = "Product",
                DisplayName = "المنتج",
                TableName = "Products",
                Module = inventory,
                IsActive = true
            });

            db.EntityDefinitions.Add(new EntityDefinition
            {
                Name = "Customer",
                DisplayName = "العميل",
                TableName = "Customers",
                Module = customers,
                IsActive = true
            });

            db.EntityDefinitions.Add(new EntityDefinition
            {
                Name = "SaleInvoice",
                DisplayName = "فاتورة المبيعات",
                TableName = "SaleInvoices",
                Module = sales,
                IsActive = true
            });
        }

        private static void SeedFields(AppDbContext db)
        {
            var product = db.EntityDefinitions.Local.First(e => e.Name == "Product");
            var customer = db.EntityDefinitions.Local.First(e => e.Name == "Customer");
            var sale = db.EntityDefinitions.Local.First(e => e.Name == "SaleInvoice");

            // ── Product fields ──
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = product, Name = "Name", DataType = "string",
                IsRequired = true, IsEditable = true, IsVisible = true,
                DisplayName = "اسم المنتج", OrderIndex = 1, MaxLength = 200
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = product, Name = "Price", DataType = "number",
                IsRequired = true, IsEditable = true, IsVisible = true,
                DisplayName = "السعر", OrderIndex = 2, DefaultValue = "0"
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = product, Name = "Quantity", DataType = "number",
                IsRequired = false, IsEditable = true, IsVisible = true,
                DisplayName = "الكمية", OrderIndex = 3, DefaultValue = "0"
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = product, Name = "Barcode", DataType = "string",
                IsRequired = false, IsEditable = true, IsVisible = true,
                DisplayName = "الباركود", OrderIndex = 4, MaxLength = 50
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = product, Name = "IsActive", DataType = "bool",
                IsRequired = false, IsEditable = true, IsVisible = true,
                DisplayName = "نشط", OrderIndex = 5, DefaultValue = "true"
            });

            // ── Customer fields ──
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = customer, Name = "Name", DataType = "string",
                IsRequired = true, IsEditable = true, IsVisible = true,
                DisplayName = "اسم العميل", OrderIndex = 1, MaxLength = 200
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = customer, Name = "Phone", DataType = "string",
                IsRequired = false, IsEditable = true, IsVisible = true,
                DisplayName = "الهاتف", OrderIndex = 2, MaxLength = 20
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = customer, Name = "Email", DataType = "string",
                IsRequired = false, IsEditable = true, IsVisible = true,
                DisplayName = "البريد الإلكتروني", OrderIndex = 3, MaxLength = 200
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = customer, Name = "Address", DataType = "string",
                IsRequired = false, IsEditable = true, IsVisible = true,
                DisplayName = "العنوان", OrderIndex = 4, MaxLength = 500
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = customer, Name = "Balance", DataType = "number",
                IsRequired = false, IsEditable = false, IsVisible = true,
                DisplayName = "الرصيد", OrderIndex = 5, DefaultValue = "0"
            });

            // ── SaleInvoice fields ──
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = sale, Name = "InvoiceNumber", DataType = "string",
                IsRequired = true, IsEditable = false, IsVisible = true,
                DisplayName = "رقم الفاتورة", OrderIndex = 1, MaxLength = 50
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = sale, Name = "Date", DataType = "date",
                IsRequired = true, IsEditable = true, IsVisible = true,
                DisplayName = "التاريخ", OrderIndex = 2
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = sale, Name = "CustomerId", DataType = "lookup",
                IsRequired = false, IsEditable = true, IsVisible = true,
                DisplayName = "العميل", OrderIndex = 3,
                LookupEntityId = null // will be resolved after SaveChanges
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = sale, Name = "TotalAmount", DataType = "number",
                IsRequired = true, IsEditable = false, IsVisible = true,
                DisplayName = "الإجمالي", OrderIndex = 4, DefaultValue = "0"
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = sale, Name = "Discount", DataType = "number",
                IsRequired = false, IsEditable = true, IsVisible = true,
                DisplayName = "الخصم", OrderIndex = 5, DefaultValue = "0"
            });
            db.FieldDefinitions.Add(new FieldDefinition
            {
                Entity = sale, Name = "IsPaid", DataType = "bool",
                IsRequired = false, IsEditable = true, IsVisible = true,
                DisplayName = "مدفوعة", OrderIndex = 6, DefaultValue = "false"
            });
        }

        private static void SeedRelations(AppDbContext db)
        {
            var sale = db.EntityDefinitions.Local.First(e => e.Name == "SaleInvoice");
            var customer = db.EntityDefinitions.Local.First(e => e.Name == "Customer");

            // SaleInvoice -> Customer (many-to-one)
            db.RelationDefinitions.Add(new RelationDefinition
            {
                SourceEntity = sale,
                TargetEntity = customer,
                RelationType = "many2one",
                FieldName = "CustomerId"
            });

            // Resolve LookupEntityId for CustomerId field now that entities have Ids
            var customerIdField = db.FieldDefinitions.Local
                .FirstOrDefault(f => f.Name == "CustomerId" && f.Entity == sale);
            if (customerIdField != null)
                customerIdField.LookupEntityRef = customer;
        }

        private static void SeedFormLayouts(AppDbContext db)
        {
            var product = db.EntityDefinitions.Local.First(e => e.Name == "Product");
            var customer = db.EntityDefinitions.Local.First(e => e.Name == "Customer");
            var sale = db.EntityDefinitions.Local.First(e => e.Name == "SaleInvoice");

            db.FormLayoutDefinitions.Add(new FormLayoutDefinition
            {
                Entity = product,
                LayoutJson = @"{""rows"":[{""columns"":[{""field"":""Name"",""span"":6},{""field"":""Barcode"",""span"":6}]},{""columns"":[{""field"":""Price"",""span"":4},{""field"":""Quantity"",""span"":4},{""field"":""IsActive"",""span"":4}]}]}"
            });

            db.FormLayoutDefinitions.Add(new FormLayoutDefinition
            {
                Entity = customer,
                LayoutJson = @"{""rows"":[{""columns"":[{""field"":""Name"",""span"":6},{""field"":""Phone"",""span"":6}]},{""columns"":[{""field"":""Email"",""span"":6},{""field"":""Address"",""span"":6}]},{""columns"":[{""field"":""Balance"",""span"":6}]}]}"
            });

            db.FormLayoutDefinitions.Add(new FormLayoutDefinition
            {
                Entity = sale,
                LayoutJson = @"{""rows"":[{""columns"":[{""field"":""InvoiceNumber"",""span"":4},{""field"":""Date"",""span"":4},{""field"":""CustomerId"",""span"":4}]},{""columns"":[{""field"":""TotalAmount"",""span"":4},{""field"":""Discount"",""span"":4},{""field"":""IsPaid"",""span"":4}]}]}"
            });
        }

        private static void SeedGridLayouts(AppDbContext db)
        {
            var product = db.EntityDefinitions.Local.First(e => e.Name == "Product");
            var customer = db.EntityDefinitions.Local.First(e => e.Name == "Customer");
            var sale = db.EntityDefinitions.Local.First(e => e.Name == "SaleInvoice");

            db.GridLayoutDefinitions.Add(new GridLayoutDefinition
            {
                Entity = product,
                ColumnsJson = @"[{""field"":""Name"",""width"":200},{""field"":""Price"",""width"":100},{""field"":""Quantity"",""width"":100},{""field"":""Barcode"",""width"":150},{""field"":""IsActive"",""width"":80}]"
            });

            db.GridLayoutDefinitions.Add(new GridLayoutDefinition
            {
                Entity = customer,
                ColumnsJson = @"[{""field"":""Name"",""width"":200},{""field"":""Phone"",""width"":150},{""field"":""Email"",""width"":200},{""field"":""Balance"",""width"":120}]"
            });

            db.GridLayoutDefinitions.Add(new GridLayoutDefinition
            {
                Entity = sale,
                ColumnsJson = @"[{""field"":""InvoiceNumber"",""width"":150},{""field"":""Date"",""width"":120},{""field"":""CustomerId"",""width"":200},{""field"":""TotalAmount"",""width"":120},{""field"":""IsPaid"",""width"":80}]"
            });
        }

        private static void SeedActions(AppDbContext db)
        {
            var product = db.EntityDefinitions.Local.First(e => e.Name == "Product");
            var customer = db.EntityDefinitions.Local.First(e => e.Name == "Customer");
            var sale = db.EntityDefinitions.Local.First(e => e.Name == "SaleInvoice");

            // Product actions
            db.ActionDefinitions.Add(new ActionDefinition { Entity = product, Name = "CreateProduct", Type = "Create" });
            db.ActionDefinitions.Add(new ActionDefinition { Entity = product, Name = "SaveProduct", Type = "Save" });
            db.ActionDefinitions.Add(new ActionDefinition { Entity = product, Name = "DeleteProduct", Type = "Delete" });

            // Customer actions
            db.ActionDefinitions.Add(new ActionDefinition { Entity = customer, Name = "CreateCustomer", Type = "Create" });
            db.ActionDefinitions.Add(new ActionDefinition { Entity = customer, Name = "SaveCustomer", Type = "Save" });
            db.ActionDefinitions.Add(new ActionDefinition { Entity = customer, Name = "DeleteCustomer", Type = "Delete" });

            // SaleInvoice actions
            db.ActionDefinitions.Add(new ActionDefinition { Entity = sale, Name = "CreateInvoice", Type = "Create" });
            db.ActionDefinitions.Add(new ActionDefinition { Entity = sale, Name = "SaveInvoice", Type = "Save" });
            db.ActionDefinitions.Add(new ActionDefinition { Entity = sale, Name = "DeleteInvoice", Type = "Delete" });
            db.ActionDefinitions.Add(new ActionDefinition { Entity = sale, Name = "ApproveInvoice", Type = "Approve" });
        }
    }
}
