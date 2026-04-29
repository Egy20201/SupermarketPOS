-- ============================================================
-- Phase 5: Observability + Control Layer
-- SQL Server Migration Script
-- ============================================================

-- STEP 1: Execution Trace System
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ExecutionTraces')
BEGIN
    CREATE TABLE [ExecutionTraces] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [CorrelationId] NVARCHAR(16) NULL,
        [EntityName] NVARCHAR(200) NOT NULL,
        [Operation] NVARCHAR(50) NOT NULL,
        [StartTime] DATETIME2 NOT NULL,
        [EndTime] DATETIME2 NULL,
        [DurationMs] BIGINT NULL,
        [Status] NVARCHAR(20) NOT NULL DEFAULT 'Running',
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [ErrorClassification] NVARCHAR(50) NULL,
        [UserId] INT NULL
    );
    CREATE INDEX [IX_ExecutionTraces_CorrelationId] ON [ExecutionTraces]([CorrelationId]);
    CREATE INDEX [IX_ExecutionTraces_Status_StartTime] ON [ExecutionTraces]([Status], [StartTime]);
    CREATE INDEX [IX_ExecutionTraces_EntityName] ON [ExecutionTraces]([EntityName]);
END
GO

-- STEP 3: Extend WorkflowExecutionLogs
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkflowExecutionLogs') AND name = 'CorrelationId')
BEGIN
    ALTER TABLE [WorkflowExecutionLogs] ADD [CorrelationId] NVARCHAR(16) NULL;
    ALTER TABLE [WorkflowExecutionLogs] ADD [StepOrder] INT NOT NULL DEFAULT 0;
    CREATE INDEX [IX_WorkflowExecutionLogs_CorrelationId] ON [WorkflowExecutionLogs]([CorrelationId]);
END
GO

-- STEP 4: Dead Letter Queue
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DeadLetterEvents')
BEGIN
    CREATE TABLE [DeadLetterEvents] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [SourceType] NVARCHAR(50) NOT NULL,
        [SourceEventId] INT NULL,
        [Payload] NVARCHAR(MAX) NOT NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [RetryCount] INT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ReplayedAt] DATETIME2 NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Dead',
        [CorrelationId] NVARCHAR(16) NULL
    );
    CREATE INDEX [IX_DeadLetterEvents_Status] ON [DeadLetterEvents]([Status]);
    CREATE INDEX [IX_DeadLetterEvents_CorrelationId] ON [DeadLetterEvents]([CorrelationId]);
END
GO
