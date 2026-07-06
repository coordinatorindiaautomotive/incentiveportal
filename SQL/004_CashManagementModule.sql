-- ============================================================
-- 004_CashManagementModule.sql
-- Cash In / Cash Out / Reconciliation / Exception / Period Control tables
-- Run after 003_AnalyticsTables.sql
-- ============================================================

-- ─────────────────────────────────────────────
-- Cash In Transactions
-- ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CashInTransactions]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[CashInTransactions] (
        [Id]                INT            IDENTITY(1,1) NOT NULL,
        [TransactionNo]     NVARCHAR(30)   NOT NULL DEFAULT '',
        [BranchId]          INT            NOT NULL,
        [TransactionDate]   DATETIME2      NOT NULL,
        [ReceiptType]       NVARCHAR(60)   NOT NULL DEFAULT '',
        [CustomerName]      NVARCHAR(180)  NOT NULL DEFAULT '',
        [DealerCode]        NVARCHAR(40)   NOT NULL DEFAULT '',
        [Amount]            DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [PaymentMode]       NVARCHAR(30)   NOT NULL DEFAULT '',
        [BankName]          NVARCHAR(120)  NOT NULL DEFAULT '',
        [ReferenceNo]       NVARCHAR(80)   NOT NULL DEFAULT '',
        [Narration]         NVARCHAR(500)  NOT NULL DEFAULT '',
        [AttachmentPath]    NVARCHAR(260)  NULL,
        [Status]            NVARCHAR(20)   NOT NULL DEFAULT 'Draft',
        [TallyVoucherNo]    NVARCHAR(30)   NULL,
        [TallyVoucherType]  NVARCHAR(20)   NULL,
        [TallyLedgerName]   NVARCHAR(80)   NULL,
        [TallyGuid]         NVARCHAR(80)   NULL,
        [TallySyncAt]       DATETIME2      NULL,
        [TallySyncStatus]   NVARCHAR(20)   NULL,
        [ApprovalRemarks]   NVARCHAR(200)  NULL,
        [ApprovedAt]        DATETIME2      NULL,
        [ApprovedBy]        NVARCHAR(100)  NULL,
        [IsDeleted]         BIT            NOT NULL DEFAULT 0,
        [CreatedAt]         DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy]         NVARCHAR(100)  NOT NULL DEFAULT 'system',
        [UpdatedAt]         DATETIME2      NULL,
        [UpdatedBy]         NVARCHAR(100)  NULL,
        CONSTRAINT [PK_CashInTransactions] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_CashIn_Branches] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branches]([Id])
    );
    CREATE INDEX [IX_CashIn_BranchId]        ON [dbo].[CashInTransactions] ([BranchId]);
    CREATE INDEX [IX_CashIn_Status]           ON [dbo].[CashInTransactions] ([Status]);
    CREATE INDEX [IX_CashIn_TransactionDate]  ON [dbo].[CashInTransactions] ([TransactionDate]);
    PRINT 'Created table: CashInTransactions';
END
GO

-- ─────────────────────────────────────────────
-- Cash Out Transactions
-- ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CashOutTransactions]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[CashOutTransactions] (
        [Id]                INT            IDENTITY(1,1) NOT NULL,
        [TransactionNo]     NVARCHAR(30)   NOT NULL DEFAULT '',
        [BranchId]          INT            NOT NULL,
        [TransactionDate]   DATETIME2      NOT NULL,
        [ExpenseCategory]   NVARCHAR(60)   NOT NULL DEFAULT '',
        [VendorName]        NVARCHAR(180)  NOT NULL DEFAULT '',
        [CostCenter]        NVARCHAR(60)   NOT NULL DEFAULT '',
        [GlAccount]         NVARCHAR(80)   NOT NULL DEFAULT '',
        [Amount]            DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [PaymentMode]       NVARCHAR(30)   NOT NULL DEFAULT '',
        [PaymentInstrument] NVARCHAR(60)   NOT NULL DEFAULT '',
        [BankName]          NVARCHAR(120)  NOT NULL DEFAULT '',
        [ReferenceNo]       NVARCHAR(80)   NOT NULL DEFAULT '',
        [Narration]         NVARCHAR(500)  NOT NULL DEFAULT '',
        [AttachmentPath]    NVARCHAR(260)  NULL,
        [Status]            NVARCHAR(20)   NOT NULL DEFAULT 'Draft',
        [TallyVoucherNo]    NVARCHAR(30)   NULL,
        [TallyGuid]         NVARCHAR(80)   NULL,
        [TallySyncAt]       DATETIME2      NULL,
        [TallySyncStatus]   NVARCHAR(20)   NULL,
        [ApprovalRemarks]   NVARCHAR(200)  NULL,
        [ApprovedAt]        DATETIME2      NULL,
        [ApprovedBy]        NVARCHAR(100)  NULL,
        [IsDeleted]         BIT            NOT NULL DEFAULT 0,
        [CreatedAt]         DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy]         NVARCHAR(100)  NOT NULL DEFAULT 'system',
        [UpdatedAt]         DATETIME2      NULL,
        [UpdatedBy]         NVARCHAR(100)  NULL,
        CONSTRAINT [PK_CashOutTransactions] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_CashOut_Branches] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branches]([Id])
    );
    CREATE INDEX [IX_CashOut_BranchId]       ON [dbo].[CashOutTransactions] ([BranchId]);
    CREATE INDEX [IX_CashOut_Status]          ON [dbo].[CashOutTransactions] ([Status]);
    CREATE INDEX [IX_CashOut_TransactionDate] ON [dbo].[CashOutTransactions] ([TransactionDate]);
    PRINT 'Created table: CashOutTransactions';
