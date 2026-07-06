CREATE DATABASE IncentivePortalDb;
GO
USE IncentivePortalDb;
GO

CREATE TABLE Roles (
    Id INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(60) NOT NULL UNIQUE,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL
);

CREATE TABLE Branches (
    Id INT IDENTITY PRIMARY KEY,
    Code NVARCHAR(20) NOT NULL UNIQUE,
    Name NVARCHAR(120) NOT NULL,
    Region NVARCHAR(120) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL
);

CREATE TABLE Users (
    Id INT IDENTITY PRIMARY KEY,
    UserName NVARCHAR(80) NOT NULL UNIQUE,
    Email NVARCHAR(160) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256) NOT NULL,
    PasswordSalt NVARCHAR(256) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    BranchId INT NULL CONSTRAINT FK_Users_Branches REFERENCES Branches(Id),
    LastLoginAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL
);

CREATE TABLE UserRoles (
    UserId INT NOT NULL CONSTRAINT FK_UserRoles_Users REFERENCES Users(Id),
    RoleId INT NOT NULL CONSTRAINT FK_UserRoles_Roles REFERENCES Roles(Id),
    CONSTRAINT PK_UserRoles PRIMARY KEY(UserId, RoleId)
);

CREATE TABLE Parties (
    Id INT IDENTITY PRIMARY KEY,
    PartyCode NVARCHAR(40) NOT NULL UNIQUE,
    PartyName NVARCHAR(180) NOT NULL,
    GST NVARCHAR(20) NOT NULL,
    Mobile NVARCHAR(20) NOT NULL,
    Address NVARCHAR(500) NOT NULL,
    BranchId INT NOT NULL CONSTRAINT FK_Parties_Branches REFERENCES Branches(Id),
    DealerType NVARCHAR(40) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL
);
CREATE INDEX IX_Parties_BranchId ON Parties(BranchId);

CREATE TABLE BankDetails (
    Id INT IDENTITY PRIMARY KEY,
    PartyId INT NOT NULL CONSTRAINT FK_BankDetails_Parties REFERENCES Parties(Id),
    AccountHolder NVARCHAR(160) NOT NULL,
    AccountNumber NVARCHAR(30) NOT NULL,
    IFSC NVARCHAR(15) NOT NULL,
    BankName NVARCHAR(120) NOT NULL,
    BranchName NVARCHAR(120) NOT NULL,
    ApprovalStatus NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    IsPrimary BIT NOT NULL DEFAULT 1,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL
);
CREATE UNIQUE INDEX UX_BankDetails_AccountNumber ON BankDetails(AccountNumber) WHERE IsDeleted = 0;

CREATE TABLE BankApprovalRequests (
    Id INT IDENTITY PRIMARY KEY,
    PartyId INT NOT NULL CONSTRAINT FK_BankApprovalRequests_Parties REFERENCES Parties(Id),
    BankDetailId INT NULL CONSTRAINT FK_BankApprovalRequests_BankDetails REFERENCES BankDetails(Id),
    RequestType NVARCHAR(40) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    OldJson NVARCHAR(MAX) NOT NULL,
    NewJson NVARCHAR(MAX) NOT NULL,
    Remarks NVARCHAR(500) NULL,
    ApprovedAt DATETIME2 NULL,
    ApprovedBy NVARCHAR(100) NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL
);

CREATE TABLE IncentiveSchemes (
    Id INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(120) NOT NULL,
    SchemeMonth INT NOT NULL,
    SchemeYear INT NOT NULL,
    Version INT NOT NULL DEFAULT 1,
    EffectiveFrom DATE NOT NULL,
    EffectiveTo DATE NOT NULL,
    IsLocked BIT NOT NULL DEFAULT 0,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL,
    CONSTRAINT CK_IncentiveSchemes_Month CHECK (SchemeMonth BETWEEN 1 AND 12),
    CONSTRAINT CK_IncentiveSchemes_Effective CHECK (EffectiveFrom <= EffectiveTo)
);
CREATE UNIQUE INDEX UX_IncentiveSchemes_PeriodVersion ON IncentiveSchemes(SchemeYear, SchemeMonth, Version) WHERE IsDeleted = 0;

CREATE TABLE IncentiveSchemeDetails (
    Id INT IDENTITY PRIMARY KEY,
    IncentiveSchemeId INT NOT NULL CONSTRAINT FK_IncentiveSchemeDetails_Schemes REFERENCES IncentiveSchemes(Id),
    MinAchievementPercent DECIMAL(18,2) NOT NULL,
    MaxAchievementPercent DECIMAL(18,2) NOT NULL,
    FixedAmount DECIMAL(18,2) NULL,
    Percentage DECIMAL(9,4) NULL,
    RuleName NVARCHAR(250) NOT NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL,
    CONSTRAINT CK_IncentiveSchemeDetails_Range CHECK (MinAchievementPercent <= MaxAchievementPercent)
);

