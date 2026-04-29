-- ============================================================
-- Phase 4: Workflow Engine + Phase 4.5: Automation Layer
-- SQL Server Migration Script
-- ============================================================

-- STEP 1: Workflow Schema
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Workflows')
BEGIN
    CREATE TABLE [Workflows] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [EntityId] INT NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Trigger] NVARCHAR(50) NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        CONSTRAINT [FK_Workflows_Entities] FOREIGN KEY ([EntityId]) REFERENCES [Entities]([Id])
    );
    CREATE INDEX [IX_Workflows_EntityId_Trigger] ON [Workflows]([EntityId], [Trigger]) WHERE [IsActive] = 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorkflowRules')
BEGIN
    CREATE TABLE [WorkflowRules] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [WorkflowId] INT NOT NULL,
        [ConditionExpression] NVARCHAR(1000) NULL,
        [Priority] INT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_WorkflowRules_Workflows] FOREIGN KEY ([WorkflowId]) REFERENCES [Workflows]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_WorkflowRules_WorkflowId] ON [WorkflowRules]([WorkflowId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorkflowActions')
BEGIN
    CREATE TABLE [WorkflowActions] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [RuleId] INT NOT NULL,
        [ActionType] NVARCHAR(50) NOT NULL,
        [ParametersJson] NVARCHAR(MAX) NULL,
        CONSTRAINT [FK_WorkflowActions_WorkflowRules] FOREIGN KEY ([RuleId]) REFERENCES [WorkflowRules]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_WorkflowActions_RuleId] ON [WorkflowActions]([RuleId]);
END
GO

-- STEP 6: Workflow Audit Log
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorkflowExecutionLogs')
BEGIN
    CREATE TABLE [WorkflowExecutionLogs] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [WorkflowId] INT NOT NULL,
        [RuleId] INT NULL,
        [ActionId] INT NULL,
        [EntityName] NVARCHAR(200) NOT NULL,
        [EntityId] INT NULL,
        [Trigger] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL,
        [ResultJson] NVARCHAR(MAX) NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [ExecutedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UserId] INT NULL
    );
    CREATE INDEX [IX_WorkflowExecutionLogs_WorkflowId] ON [WorkflowExecutionLogs]([WorkflowId]);
    CREATE INDEX [IX_WorkflowExecutionLogs_ExecutedAt] ON [WorkflowExecutionLogs]([ExecutedAt]);
END
GO

-- STEP 7: Scheduled Jobs
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ScheduledJobs')
BEGIN
    CREATE TABLE [ScheduledJobs] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Name] NVARCHAR(200) NOT NULL,
        [CronExpression] NVARCHAR(100) NOT NULL,
        [ActionName] NVARCHAR(200) NOT NULL,
        [EntityName] NVARCHAR(200) NOT NULL,
        [ParametersJson] NVARCHAR(MAX) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [LastRunAt] DATETIME2 NULL,
        [NextRunAt] DATETIME2 NULL,
        [LastRunStatus] NVARCHAR(50) NULL,
        [LastRunError] NVARCHAR(MAX) NULL
    );
END
GO

-- ============================================================
-- Example: Invoice > 10000 auto "Pending Approval"
-- ============================================================
/*
-- 1. Get the SaleInvoice entity ID
DECLARE @entityId INT = (SELECT Id FROM [Entities] WHERE Name = 'SaleInvoice');

-- 2. Create the workflow
INSERT INTO [Workflows] (EntityId, Name, [Trigger], IsActive)
VALUES (@entityId, 'Auto Approval Check', 'OnCreate', 1);
DECLARE @wfId INT = SCOPE_IDENTITY();

-- 3. Create the rule: Total > 10000
INSERT INTO [WorkflowRules] (WorkflowId, ConditionExpression, Priority)
VALUES (@wfId, 'Total > 10000', 1);
DECLARE @ruleId INT = SCOPE_IDENTITY();

-- 4. Create the action: SetField Status = 'Pending Approval'
INSERT INTO [WorkflowActions] (RuleId, ActionType, ParametersJson)
VALUES (@ruleId, 'SetField', '{"Status": "Pending Approval"}');
*/
