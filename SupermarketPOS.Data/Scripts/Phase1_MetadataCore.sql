-- ============================================================================
-- Phase 1: Metadata Core Schema
-- SupermarketPOS ERP Platform
-- 
-- NOTE: These tables are auto-created by EF6 AutomaticMigrations.
-- This script is provided as documentation and for manual deployment scenarios.
-- ============================================================================

-- 1. Modules: logical grouping of entities (e.g., Sales, Inventory, HR)
CREATE TABLE [dbo].[Modules] (
    [Id]       INT            IDENTITY (1, 1) NOT NULL,
    [Name]     NVARCHAR (100) NOT NULL,
    [IsActive] BIT            NOT NULL DEFAULT (1),
    CONSTRAINT [PK_dbo.Modules] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- 2. Entities: business objects described by metadata (e.g., Product, Customer)
CREATE TABLE [dbo].[Entities] (
    [Id]        INT            IDENTITY (1, 1) NOT NULL,
    [Name]      NVARCHAR (100) NOT NULL,
    [TableName] NVARCHAR (100) NOT NULL,
    [ModuleId]  INT            NOT NULL,
    [IsActive]  BIT            NOT NULL DEFAULT (1),
    CONSTRAINT [PK_dbo.Entities] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_dbo.Entities_dbo.Modules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [dbo].[Modules] ([Id])
);
CREATE NONCLUSTERED INDEX [IX_ModuleId] ON [dbo].[Entities] ([ModuleId]);

-- 3. Fields: describes columns/properties of an entity
CREATE TABLE [dbo].[Fields] (
    [Id]          INT            IDENTITY (1, 1) NOT NULL,
    [EntityId]    INT            NOT NULL,
    [Name]        NVARCHAR (100) NOT NULL,
    [DataType]    NVARCHAR (50)  NOT NULL,  -- string, int, decimal, date, bool
    [IsRequired]  BIT            NOT NULL DEFAULT (0),
    [IsEditable]  BIT            NOT NULL DEFAULT (1),
    [IsVisible]   BIT            NOT NULL DEFAULT (1),
    [DisplayName] NVARCHAR (200) NULL,
    [OrderIndex]  INT            NULL,
    CONSTRAINT [PK_dbo.Fields] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_dbo.Fields_dbo.Entities_EntityId] FOREIGN KEY ([EntityId]) REFERENCES [dbo].[Entities] ([Id])
);
CREATE NONCLUSTERED INDEX [IX_EntityId] ON [dbo].[Fields] ([EntityId]);

-- 4. Relations: describes FK relationships between entities
CREATE TABLE [dbo].[Relations] (
    [Id]             INT            IDENTITY (1, 1) NOT NULL,
    [SourceEntityId] INT            NOT NULL,
    [TargetEntityId] INT            NOT NULL,
    [RelationType]   NVARCHAR (50)  NOT NULL,  -- many2one, one2many
    [FieldName]      NVARCHAR (100) NULL,
    CONSTRAINT [PK_dbo.Relations] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_dbo.Relations_dbo.Entities_SourceEntityId] FOREIGN KEY ([SourceEntityId]) REFERENCES [dbo].[Entities] ([Id]),
    CONSTRAINT [FK_dbo.Relations_dbo.Entities_TargetEntityId] FOREIGN KEY ([TargetEntityId]) REFERENCES [dbo].[Entities] ([Id])
);
CREATE NONCLUSTERED INDEX [IX_SourceEntityId] ON [dbo].[Relations] ([SourceEntityId]);
CREATE NONCLUSTERED INDEX [IX_TargetEntityId] ON [dbo].[Relations] ([TargetEntityId]);

-- ============================================================================
-- Seed Data
-- ============================================================================

-- Modules
INSERT INTO [dbo].[Modules] ([Name], [IsActive]) VALUES (N'Inventory', 1);
INSERT INTO [dbo].[Modules] ([Name], [IsActive]) VALUES (N'Sales', 1);
INSERT INTO [dbo].[Modules] ([Name], [IsActive]) VALUES (N'Customers', 1);