CREATE TABLE ImportLogs (
    Id INT IDENTITY PRIMARY KEY,
    ImportType NVARCHAR(80) NOT NULL,
    FileName NVARCHAR(260) NOT NULL,
    TotalRows INT NOT NULL DEFAULT 0,
    SuccessRows INT NOT NULL DEFAULT 0,
    FailedRows INT NOT NULL DEFAULT 0,
    Status NVARCHAR(20) NOT NULL,
    ErrorJson NVARCHAR(MAX) NOT NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL
);

CREATE TABLE MonthlySales (
    Id INT IDENTITY PRIMARY KEY,
    Month INT NOT NULL,
    Year INT NOT NULL,
    SourceLocation NVARCHAR(40) NOT NULL DEFAULT '',
    PartyId INT NOT NULL CONSTRAINT FK_MonthlySales_Parties REFERENCES Parties(Id),
    SaleValue DECIMAL(18,2) NOT NULL,
    Discount DECIMAL(18,2) NOT NULL,
    Outstanding DECIMAL(18,2) NOT NULL,
    AchievementPercent DECIMAL(18,2) NOT NULL,
    ImportedSlabPercent DECIMAL(9,4) NOT NULL DEFAULT 0,
    ImportedIncentive DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsLocked BIT NOT NULL DEFAULT 0,
    ImportLogId INT NULL CONSTRAINT FK_MonthlySales_ImportLogs REFERENCES ImportLogs(Id),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL,
    CONSTRAINT CK_MonthlySales_Month CHECK (Month BETWEEN 1 AND 12)
);
CREATE UNIQUE INDEX UX_MonthlySales_PeriodParty ON MonthlySales(Year, Month, PartyId) WHERE IsDeleted = 0;

CREATE TABLE IncentiveCalculations (
    Id INT IDENTITY PRIMARY KEY,
    MonthlySaleId INT NOT NULL CONSTRAINT FK_IncentiveCalculations_MonthlySales REFERENCES MonthlySales(Id),
    IncentiveSchemeDetailId INT NOT NULL CONSTRAINT FK_IncentiveCalculations_Slabs REFERENCES IncentiveSchemeDetails(Id),
    GrossIncentive DECIMAL(18,2) NOT NULL,
    AdjustedAmount DECIMAL(18,2) NOT NULL,
    TransferAmount DECIMAL(18,2) NOT NULL,
    SnapshotJson NVARCHAR(MAX) NOT NULL,
    IsLocked BIT NOT NULL DEFAULT 0,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL
);
CREATE UNIQUE INDEX UX_IncentiveCalculations_MonthlySale ON IncentiveCalculations(MonthlySaleId) WHERE IsDeleted = 0;

CREATE TABLE AdjustmentLedger (
    Id INT IDENTITY PRIMARY KEY,
    IncentiveCalculationId INT NOT NULL CONSTRAINT FK_AdjustmentLedger_Calculations REFERENCES IncentiveCalculations(Id),
    OpeningOutstanding DECIMAL(18,2) NOT NULL,
    AdjustedAmount DECIMAL(18,2) NOT NULL,
    CarryForward DECIMAL(18,2) NOT NULL,
    TransferEligibleAmount DECIMAL(18,2) NOT NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL
);

CREATE TABLE TransferEntries (
    Id INT IDENTITY PRIMARY KEY,
    IncentiveCalculationId INT NOT NULL CONSTRAINT FK_TransferEntries_Calculations REFERENCES IncentiveCalculations(Id),
    TransferMode NVARCHAR(20) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    UTR NVARCHAR(60) NULL,
    Amount DECIMAL(18,2) NOT NULL,
    TransferDate DATETIME2 NULL,
    ReconciledAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL
);
CREATE UNIQUE INDEX UX_TransferEntries_UTR ON TransferEntries(UTR) WHERE UTR IS NOT NULL AND IsDeleted = 0;

CREATE TABLE AuditLogs (
    Id BIGINT IDENTITY PRIMARY KEY,
    EntityName NVARCHAR(120) NOT NULL,
    EntityId NVARCHAR(40) NOT NULL,
    Action NVARCHAR(40) NOT NULL,
    OldValue NVARCHAR(MAX) NOT NULL,
    NewValue NVARCHAR(MAX) NOT NULL,
    ChangedBy NVARCHAR(100) NOT NULL,
    ChangedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    IpAddress NVARCHAR(60) NULL
);
CREATE INDEX IX_AuditLogs_Entity ON AuditLogs(EntityName, EntityId);
GO

INSERT INTO Roles(Name) VALUES ('Super Admin'), ('HO Finance'), ('Branch Manager'), ('Associate'), ('Auditor');
INSERT INTO Branches(Code, Name, Region) VALUES ('HO', 'Head Office', 'Corporate'), ('BR-N', 'North Branch', 'North');
GO