END
GO

-- ─────────────────────────────────────────────
-- Cash Reconciliation Records
-- ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CashReconRecords]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[CashReconRecords] (
        [Id]               INT           IDENTITY(1,1) NOT NULL,
        [ReconRef]         NVARCHAR(30)  NOT NULL DEFAULT '',
        [BranchId]         INT           NOT NULL,
        [ReconDate]        DATETIME2     NOT NULL,
        [TransactionType]  NVARCHAR(20)  NOT NULL DEFAULT 'CashIn',
        [CashInId]         INT           NULL,
        [CashOutId]        INT           NULL,
        [PortalAmount]     DECIMAL(18,2) NOT NULL DEFAULT 0,
        [TallyVoucherNo]   NVARCHAR(40)  NULL,
        [TallyAmount]      DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Variance]         DECIMAL(18,2) NOT NULL DEFAULT 0,
        [ReconStatus]      NVARCHAR(20)  NOT NULL DEFAULT 'Pending',
        [Remarks]          NVARCHAR(500) NULL,
        [ApprovedAt]       DATETIME2     NULL,
        [ApprovedBy]       NVARCHAR(100) NULL,
        [IsDeleted]        BIT           NOT NULL DEFAULT 0,
        [CreatedAt]        DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy]        NVARCHAR(100) NOT NULL DEFAULT 'system',
        [UpdatedAt]        DATETIME2     NULL,
        [UpdatedBy]        NVARCHAR(100) NULL,
        CONSTRAINT [PK_CashReconRecords] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Recon_Branches]  FOREIGN KEY ([BranchId])  REFERENCES [dbo].[Branches]([Id]),
        CONSTRAINT [FK_Recon_CashIn]    FOREIGN KEY ([CashInId])   REFERENCES [dbo].[CashInTransactions]([Id]),
        CONSTRAINT [FK_Recon_CashOut]   FOREIGN KEY ([CashOutId])  REFERENCES [dbo].[CashOutTransactions]([Id])
    );
    CREATE INDEX [IX_Recon_BranchId]   ON [dbo].[CashReconRecords] ([BranchId]);
    CREATE INDEX [IX_Recon_Status]     ON [dbo].[CashReconRecords] ([ReconStatus]);
    PRINT 'Created table: CashReconRecords';
END
GO

-- ─────────────────────────────────────────────
-- Cash Exceptions
-- ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CashExceptions]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[CashExceptions] (
        [Id]            INT           IDENTITY(1,1) NOT NULL,
        [ExceptionRef]  NVARCHAR(30)  NOT NULL DEFAULT '',
        [BranchId]      INT           NOT NULL,
        [ExceptionDate] DATETIME2     NOT NULL,
        [ExceptionType] NVARCHAR(60)  NOT NULL DEFAULT '',
        [Description]   NVARCHAR(500) NOT NULL DEFAULT '',
        [Amount]        DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Severity]      NVARCHAR(20)  NOT NULL DEFAULT 'Medium',
        [Status]        NVARCHAR(20)  NOT NULL DEFAULT 'Open',
        [AssignedTo]    NVARCHAR(100) NULL,
        [Resolution]    NVARCHAR(500) NULL,
        [ResolvedAt]    DATETIME2     NULL,
        [ResolvedBy]    NVARCHAR(100) NULL,
        [IsDeleted]     BIT           NOT NULL DEFAULT 0,
        [CreatedAt]     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy]     NVARCHAR(100) NOT NULL DEFAULT 'system',
        [UpdatedAt]     DATETIME2     NULL,
        [UpdatedBy]     NVARCHAR(100) NULL,
        CONSTRAINT [PK_CashExceptions] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_CashEx_Branches] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branches]([Id])
    );
    CREATE INDEX [IX_CashEx_BranchId] ON [dbo].[CashExceptions] ([BranchId]);
    CREATE INDEX [IX_CashEx_Severity] ON [dbo].[CashExceptions] ([Severity]);
    CREATE INDEX [IX_CashEx_Status]   ON [dbo].[CashExceptions] ([Status]);
    PRINT 'Created table: CashExceptions';
END
GO

-- ─────────────────────────────────────────────
-- Period Controls
-- ─────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CashPeriodControls]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[CashPeriodControls] (
        [Id]           INT           IDENTITY(1,1) NOT NULL,
        [ControlYear]  INT           NOT NULL,
        [ControlMonth] INT           NOT NULL,
        [Status]       NVARCHAR(20)  NOT NULL DEFAULT 'Open',
        [ClosedAt]     DATETIME2     NULL,
        [ClosedBy]     NVARCHAR(100) NULL,
        [UnlockReason] NVARCHAR(500) NULL,
        [IsDeleted]    BIT           NOT NULL DEFAULT 0,
        [CreatedAt]    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy]    NVARCHAR(100) NOT NULL DEFAULT 'system',
        [UpdatedAt]    DATETIME2     NULL,
        [UpdatedBy]    NVARCHAR(100) NULL,
        CONSTRAINT [PK_CashPeriodControls] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE UNIQUE INDEX [IX_PeriodControls_YearMonth] ON [dbo].[CashPeriodControls] ([ControlYear],[ControlMonth]) WHERE [IsDeleted] = 0;
    PRINT 'Created table: CashPeriodControls';
END
GO

PRINT '004_CashManagementModule.sql executed successfully.';
GO