-- Entities
DECLARE @InventoryId INT = (SELECT Id FROM [dbo].[Modules] WHERE Name = 'Inventory');
DECLARE @SalesId INT = (SELECT Id FROM [dbo].[Modules] WHERE Name = 'Sales');
DECLARE @CustomersId INT = (SELECT Id FROM [dbo].[Modules] WHERE Name = 'Customers');

INSERT INTO [dbo].[Entities] ([Name], [TableName], [ModuleId], [IsActive]) VALUES (N'Product', N'Products', @InventoryId, 1);
INSERT INTO [dbo].[Entities] ([Name], [TableName], [ModuleId], [IsActive]) VALUES (N'Customer', N'Customers', @CustomersId, 1);
INSERT INTO [dbo].[Entities] ([Name], [TableName], [ModuleId], [IsActive]) VALUES (N'SaleInvoice', N'SaleInvoices', @SalesId, 1);

-- Fields
DECLARE @ProductId INT = (SELECT Id FROM [dbo].[Entities] WHERE Name = 'Product');
DECLARE @CustomerId INT = (SELECT Id FROM [dbo].[Entities] WHERE Name = 'Customer');
DECLARE @SaleId INT = (SELECT Id FROM [dbo].[Entities] WHERE Name = 'SaleInvoice');

-- Product fields
INSERT INTO [dbo].[Fields] ([EntityId],[Name],[DataType],[IsRequired],[IsEditable],[IsVisible],[DisplayName],[OrderIndex])
VALUES (@ProductId, N'Name', N'string', 1, 1, 1, N'اسم المنتج', 1);
INSERT INTO [dbo].[Fields] ([EntityId],[Name],[DataType],[IsRequired],[IsEditable],[IsVisible],[DisplayName],[OrderIndex])
VALUES (@ProductId, N'Price', N'decimal', 1, 1, 1, N'السعر', 2);
INSERT INTO [dbo].[Fields] ([EntityId],[Name],[DataType],[IsRequired],[IsEditable],[IsVisible],[DisplayName],[OrderIndex])
VALUES (@ProductId, N'Quantity', N'int', 0, 1, 1, N'الكمية', 3);

-- Customer fields
INSERT INTO [dbo].[Fields] ([EntityId],[Name],[DataType],[IsRequired],[IsEditable],[IsVisible],[DisplayName],[OrderIndex])
VALUES (@CustomerId, N'Name', N'string', 1, 1, 1, N'اسم العميل', 1);
INSERT INTO [dbo].[Fields] ([EntityId],[Name],[DataType],[IsRequired],[IsEditable],[IsVisible],[DisplayName],[OrderIndex])
VALUES (@CustomerId, N'Phone', N'string', 0, 1, 1, N'الهاتف', 2);

-- SaleInvoice fields
INSERT INTO [dbo].[Fields] ([EntityId],[Name],[DataType],[IsRequired],[IsEditable],[IsVisible],[DisplayName],[OrderIndex])
VALUES (@SaleId, N'InvoiceNumber', N'string', 1, 0, 1, N'رقم الفاتورة', 1);
INSERT INTO [dbo].[Fields] ([EntityId],[Name],[DataType],[IsRequired],[IsEditable],[IsVisible],[DisplayName],[OrderIndex])
VALUES (@SaleId, N'TotalAmount', N'decimal', 1, 0, 1, N'الإجمالي', 2);
INSERT INTO [dbo].[Fields] ([EntityId],[Name],[DataType],[IsRequired],[IsEditable],[IsVisible],[DisplayName],[OrderIndex])
VALUES (@SaleId, N'SaleDate', N'date', 1, 0, 1, N'تاريخ البيع', 3);

-- Relations
INSERT INTO [dbo].[Relations] ([SourceEntityId],[TargetEntityId],[RelationType],[FieldName])
VALUES (@SaleId, @CustomerId, N'many2one', N'CustomerId');
