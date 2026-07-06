USE IncentivePortalDb;
GO

IF OBJECT_ID('DealerMonthlyPerformances', 'U') IS NULL
BEGIN
    CREATE TABLE DealerMonthlyPerformances (
        Id INT IDENTITY PRIMARY KEY,
        PartyId INT NOT NULL CONSTRAINT FK_DealerMonthlyPerformances_Parties REFERENCES Parties(Id),
        Month INT NOT NULL,
        Year INT NOT NULL,
        TotalSales DECIMAL(18,2) NOT NULL,
        TotalDiscount DECIMAL(18,2) NOT NULL,
        SlabPercent DECIMAL(9,4) NOT NULL,
        IncentiveEarned DECIMAL(18,2) NOT NULL,
        Outstanding DECIMAL(18,2) NOT NULL,
        GrowthTrend NVARCHAR(100) NOT NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
        UpdatedAt DATETIME2 NULL,
        UpdatedBy NVARCHAR(100) NULL
    );
    CREATE UNIQUE INDEX UX_DealerMonthlyPerformances_PeriodParty ON DealerMonthlyPerformances(Year, Month, PartyId) WHERE IsDeleted = 0;
END
GO

IF OBJECT_ID('IncentiveSummaries', 'U') IS NULL
BEGIN
    CREATE TABLE IncentiveSummaries (
        Id INT IDENTITY PRIMARY KEY,
        PartyId INT NOT NULL CONSTRAINT FK_IncentiveSummaries_Parties REFERENCES Parties(Id),
        Month INT NOT NULL,
        Year INT NOT NULL,
        CurrentSale DECIMAL(18,2) NOT NULL,
        CurrentSlabPercent DECIMAL(9,4) NOT NULL,
        CurrentIncentive DECIMAL(18,2) NOT NULL,
        NextSlabTarget DECIMAL(18,2) NOT NULL,
        AdditionalPurchaseRequired DECIMAL(18,2) NOT NULL,
        NextIncentive DECIMAL(18,2) NOT NULL,
        ForecastedIncentive DECIMAL(18,2) NOT NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
        UpdatedAt DATETIME2 NULL,
        UpdatedBy NVARCHAR(100) NULL
    );
    CREATE UNIQUE INDEX UX_IncentiveSummaries_PeriodParty ON IncentiveSummaries(Year, Month, PartyId) WHERE IsDeleted = 0;
END
GO

IF OBJECT_ID('DealerSlabProgresses', 'U') IS NULL
BEGIN
    CREATE TABLE DealerSlabProgresses (
        Id INT IDENTITY PRIMARY KEY,
        PartyId INT NOT NULL CONSTRAINT FK_DealerSlabProgresses_Parties REFERENCES Parties(Id),
        Month INT NOT NULL,
        Year INT NOT NULL,
        CurrentSale DECIMAL(18,2) NOT NULL,
        NextSlabPercent DECIMAL(9,4) NOT NULL,
        NextSlabTarget DECIMAL(18,2) NOT NULL,
        ProgressPercent DECIMAL(9,4) NOT NULL,
        RemainingAmount DECIMAL(18,2) NOT NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
        UpdatedAt DATETIME2 NULL,
        UpdatedBy NVARCHAR(100) NULL
    );
    CREATE UNIQUE INDEX UX_DealerSlabProgresses_PeriodParty ON DealerSlabProgresses(Year, Month, PartyId) WHERE IsDeleted = 0;
END
GO

IF OBJECT_ID('DealerGrowthAnalytics', 'U') IS NULL
BEGIN
    CREATE TABLE DealerGrowthAnalytics (
        Id INT IDENTITY PRIMARY KEY,
        PartyId INT NOT NULL CONSTRAINT FK_DealerGrowthAnalytics_Parties REFERENCES Parties(Id),
        Month INT NOT NULL,
        Year INT NOT NULL,
        SalesCurrent DECIMAL(18,2) NOT NULL,
        SalesPriorMonth DECIMAL(18,2) NOT NULL,
        SalesPriorYearSamePeriod DECIMAL(18,2) NOT NULL,
        IncentiveCurrent DECIMAL(18,2) NOT NULL,
        IncentivePriorMonth DECIMAL(18,2) NOT NULL,
        IncentivePriorYearSamePeriod DECIMAL(18,2) NOT NULL,
        SalesGrowthMoM DECIMAL(9,4) NOT NULL,
        SalesGrowthYoY DECIMAL(9,4) NOT NULL,
        IncentiveGrowthMoM DECIMAL(9,4) NOT NULL,
        IncentiveGrowthYoY DECIMAL(9,4) NOT NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
        UpdatedAt DATETIME2 NULL,
        UpdatedBy NVARCHAR(100) NULL
    );
    CREATE UNIQUE INDEX UX_DealerGrowthAnalytics_PeriodParty ON DealerGrowthAnalytics(Year, Month, PartyId) WHERE IsDeleted = 0;
END
GO
