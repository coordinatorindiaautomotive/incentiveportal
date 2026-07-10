using IncentivePortal.Helpers;
using IncentivePortal.Models;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Data;

public sealed class SeedDataInitializer(IncentiveDbContext db, IPasswordHasher hasher, IConfiguration configuration)
{
    public async Task EnsureSeedDataAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("SeedData:Enabled", true))
            return;

        await db.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureUserLockoutColumnsAsync(cancellationToken);
        // await InspectExcelMetadataAsync(cancellationToken); // Skipped to avoid slow startup
        await EnsureBranchesMetadataColumnsAsync(cancellationToken);
        await SeedOperationalBranchesAsync(cancellationToken);
        await EnsureSlabMasterSchemeAsync(cancellationToken);
        await EnsurePartiesFixedIncentivePercentColumnAsync(cancellationToken);
        await EnsureBankDetailsPANAndMobileColumnsAsync(cancellationToken);
        await EnsureAnalyticsTablesAsync(cancellationToken);
        // await EnsureLedgerTablesAsync(cancellationToken); // Deleted legacy tables
        await EnsureDatabaseOptimizationsAsync(cancellationToken);
        await EnsureAnnouncementsTableAsync(cancellationToken);
        await EnsurePortalSettingsAsync(cancellationToken);
        await EnsureCategorySalesAggregatesTableAsync(cancellationToken);
        await EnsureRawTableAsync(cancellationToken);
        await EnsureSsIncentivesTableAsync(cancellationToken);
        await SeedDealerTypesAsync(cancellationToken);
        await EnsureIncentivePeriodsTableAsync(cancellationToken);

        await EnsureCashMasterItemsAsync(cancellationToken);
        await EnsureCostCenterCashesTableAsync(cancellationToken);
        await EnsureSaasTablesAsync(cancellationToken);
        await EnsureDynamicReportBuilderTablesAsync(cancellationToken);
        await EnsureItPortalTablesAsync(cancellationToken);
        await EnsureControlTowerTablesAsync(cancellationToken);
        await EnsureEngineTablesAsync(cancellationToken);

        if (!await db.Users.AnyAsync(x => x.UserName == "admin", cancellationToken))
        {
            var password = configuration["SeedData:AdminPassword"] ?? "Admin@123";
            if (string.IsNullOrWhiteSpace(password) || password.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            {
                password = "Admin@123";
            }
            var (hash, salt) = hasher.HashPassword(password);
            var user = new User
            {
                UserName = "admin",
                Email = "admin@company.local",
                PasswordHash = hash,
                PasswordSalt = salt,
                BranchId = 1,
                UserRoles = new List<UserRole> { new() { RoleId = 1 } }
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.Roles.AnyAsync(x => x.Id == 6 || x.Name == AppRoles.SalesExecutive, cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Roles ON; INSERT INTO Roles (Id, Name, IsDeleted, CreatedAt, CreatedBy) VALUES (6, 'Sales Executive', 0, SYSUTCDATETIME(), 'system'); SET IDENTITY_INSERT Roles OFF;", cancellationToken);
        }

        if (!await db.Users.AnyAsync(x => x.UserName == "ANIL", cancellationToken))
        {
            var (hash, salt) = hasher.HashPassword("Sales@123");
            var user = new User
            {
                UserName = "ANIL",
                Email = "anil.pareek@company.local",
                PasswordHash = hash,
                PasswordSalt = salt,
                BranchId = 1, // Head Office
                UserRoles = new List<UserRole> { new() { RoleId = 6 } } // Sales Executive
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureUserLockoutColumnsAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('Users', 'FailedLoginAttempts') IS NULL
                ALTER TABLE Users ADD FailedLoginAttempts INT NOT NULL CONSTRAINT DF_Users_FailedLoginAttempts DEFAULT(0);
            IF COL_LENGTH('Users', 'LockoutEnd') IS NULL
                ALTER TABLE Users ADD LockoutEnd DATETIME2 NULL;
            """, cancellationToken);
    }

    private async Task EnsureIncentivePeriodsTableAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('IncentivePeriods', 'U') IS NULL
            BEGIN
                CREATE TABLE IncentivePeriods (
                    Id INT IDENTITY PRIMARY KEY,
                    [Month] INT NOT NULL,
                    [Year] INT NOT NULL,
                    SourceType NVARCHAR(50) NOT NULL CONSTRAINT DF_IncentivePeriods_SourceType DEFAULT 'Dynamic',
                    [Status] NVARCHAR(50) NOT NULL CONSTRAINT DF_IncentivePeriods_Status DEFAULT 'Draft',
                    LockedFlag BIT NOT NULL CONSTRAINT DF_IncentivePeriods_LockedFlag DEFAULT 0,
                    LockedBy NVARCHAR(100) NULL,
                    LockedDate DATETIME2 NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_IncentivePeriods_Period ON IncentivePeriods(Year, Month) WHERE IsDeleted = 0;
            END
            """, cancellationToken);
    }





    private async Task EnsurePartiesFixedIncentivePercentColumnAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('Parties', 'FixedIncentivePercent') IS NULL
                ALTER TABLE Parties ADD FixedIncentivePercent decimal(9,4) NOT NULL CONSTRAINT DF_Parties_FixedIncentivePercent DEFAULT(0);
            IF COL_LENGTH('Parties', 'OriginalPartyCode') IS NULL
                ALTER TABLE Parties ADD OriginalPartyCode nvarchar(40) NOT NULL CONSTRAINT DF_Parties_OriginalPartyCode DEFAULT('');
            """, cancellationToken);
    }

    private async Task EnsureBankDetailsPANAndMobileColumnsAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('BankDetails', 'PAN') IS NULL
                ALTER TABLE BankDetails ADD PAN nvarchar(20) NOT NULL CONSTRAINT DF_BankDetails_PAN DEFAULT('');
            IF COL_LENGTH('BankDetails', 'Mobile') IS NULL
                ALTER TABLE BankDetails ADD Mobile nvarchar(20) NOT NULL CONSTRAINT DF_BankDetails_Mobile DEFAULT('');
            """, cancellationToken);
    }

    private async Task EnsureAnalyticsTablesAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
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
            """, cancellationToken);
    }

    private async Task EnsureBranchesMetadataColumnsAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('Branches', 'BranchType') IS NULL
                ALTER TABLE Branches ADD BranchType nvarchar(20) NOT NULL CONSTRAINT DF_Branches_BranchType DEFAULT('');
            IF COL_LENGTH('Branches', 'Consignee') IS NULL
                ALTER TABLE Branches ADD Consignee nvarchar(40) NOT NULL CONSTRAINT DF_Branches_Consignee DEFAULT('');
            IF COL_LENGTH('Branches', 'Incharge') IS NULL
                ALTER TABLE Branches ADD Incharge nvarchar(100) NOT NULL CONSTRAINT DF_Branches_Incharge DEFAULT('');
            IF COL_LENGTH('Branches', 'MobileNo') IS NULL
                ALTER TABLE Branches ADD MobileNo nvarchar(30) NOT NULL CONSTRAINT DF_Branches_MobileNo DEFAULT('');
            IF COL_LENGTH('Branches', 'EmailID') IS NULL
                ALTER TABLE Branches ADD EmailID nvarchar(120) NOT NULL CONSTRAINT DF_Branches_EmailID DEFAULT('');
            IF COL_LENGTH('Branches', 'Address') IS NULL
                ALTER TABLE Branches ADD Address nvarchar(500) NOT NULL CONSTRAINT DF_Branches_Address DEFAULT('');
            IF COL_LENGTH('Branches', 'OpeningYear') IS NULL
                ALTER TABLE Branches ADD OpeningYear nvarchar(30) NOT NULL CONSTRAINT DF_Branches_OpeningYear DEFAULT('');
            IF COL_LENGTH('Branches', 'Area') IS NULL
                ALTER TABLE Branches ADD Area nvarchar(30) NOT NULL CONSTRAINT DF_Branches_Area DEFAULT('');
            IF COL_LENGTH('Branches', 'Longitude') IS NULL
                ALTER TABLE Branches ADD Longitude nvarchar(30) NOT NULL CONSTRAINT DF_Branches_Longitude DEFAULT('');
            IF COL_LENGTH('Branches', 'Latitude') IS NULL
                ALTER TABLE Branches ADD Latitude nvarchar(30) NOT NULL CONSTRAINT DF_Branches_Latitude DEFAULT('');
            IF COL_LENGTH('Branches', 'AllowedCategories') IS NULL
                ALTER TABLE Branches ADD AllowedCategories nvarchar(100) NOT NULL CONSTRAINT DF_Branches_AllowedCategories DEFAULT('AA,M');
            IF COL_LENGTH('Branches', 'AllowedPartyTypes') IS NULL
                ALTER TABLE Branches ADD AllowedPartyTypes nvarchar(200) NOT NULL CONSTRAINT DF_Branches_AllowedPartyTypes DEFAULT('INDEPENDENT WORKSHOP');
            """, cancellationToken);
    }

    private async Task SeedOperationalBranchesAsync(CancellationToken cancellationToken)
    {
        // Normalize any old branch types from previous hardcoding
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE Branches SET BranchType = 'MW' WHERE BranchType = 'Main Warehouse (MW)';
            """, cancellationToken);

        var operationalBranches = new List<Branch>
        {
            new() { Code = "VBZ", Name = "TRANSPORT NAGAR-SPR", Region = "Jaipur Region", BranchType = "MW", Consignee = "RJ06111", Incharge = "SAJU M RAGHVAN", MobileNo = "8239991456", EmailID = "TNG.INDIAAUTOMOTIVES@GMAIL.COM", Address = "E-27 PRAGYA PLAZA TRANSPORT NAGAR", OpeningYear = "Apr-07", Area = "19,000", Longitude = "75.8456939", Latitude = "26.9085176" },
            new() { Code = "RQL", Name = "GOPINATH MARG-SPR", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "JAI RAM GURJAR", MobileNo = "8239999054", EmailID = "INDIAAUTO.RO.JAIPUR@GMAIL.COM", Address = "D7 GOPINATH MARG NEAR MI ROAD", OpeningYear = "Feb-08", Area = "2,254", Longitude = "75.8075575", Latitude = "26.9187307" },
            new() { Code = "UTD", Name = "UIT ROAD-SPR", Region = "Ganganagar Region", BranchType = "AW", Consignee = "RJ06U21", Incharge = "MANOJ KUMAR PUROHIT", MobileNo = "9509898782", EmailID = "INDIAAUTOMOTIVESSGNR@GMAIL.COM", Address = "CHAK 6E CHHOTI NEAR AUDI MOTOR UIT ROAD SRI GANGANAGAR", OpeningYear = "Apr-09", Area = "6,000", Longitude = "73.8769846", Latitude = "29.9095635" },
            new() { Code = "BGI", Name = "SIKAR ROAD-SPR", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "PRASHANT PAREEK", MobileNo = "8239999051", EmailID = "INDIAAUTOMOTIVES.SIKARROADRO@GMAIL.COM", Address = "53 DHER KE BALAJI SIKAR ROAD", OpeningYear = "Jan-10", Area = "1,400", Longitude = "75.7712641", Latitude = "26.9549542" },
            new() { Code = "ALW", Name = "ALWAR-SPR", Region = "Alwar Region", BranchType = "AW", Consignee = "RJ06F91", Incharge = "LAXMI NARAYAN SHARMA", MobileNo = "8239999056", EmailID = "INDIAAUTOMOTIVESALWAR@GMAIL.COM", Address = "PLOT NO.10-11 DAYANAND NAGAR MAIN 200FT BY PASS RO", OpeningYear = "Apr-11", Area = "4,500", Longitude = "76.6413275", Latitude = "27.5596231" },
            new() { Code = "BSE", Name = "NIRMAN NAGAR-SPR", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "MANOJ PAWAN SHARMA", MobileNo = "8239999053", EmailID = "INDIAAUTOMOTIVESDCM.JAIPUR@YAHOO.COM", Address = "AB-18 NIRMAN NAGAR DCM AJMER ROAD", OpeningYear = "Mar-12", Area = "1,100", Longitude = "75.745416", Latitude = "26.892607" },
            new() { Code = "SKR", Name = "SIKAR-SPR", Region = "Sikar Region", BranchType = "AW", Consignee = "RJ06K71", Incharge = "RAMJILAL PRAJAPAT", MobileNo = "8239999087", EmailID = "INDIAAUTOMOTIVES.SIKAR.AWH@GMAIL.COM", Address = "OPP.RAJ VILLAS GARDEN JAIPUR ROAD SIKAR", OpeningYear = "Sep-12", Area = "9,000", Longitude = "75.168884", Latitude = "27.5925069" },
            new() { Code = "GRL", Name = "BEHIND SHIV MANDIR-SPR", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "MUSHTAQ KAGZI", MobileNo = "8239999724", EmailID = "INDIAAUTOMOTIVES.ATC@GMAIL.COM", Address = "OPPOSITE POLICE STATION BEHIND SHIV MANDIR", OpeningYear = "Dec-12", Area = "1,500", Longitude = "75.7967883", Latitude = "26.8183872" },
            new() { Code = "SGN", Name = "SRI GANGANAGAR-SPR", Region = "Ganganagar Region", BranchType = "RO", Consignee = "RJ06U21", Incharge = "RAJENDRA SWAMI", MobileNo = "8239999350", EmailID = "INDIAAUTOMOTIVES.SGN.RO@GMAIL.COM", Address = "4/98 SUKHADIA SHOPPING CENTRE SRI GANGANAGAR", OpeningYear = "Oct-13", Area = "1,800", Longitude = "73.8770226", Latitude = "29.9095" },
            new() { Code = "JNU", Name = "JHUNJHUNU-SPR", Region = "Jhunjhunu Region", BranchType = "RO", Consignee = "RJ06K71", Incharge = "PINTOO SAINI", MobileNo = "9772701222", EmailID = "INDIAAUTOMOTIVE.JHN.RO@GMAIL.COM", Address = "NEAR BY BANK OF BARODA JHUNJHUNU", OpeningYear = "Feb-14", Area = "4,000", Longitude = "75.3885025", Latitude = "28.1096055" },
            new() { Code = "HMR", Name = "HANUMANGARH JUNCTION-SPR", Region = "Hanumangarh Region", BranchType = "RO", Consignee = "RJ06U21", Incharge = "Ravinder Kumar", MobileNo = "8239999651", EmailID = "INDIAAUTOMOTIVES.RO.HMJ@GMAIL.COM", Address = "NEAR ADARSH CINEMA TOWN ROAD HANUMANGARH JUNCTION", OpeningYear = "Oct-14", Area = "2,160", Longitude = "74.3003422", Latitude = "29.6066308" },
            new() { Code = "JPD", Name = "JAIPUR ROAD-SPR", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "PRATHVI SINGH", MobileNo = "8239994093", EmailID = "INDIAAUTOMOTIVES.RO.CHOMU@GMAIL.COM", Address = "BEHIND SHARMA HOSPITAL JAIPUR ROAD CHOMU", OpeningYear = "Dec-14", Area = "1,280", Longitude = "75.7245614", Latitude = "27.1603385" },
            new() { Code = "NBT", Name = "DEEG ROAD-SPR", Region = "Bharatpur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "RAJENDRA PRASAD SHARMA", MobileNo = "8209084778", EmailID = "INDIAAUTOMOTIVES.BTP@GMAIL.COM", Address = "NEAR BHAGWAN TAKEEJ DEEG ROAD", OpeningYear = "Mar-17", Area = "900", Longitude = "77.476727", Latitude = "27.223104" },
            new() { Code = "BWI", Name = "BHIWADI-SPR", Region = "Alwar Region", BranchType = "RO", Consignee = "RJ06F91", Incharge = "MAYANK YADAV", MobileNo = "8239999185", EmailID = "INDIAAUTOMOTIVES.BHIWADI@GMAIL.COM", Address = "ALWAR BY PASS ROAD NEAR HARISH BAKERY BHIWADI", OpeningYear = "Apr-17", Area = "1,950", Longitude = "76.8082704", Latitude = "28.2058485" },
            new() { Code = "PSS", Name = "PATEL NAGAR-SPR", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "RAJESH YADAV", MobileNo = "8239994050", EmailID = "INDIAAUTOMOTIVES.RO.JRU@GMAIL.COM", Address = "PLOT NO. 41 PATEL NAGAR GOVINDPURA KALWAR ROAD", OpeningYear = "Apr-17", Area = "1,280", Longitude = "75.7028415", Latitude = "26.9498085" },
            new() { Code = "PPH", Name = "KOTPUTALI-SPR", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "OMPRAKASH YADAV", MobileNo = "8239991450", EmailID = "INDIAAUTOMOTIVESRO.KOP@GMAIL.COM", Address = "PLOT NO.7 PUTALI CUT NH-8 KOTPUTALI", OpeningYear = "Apr-18", Area = "1,440", Longitude = "76.1864717", Latitude = "27.6914677" },
            new() { Code = "ISN", Name = "ISKON ROAD-SPR", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "SHISHUPAL YADAV", MobileNo = "8239970641", EmailID = "INDIAAUTOMOTIVES.ROMANSROVER@GMAIL.COM", Address = "SHOP NO. 3-4 HANUMAN VIHAR ISKON ROAD MANSAROVAR", OpeningYear = "Jun-19", Area = "1,800", Longitude = "75.7588902", Latitude = "26.8395058" },
            new() { Code = "DUS", Name = "DAUSA-SPR", Region = "Dausa Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "RAMAVTTAR SHARMA", MobileNo = "9672922111", EmailID = "INDIAAUTOMOTIVES.DAUSARO@GMAIL.COM", Address = "SHOP NO.2 NEAR TATA MOTOR SHOWROOM JAIPUR BY PASS", OpeningYear = "Nov-19", Area = "1,075", Longitude = "76.307135", Latitude = "26.8972593" },
            new() { Code = "STO", Name = "STATION ROAD-SPR", Region = "Alwar Region", BranchType = "RO", Consignee = "RJ06F91", Incharge = "SURESH CHAND", MobileNo = "8239991321", EmailID = "INDIAAUTOMOTIVESALWARRO@GMAIL.COM", Address = "DATA ARCADE 13 MAGAL MARG STATION ROAD NEW TEJ MAN", OpeningYear = "Mar-20", Area = "882", Longitude = "76.616939", Latitude = "27.56383" },
            new() { Code = "JGT", Name = "JAGATPUR-SPR", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "SUDIP BAGCHI", MobileNo = "8239990210", EmailID = "INDIAAUTOMOTIVESJAGATPURA@GMAIL.COM", Address = "PLOT NO.01 PURUSHARTH NAGAR A VISTAR NEAR KHATU", OpeningYear = "Jul-20", Area = "1,600", Longitude = "75.7968055", Latitude = "26.818361" },
            new() { Code = "SDH", Name = "SARDARSHAHAR-SPR", Region = "Churu Region", BranchType = "RO", Consignee = "RJ06K71", Incharge = "HIMMAT SINGH", MobileNo = "8796304521", EmailID = "INDIAAUTOMOTIVES.SSH.RO@GMAIL.COM", Address = "OPP.HO PETROL PUMP RATANGARH ROAD SARDARSHAHAR", OpeningYear = "Aug-21", Area = "1,200", Longitude = "28.432697", Latitude = "74.520724" },
            new() { Code = "HDN", Name = "HINDAUN-SPR", Region = "Karauli Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "ANIS AHMAD", MobileNo = "8239992234", EmailID = "INDIAAUTOMOTIVESHINDAUN.RO@GMAIL.COM", Address = "STATION ROAD NEAR BY VISHWANATH PETROL PUMP", OpeningYear = "Aug-21", Area = "1,080", Longitude = "26.44384", Latitude = "77.01469" },
            new() { Code = "TNG", Name = "TRANSPORT NAGAR-AW", Region = "Jaipur Region", BranchType = "AW", Consignee = "RJ06112", Incharge = "RAVIKANT AGARWAL", MobileNo = "8239090344", EmailID = "TNG.INDIAAUTOMOTIVES@GMAIL.COM", Address = "F-5 LOWER GROUND FLOOR TRANSPORT NAGAR", OpeningYear = "Sep-21", Area = "13,000", Longitude = "0", Latitude = "0" },
            new() { Code = "PKT", Name = "BHADARA ROAD-SPR", Region = "Hanumangarh Region", BranchType = "RO", Consignee = "RJ06U21", Incharge = "PAVAN KUMAR", MobileNo = "823901654", EmailID = "INDIAAUTOMOTIVES.PKT@GMAIL.COM", Address = "GROUND FLOOR SHOP NO.01 PLOT NO. 182 RAJ GURU MA", OpeningYear = "Oct-22", Area = "910", Longitude = "29.190841", Latitude = "74.778214" },
            new() { Code = "LQU", Name = "DHOLPUR-SPR", Region = "Dholpur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "VINOD SINGH", MobileNo = "9773369731", EmailID = "INDIAAUTOMOTIVES.DHOLPUR@GMAIL.COM", Address = "PLOT NO.1 NEAR VIVEK HOTEL GT ROAD DHOLPUR", OpeningYear = "Mar-23", Area = "1,350", Longitude = "26.716345", Latitude = "77.895892" },
            new() { Code = "JSK", Name = "JAIPUR ROAD-SIKAR-SPR", Region = "Sikar Region", BranchType = "RO", Consignee = "RJ06K71", Incharge = "MUL CHAND", MobileNo = "8239999087", EmailID = "INDIAAUTOMOTIVES.SIKAR.RO@GMAIL.COM", Address = "NEAR BALAJI DHARAM KANTA JAIPUR ROAD SIKAR", OpeningYear = "Jul-23", Area = "3,094", Longitude = "27.596642", Latitude = "75.183688" },
            new() { Code = "OR7", Name = "CHIRAWA-SPR", Region = "Jhunjhunu Region", BranchType = "RO", Consignee = "RJ06K71", Incharge = "AMIT KUMAR SHARMA", MobileNo = "8209753481", EmailID = "INDIAAUTOMOTIVES.CHIRAWA@GMAIL.COM", Address = "NEAR SURAJGARH BYPASS CHIRAWA", OpeningYear = "Oct-23", Area = "1,950", Longitude = "0", Latitude = "0" },
            new() { Code = "SKF", Name = "BAGRU-SPR", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "KAILASH CHANDRA BAIRWA", MobileNo = "8239022225", EmailID = "INDIAAUTOMOTIVES.BAGRU@GMAIL.COM", Address = "Near Prem Motors, Ajmer Highway, Bagru Jaipur 303007", OpeningYear = "Jun-24", Area = "860", Longitude = "0", Latitude = "0" },
            new() { Code = "HUH", Name = "TONK ROAD-RO", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "VINOD BAIRWA", MobileNo = "8239990130", EmailID = "INDIAAUTOMOTIVES.CHAKSU@GMAIL.COM", Address = "Near Oswal Gas Agency Jaipur Road Chaksu", OpeningYear = "Jun-24", Area = "860", Longitude = "0", Latitude = "0" },
            new() { Code = "F33", Name = "NEEM KA THANA-SPR", Region = "Sikar Region", BranchType = "RO", Consignee = "RJ06K71", Incharge = "SATPAL", MobileNo = "8239011119", EmailID = "INDIAAUTOMOTIVES.NEEMKATHANA@GMAIL.COM", Address = "Kotputli Bypass Road Neem ka thana. 332713", OpeningYear = "Oct-24", Area = "0", Longitude = "0", Latitude = "0" },
            new() { Code = "BER", Name = "BEHROR-SPR", Region = "Alwar Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "ASHOK SAIN", MobileNo = "8239044440", EmailID = "INDIAAUTOMOTIVES.BEHROR@GMAIL.COM", Address = "Near Gopal Dharam Kanta Delhi Highway Behror 301701", OpeningYear = "Jul-25", Area = "0", Longitude = "0", Latitude = "0" },
            new() { Code = "KNO", Name = "KANOTA-SPR", Region = "Jaipur Region", BranchType = "RO", Consignee = "RJ06112", Incharge = "MADAN SINGH", MobileNo = "8239044441", EmailID = "INDIAAUTOMOTIVES.KANOTA@GMAIL.COM", Address = "Near CBI Bank, Jaipur Highway Kanota 303012", OpeningYear = "Jul-25", Area = "0", Longitude = "0", Latitude = "0" },
            new() { Code = "SJG", Name = "SUJANGARH-SPR", Region = "Churu Region", BranchType = "RO", Consignee = "RJ06K71", Incharge = "NARENDRA KUMAR SAINI", MobileNo = "8239044442", EmailID = "INDIAAUTOMOTIVES.SUJANGARH@GMAIL.COM", Address = "In front of Hotel Sadguru, Near Bus Stand Sujangarh 331507", OpeningYear = "Oct-25", Area = "0", Longitude = "0", Latitude = "0" },
            new() { Code = "SGH", Name = "SURATGARH-SPR", Region = "Ganganagar Region", BranchType = "RO", Consignee = "RJ06U21", Incharge = "SATPAL VERMA", MobileNo = "8239990340", EmailID = "INDIAAUTOMOTIVES.SURATGARH@GMAIL.COM", Address = "Suratgarh Bikaner Road opp. Jo petrol Pump, Suratgarh 335804", OpeningYear = "Mar-26", Area = "0", Longitude = "0", Latitude = "0" },
            new() { Code = "CR9", Name = "CHURU-SPR", Region = "Churu Region", BranchType = "RO", Consignee = "RJ06K71", Incharge = "VIPIN SUROLIA", MobileNo = "8239990339", EmailID = "INDIAAUTOMOTIVES.CHURU@GMAIL.COM", Address = "Near ABS Motors, Jaipur Bypass Road, Churu 331001", OpeningYear = "Apr-26", Area = "0", Longitude = "0", Latitude = "0" }
        };

        foreach (var b in operationalBranches)
        {
            if (b.Code == "BWI")
            {
                b.AllowedCategories = "AA,M,AG";
                b.AllowedPartyTypes = "MASS";
            }
            else
            {
                b.AllowedCategories = "AA,M";
                b.AllowedPartyTypes = "INDEPENDENT WORKSHOP";
            }
        }

        foreach (var b in operationalBranches)
        {
            var existing = await db.Branches.FirstOrDefaultAsync(x => x.Code == b.Code, cancellationToken);
            if (existing is null)
            {
                db.Branches.Add(b);
            }
            else
            {
                existing.Name = b.Name;
                existing.Region = b.Region;
                existing.BranchType = b.BranchType;
                existing.Consignee = b.Consignee;
                existing.Incharge = b.Incharge;
                existing.MobileNo = b.MobileNo;
                existing.EmailID = b.EmailID ?? string.Empty;
                existing.Address = b.Address;
                existing.OpeningYear = b.OpeningYear;
                existing.Area = b.Area;
                existing.Longitude = b.Longitude;
                existing.Latitude = b.Latitude;
                if (string.IsNullOrEmpty(existing.AllowedCategories))
                    existing.AllowedCategories = b.AllowedCategories;
                if (string.IsNullOrEmpty(existing.AllowedPartyTypes))
                    existing.AllowedPartyTypes = b.AllowedPartyTypes;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSlabMasterSchemeAsync(CancellationToken cancellationToken)
    {
        var existing = await db.IncentiveSchemes.Include(x => x.Details)
            .FirstOrDefaultAsync(x => x.SchemeMonth == 5 && x.SchemeYear == 2026 && x.Name == "Standard Sales Value Scheme", cancellationToken);

        if (existing is null)
        {
            var scheme = new IncentiveScheme
            {
                Name = "Standard Sales Value Scheme",
                SchemeMonth = 5,
                SchemeYear = 2026,
                Version = 1,
                EffectiveFrom = new DateTime(2000, 1, 1),
                EffectiveTo = new DateTime(2099, 12, 31), // Indefinite open-ended fallback
                Details = new List<IncentiveSchemeDetail>
                {
                    new() { MinAchievementPercent = 0, MaxAchievementPercent = 29999, FixedAmount = 0, Percentage = 0, RuleName = "Slab 1" },
                    new() { MinAchievementPercent = 30000, MaxAchievementPercent = 49999, FixedAmount = 0, Percentage = 3, RuleName = "Slab 2" },
                    new() { MinAchievementPercent = 50000, MaxAchievementPercent = 74999, FixedAmount = 0, Percentage = 4, RuleName = "Slab 3" },
                    new() { MinAchievementPercent = 75000, MaxAchievementPercent = 119999, FixedAmount = 0, Percentage = 5, RuleName = "Slab 4" },
                    new() { MinAchievementPercent = 120000, MaxAchievementPercent = 999999999, FixedAmount = 0, Percentage = 6, RuleName = "Slab 5" }
                }
            };
            db.IncentiveSchemes.Add(scheme);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var changed = false;
            if (existing.EffectiveTo < new DateTime(2099, 12, 31))
            {
                existing.EffectiveTo = new DateTime(2099, 12, 31);
                changed = true;
            }
            if (existing.EffectiveFrom > new DateTime(2000, 1, 1))
            {
                existing.EffectiveFrom = new DateTime(2000, 1, 1);
                changed = true;
            }
            if (changed)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task InspectExcelMetadataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var path = @"C:\Users\ACER\Desktop\Incentive Portal\Dynamic_Incenitve.xlsx";
            if (!System.IO.File.Exists(path))
            {
                Console.WriteLine($"[EXCEL_INSPECT] Excel file not found at: {path}");
                return;
            }

            using var workbook = new ClosedXML.Excel.XLWorkbook(path);
            Console.WriteLine($"[EXCEL_INSPECT] Sheets count: {workbook.Worksheets.Count}");
            var ws = workbook.Worksheets.First();
            Console.WriteLine($"[EXCEL_INSPECT] Sheet: '{ws.Name}', Rows used: {ws.RowsUsed().Count()}");
            
            var firstRow = ws.Row(1);
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i <= ws.ColumnsUsed().Count(); i++)
            {
                headers[firstRow.Cell(i).GetString()] = i;
            }

            var catCol = headers["Part Category Code"];
            var typeCol = headers["Party Type"];
            var locCol = headers["Loc"];

            var distinctCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var distinctTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var distinctLocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rows = ws.RowsUsed().Skip(1);
            foreach (var row in rows)
            {
                distinctCats.Add(row.Cell(catCol).GetString());
                distinctTypes.Add(row.Cell(typeCol).GetString());
                distinctLocs.Add(row.Cell(locCol).GetString());
            }

            Console.WriteLine($"[EXCEL_INSPECT] Distinct Part Category Codes: {string.Join(", ", distinctCats.OrderBy(x => x))}");
            Console.WriteLine($"[EXCEL_INSPECT] Distinct Party Types: {string.Join(", ", distinctTypes.OrderBy(x => x))}");
            Console.WriteLine($"[EXCEL_INSPECT] Distinct Locations: {string.Join(", ", distinctLocs.OrderBy(x => x))}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EXCEL_INSPECT] Error during inspection: {ex.Message}");
        }
    }

    private async Task EnsureLedgerTablesAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('ImportLogs', 'IsHistorical') IS NULL
                ALTER TABLE ImportLogs ADD IsHistorical bit NOT NULL CONSTRAINT DF_ImportLogs_IsHistorical DEFAULT(0);
            IF COL_LENGTH('ImportLogs', 'Year') IS NULL
                ALTER TABLE ImportLogs ADD Year int NOT NULL CONSTRAINT DF_ImportLogs_Year DEFAULT(0);
            IF COL_LENGTH('ImportLogs', 'Month') IS NULL
                ALTER TABLE ImportLogs ADD Month int NOT NULL CONSTRAINT DF_ImportLogs_Month DEFAULT(0);
            IF COL_LENGTH('ImportLogs', 'VersionNumber') IS NULL
                ALTER TABLE ImportLogs ADD VersionNumber int NOT NULL CONSTRAINT DF_ImportLogs_VersionNumber DEFAULT(1);
            IF COL_LENGTH('ImportLogs', 'ChangeReason') IS NULL
                ALTER TABLE ImportLogs ADD ChangeReason nvarchar(500) NULL;
            IF COL_LENGTH('ImportLogs', 'PreviousImportLogId') IS NULL
                ALTER TABLE ImportLogs ADD PreviousImportLogId int NULL CONSTRAINT FK_ImportLogs_Previous FOREIGN KEY REFERENCES ImportLogs(Id);
            IF COL_LENGTH('ImportLogs', 'LockedAt') IS NULL
                ALTER TABLE ImportLogs ADD LockedAt datetime2 NULL;
            IF COL_LENGTH('ImportLogs', 'LockedBy') IS NULL
                ALTER TABLE ImportLogs ADD LockedBy nvarchar(100) NULL;

            IF OBJECT_ID('MonthLocks', 'U') IS NULL
            BEGIN
                CREATE TABLE MonthLocks (
                    Id INT IDENTITY PRIMARY KEY,
                    LockYear INT NOT NULL,
                    LockMonth INT NOT NULL,
                    IsLocked BIT NOT NULL DEFAULT 0,
                    LockedAt DATETIME2 NULL,
                    LockedBy NVARCHAR(100) NULL,
                    UnlockReason NVARCHAR(500) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_MonthLocks_Period ON MonthLocks(LockYear, LockMonth) WHERE IsDeleted = 0;
            END



            IF OBJECT_ID('PartyExecutiveMappings', 'U') IS NULL
            BEGIN
                CREATE TABLE PartyExecutiveMappings (
                    Id INT IDENTITY PRIMARY KEY,
                    PartyCode NVARCHAR(40) NOT NULL,
                    PartyName NVARCHAR(180) NOT NULL,
                    ExecutiveCode NVARCHAR(40) NOT NULL,
                    ExecutiveName NVARCHAR(160) NOT NULL,
                    BranchCode NVARCHAR(40) NOT NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_PartyExecutiveMappings_Party ON PartyExecutiveMappings(PartyCode) WHERE IsDeleted = 0;
                CREATE INDEX IX_PartyExecutiveMappings_Exec ON PartyExecutiveMappings(ExecutiveCode) WHERE IsDeleted = 0;
                CREATE INDEX IX_PartyExecutiveMappings_Branch ON PartyExecutiveMappings(BranchCode) WHERE IsDeleted = 0;
            END

            SET QUOTED_IDENTIFIER ON;
            IF OBJECT_ID('IncentiveLedgers', 'U') IS NULL
            BEGIN
                CREATE TABLE IncentiveLedgers (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    LedgerRef NVARCHAR(40) NOT NULL DEFAULT '',
                    IncentiveYear INT NOT NULL,
                    IncentiveMonth INT NOT NULL,
                    CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    PaymentDate DATETIME2 NULL,
                    
                    EmployeeCode NVARCHAR(40) NULL,
                    EmployeeName NVARCHAR(160) NULL,
                    PartyCode NVARCHAR(40) NOT NULL DEFAULT '',
                    PartyName NVARCHAR(180) NOT NULL DEFAULT '',
                    
                    SaleValue DECIMAL(18,2) NOT NULL DEFAULT 0,
                    OnBillDiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    AchievementPercent DECIMAL(18,2) NOT NULL DEFAULT 0,
                    SlabApplied NVARCHAR(100) NOT NULL DEFAULT '',
                    SlabVersion INT NOT NULL DEFAULT 1,
                    GrossIncentive DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TdsPercent DECIMAL(5,2) NOT NULL DEFAULT 0,
                    TdsAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    NetTransferAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    
                    PaymentStatus NVARCHAR(20) NOT NULL DEFAULT 'Pending',
                    UTRNumber NVARCHAR(60) NULL,
                    PartCategoryCode NVARCHAR(20) NOT NULL DEFAULT '',
                    
                    ImportLogId INT NOT NULL CONSTRAINT FK_IncentiveLedgers_ImportLogs REFERENCES ImportLogs(Id),
                    VersionNumber INT NOT NULL DEFAULT 1,
                    IsLatestVersion BIT NOT NULL DEFAULT 1,
                    PreviousLedgerId INT NULL CONSTRAINT FK_IncentiveLedgers_Previous FOREIGN KEY REFERENCES IncentiveLedgers(Id),
                    
                    Remarks NVARCHAR(500) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );

                 CREATE UNIQUE INDEX UX_IncentiveLedgers_PeriodParty ON IncentiveLedgers(IncentiveYear, IncentiveMonth, PartyCode) WHERE IsDeleted = 0;
                 CREATE INDEX IX_IncentiveLedgers_Period ON IncentiveLedgers(IncentiveYear, IncentiveMonth);
                 CREATE INDEX IX_IncentiveLedgers_PartyPeriod ON IncentiveLedgers(PartyCode, IncentiveYear, IncentiveMonth);
                 CREATE INDEX IX_IncentiveLedgers_PaymentStatus ON IncentiveLedgers(PaymentStatus);
             END

             IF OBJECT_ID('AdjustmentLedger', 'U') IS NULL
             BEGIN
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
             END
            """, cancellationToken);
    }

    private async Task EnsureDatabaseOptimizationsAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Parties_PartyCode' AND object_id = OBJECT_ID('Parties'))
                CREATE INDEX IX_Parties_PartyCode ON Parties(PartyCode, IsDeleted);

            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_BankDetails_Party' AND object_id = OBJECT_ID('BankDetails'))
                CREATE INDEX IX_BankDetails_Party ON BankDetails(PartyId, IsDeleted);

            -- Cash Management & Announcement Performance Indexes
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CashIn_BranchId' AND object_id = OBJECT_ID('CashInTransactions'))
                CREATE INDEX IX_CashIn_BranchId ON CashInTransactions(BranchId);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CashIn_Status' AND object_id = OBJECT_ID('CashInTransactions'))
                CREATE INDEX IX_CashIn_Status ON CashInTransactions(Status);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CashIn_TransactionDate' AND object_id = OBJECT_ID('CashInTransactions'))
                CREATE INDEX IX_CashIn_TransactionDate ON CashInTransactions(TransactionDate);

            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CashOut_BranchId' AND object_id = OBJECT_ID('CashOutTransactions'))
                CREATE INDEX IX_CashOut_BranchId ON CashOutTransactions(BranchId);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CashOut_Status' AND object_id = OBJECT_ID('CashOutTransactions'))
                CREATE INDEX IX_CashOut_Status ON CashOutTransactions(Status);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CashOut_TransactionDate' AND object_id = OBJECT_ID('CashOutTransactions'))
                CREATE INDEX IX_CashOut_TransactionDate ON CashOutTransactions(TransactionDate);

            IF OBJECT_ID('Announcements', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Announcements_ActiveDeleted' AND object_id = OBJECT_ID('Announcements'))
                    CREATE INDEX IX_Announcements_ActiveDeleted ON Announcements(IsActive, IsDeleted, StartDate, EndDate);
            END
            """, cancellationToken);
    }

    private async Task EnsureAnnouncementsTableAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('Announcements', 'U') IS NULL
            BEGIN
                CREATE TABLE Announcements (
                    Id INT IDENTITY PRIMARY KEY,
                    Message NVARCHAR(500) NOT NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    StartDate DATETIME2 NULL,
                    EndDate DATETIME2 NULL,
                    Severity NVARCHAR(20) NOT NULL DEFAULT 'info',
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END
            """, cancellationToken);
    }

    private async Task EnsureCategorySalesAggregatesTableAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('CategorySalesAggregates', 'U') IS NULL
            BEGIN
                CREATE TABLE CategorySalesAggregates (
                    Id INT IDENTITY PRIMARY KEY,
                    Year INT NOT NULL,
                    Month INT NOT NULL,
                    MonthYear NVARCHAR(40) NOT NULL,
                    Quarter NVARCHAR(20) NOT NULL,
                    PartyType NVARCHAR(60) NOT NULL,
                    PartCategoryCode NVARCHAR(20) NOT NULL,
                    Loc NVARCHAR(40) NOT NULL,
                    DealerSubType NVARCHAR(20) NOT NULL,
                    NetSales DECIMAL(18,2) NOT NULL,
                    NetDdl DECIMAL(18,2) NOT NULL,
                    Discount DECIMAL(18,2) NOT NULL,
                    Transactions INT NOT NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_CategorySalesAggregates_Period ON CategorySalesAggregates(Year, Month) WHERE IsDeleted = 0;
            END
            """, cancellationToken);

        // Auto-rebuild aggregates if Raw contains data but aggregates table is empty
        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM CategorySalesAggregates) AND EXISTS (SELECT 1 FROM Raw)
            BEGIN
                SET QUOTED_IDENTIFIER ON;
                SET ANSI_NULLS ON;
                INSERT INTO CategorySalesAggregates (Year, Month, MonthYear, Quarter, PartyType, PartCategoryCode, Loc, DealerSubType, NetSales, NetDdl, Discount, Transactions, CreatedAt, CreatedBy, IsDeleted)
                SELECT 
                    YearNumber AS Year,
                    MonthNumber AS Month,
                    ISNULL(MonthYear, '') AS MonthYear,
                    ISNULL(Quarter, '') AS Quarter,
                    ISNULL(PartyType, 'INDEPENDENT WORKSHOP') AS PartyType,
                    ISNULL(PartCategoryCode, 'AA') AS PartCategoryCode,
                    ISNULL(Loc, 'HO') AS Loc,
                    ISNULL(DealerSubType, 'AW') AS DealerSubType,
                    SUM(NetRetailSelling) AS NetSales,
                    SUM(NetRetailSelling - DiscountAmount) AS NetDdl,
                    SUM(DiscountAmount) AS Discount,
                    COUNT(*) AS Transactions,
                    SYSUTCDATETIME() AS CreatedAt,
                    'SystemRebuild' AS CreatedBy,
                    0 AS IsDeleted
                FROM Raw
                WHERE IsDeleted = 0 AND YearNumber IS NOT NULL AND MonthNumber IS NOT NULL
                GROUP BY YearNumber, MonthNumber, MonthYear, Quarter, PartyType, PartCategoryCode, Loc, DealerSubType;
            END
            """, cancellationToken);
    }

    private async Task EnsureRawTableAsync(CancellationToken cancellationToken)
    {
         await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('Raw', 'U') IS NULL
            BEGIN
                CREATE TABLE Raw (
                    Id INT IDENTITY PRIMARY KEY,
                    DealerSubType NVARCHAR(40) NULL,
                    Consignee NVARCHAR(180) NULL,
                    DealerCode NVARCHAR(40) NULL,
                    Loc NVARCHAR(40) NULL,
                    PartCategoryCode NVARCHAR(20) NULL,
                    FiscalYear NVARCHAR(40) NULL,
                    Quarter NVARCHAR(20) NULL,
                    Month NVARCHAR(20) NULL,
                    MonthYear NVARCHAR(40) NULL,
                    ConsPartyCode NVARCHAR(40) NULL,
                    ConsPartyName NVARCHAR(180) NULL,
                    PartyType NVARCHAR(60) NULL,
                    DocumentNum NVARCHAR(80) NULL,
                    Remarks NVARCHAR(500) NULL,
                    NetRetailSelling DECIMAL(18,2) NOT NULL DEFAULT 0,
                    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    NetRetailDdl DECIMAL(18,2) NOT NULL DEFAULT 0,
                    OriginalCode NVARCHAR(40) NULL,
                    ImportLogId INT NULL CONSTRAINT FK_Raw_ImportLogs REFERENCES ImportLogs(Id),
                    RowNumber INT NULL,
                    MonthNumber INT NULL,
                    YearNumber INT NULL,
                    AchievementPercent DECIMAL(18,2) NOT NULL DEFAULT 0,
                    PartNum NVARCHAR(100) NULL,
                    RootPartNum NVARCHAR(100) NULL,
                    Day INT NULL,
                    NetRetailQty INT NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_Raw_PeriodParty ON Raw(YearNumber, MonthNumber, ConsPartyCode) WHERE IsDeleted = 0;
                CREATE INDEX IX_Raw_ImportLog ON Raw(ImportLogId) WHERE IsDeleted = 0;
            END
            ELSE
            BEGIN
                IF COL_LENGTH('Raw', 'AchievementPercent') IS NULL
                    ALTER TABLE Raw ADD AchievementPercent DECIMAL(18,2) NOT NULL CONSTRAINT DF_Raw_AchievementPercent DEFAULT(0);
                IF COL_LENGTH('Raw', 'PartNum') IS NULL
                    ALTER TABLE Raw ADD PartNum NVARCHAR(100) NULL;
                IF COL_LENGTH('Raw', 'RootPartNum') IS NULL
                    ALTER TABLE Raw ADD RootPartNum NVARCHAR(100) NULL;
                IF COL_LENGTH('Raw', 'Day') IS NULL
                    ALTER TABLE Raw ADD Day INT NULL;
                IF COL_LENGTH('Raw', 'NetRetailQty') IS NULL
                    ALTER TABLE Raw ADD NetRetailQty INT NULL;
            END
            """, cancellationToken);
    }


    private async Task EnsureSsIncentivesTableAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            SET QUOTED_IDENTIFIER ON;
            IF OBJECT_ID('ssincentives', 'U') IS NULL
            BEGIN
                CREATE TABLE ssincentives (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    [Month] INT NOT NULL,
                    [Year] INT NOT NULL,
                    MonthLabel NVARCHAR(50) NOT NULL DEFAULT '',
                    PartyCode NVARCHAR(40) NOT NULL DEFAULT '',
                    PartyName NVARCHAR(180) NOT NULL DEFAULT '',
                    SaleValue DECIMAL(18,2) NOT NULL DEFAULT 0,
                    SlabPercent DECIMAL(9,4) NOT NULL DEFAULT 0,
                    OnBillDiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    AchievementPercent DECIMAL(18,2) NOT NULL DEFAULT 0,
                    GrossIncentive DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TdsAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    NetTransferAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    TransferredAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Outstanding DECIMAL(18,2) NOT NULL DEFAULT 0,
                    ProcessingDate DATETIME2 NULL,
                    PaymentDate DATETIME2 NULL,
                    PaymentStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending',
                    UTRNumber NVARCHAR(100) NULL,
                    BankAccountNumber NVARCHAR(50) NOT NULL DEFAULT '',
                    IFSC NVARCHAR(50) NOT NULL DEFAULT '',
                    BeneficiaryName NVARCHAR(180) NOT NULL DEFAULT '',
                    PartCategoryCode NVARCHAR(20) NOT NULL DEFAULT '',
                    SourceLocation NVARCHAR(40) NOT NULL DEFAULT '',
                    TdsNote NVARCHAR(200) NULL,
                    IncentiveType NVARCHAR(50) NULL,
                    ApplicableSlab NVARCHAR(100) NULL,
                    
                    [Mode] NVARCHAR(50) NOT NULL DEFAULT '',
                    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Draft',
                    IsEdited BIT NOT NULL DEFAULT 0,
                    ApprovedBy NVARCHAR(100) NULL,
                    ApprovedAt DATETIME2 NULL,
                    Remarks NVARCHAR(500) NULL,
                    ImportLogId INT NULL CONSTRAINT FK_ssincentives_ImportLogs_ImportLogId REFERENCES ImportLogs(Id),
                    
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );

                CREATE INDEX IX_ssincentives_Status ON ssincentives([Status]);
            END
            ELSE
            BEGIN
                IF COL_LENGTH('ssincentives', 'Outstanding') IS NULL
                    ALTER TABLE ssincentives ADD Outstanding DECIMAL(18,2) NOT NULL CONSTRAINT DF_ssincentives_Outstanding DEFAULT(0);
                IF COL_LENGTH('ssincentives', 'TransferredAmount') IS NULL
                    ALTER TABLE ssincentives ADD TransferredAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_ssincentives_TransferredAmount DEFAULT(0);
                IF COL_LENGTH('ssincentives', 'PartCategoryCode') IS NULL
                    ALTER TABLE ssincentives ADD PartCategoryCode NVARCHAR(20) NOT NULL CONSTRAINT DF_ssincentives_PartCategoryCode DEFAULT('');
                IF COL_LENGTH('ssincentives', 'SourceLocation') IS NULL
                    ALTER TABLE ssincentives ADD SourceLocation NVARCHAR(40) NOT NULL CONSTRAINT DF_ssincentives_SourceLocation DEFAULT('');
                if (COL_LENGTH('ssincentives', 'ImportLogId') IS NULL)
                    ALTER TABLE ssincentives ADD ImportLogId INT NULL CONSTRAINT FK_ssincentives_ImportLogs_ImportLogId REFERENCES ImportLogs(Id);
                IF COL_LENGTH('ssincentives', 'TdsNote') IS NULL
                    ALTER TABLE ssincentives ADD TdsNote NVARCHAR(200) NULL;
                IF COL_LENGTH('ssincentives', 'IncentiveType') IS NULL
                    ALTER TABLE ssincentives ADD IncentiveType NVARCHAR(50) NULL;
                IF COL_LENGTH('ssincentives', 'ApplicableSlab') IS NULL
                    ALTER TABLE ssincentives ADD ApplicableSlab NVARCHAR(100) NULL;
            END

            IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ssincentives_Year_Month_PartyCode' AND object_id = OBJECT_ID('ssincentives'))
            BEGIN
                DROP INDEX IX_ssincentives_Year_Month_PartyCode ON ssincentives;
            END

            IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_ssincentives_PeriodParty' AND object_id = OBJECT_ID('ssincentives'))
            BEGIN
                DROP INDEX UX_ssincentives_PeriodParty ON ssincentives;
            END
            CREATE UNIQUE INDEX UX_ssincentives_PeriodParty ON ssincentives([Year], [Month], PartyCode, PartCategoryCode) WHERE IsDeleted = 0;

            -- Data migration: sync TransferredAmount for legacy paid/reconciled records
            UPDATE ssincentives
            SET TransferredAmount = NetTransferAmount
            WHERE (PaymentStatus IN ('Paid', 'Success', 'Reconciled') OR UTRNumber IS NOT NULL)
              AND TransferredAmount = 0
              AND IsDeleted = 0;
            """, cancellationToken);
    }


    private async Task SeedDealerTypesAsync(CancellationToken cancellationToken)
    {
        // Ensure that dummy fallback parties exist so their DealerTypes are represented in distinct lists
        var seedFallbacks = new List<Party>
        {
            new() { PartyCode = "WRJ010120962", PartyName = "CO-DEALER PARTNER", DealerType = "CO-DEALER" },
            new() { PartyCode = "WRJ0106114", PartyName = "CO-DISTRIBUTOR PARTNER", DealerType = "CO-DISTRIBUTOR" },
            new() { PartyCode = "WRJ0123154", PartyName = "IWS PARTNER", DealerType = "INDEPENDENT WORKSHOP" },
            new() { PartyCode = "WRJ050318666", PartyName = "MASS PARTNER", DealerType = "MASS" },
            new() { PartyCode = "WRJ060373022", PartyName = "TRADER PARTNER", DealerType = "TRADER/RETAILER" },
            new() { PartyCode = "WRJ0106115", PartyName = "WALK-IN CUSTOMER", DealerType = "WALK-IN CUSTOMER" }
        };

        foreach (var fallback in seedFallbacks)
        {
            var exists = await db.Parties.AnyAsync(p => p.PartyCode == fallback.PartyCode, cancellationToken);
            if (!exists)
            {
                fallback.GST = "N/A";
                fallback.Mobile = "9999999999";
                fallback.Address = "Seeded fallback party";
                fallback.FixedIncentivePercent = 0m;
                fallback.BranchId = 1;
                fallback.Status = "Active";
                fallback.CreatedAt = DateTime.UtcNow;
                fallback.CreatedBy = "system";
                db.Parties.Add(fallback);
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        // Run the hardcoded updates as a fallback
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE Parties SET DealerType = 'CO-DEALER' WHERE PartyCode = 'WRJ010120962' AND (DealerType IN ('Dealer', '') OR DealerType IS NULL);
            UPDATE Parties SET DealerType = 'CO-DISTRIBUTOR' WHERE PartyCode = 'WRJ0106114' AND (DealerType IN ('Dealer', '') OR DealerType IS NULL);
            UPDATE Parties SET DealerType = 'INDEPENDENT WORKSHOP' WHERE PartyCode = 'WRJ0123154' AND (DealerType IN ('Dealer', '') OR DealerType IS NULL);
            UPDATE Parties SET DealerType = 'MASS' WHERE PartyCode = 'WRJ050318666' AND (DealerType IN ('Dealer', '') OR DealerType IS NULL);
            UPDATE Parties SET DealerType = 'TRADER/RETAILER' WHERE PartyCode = 'WRJ060373022' AND (DealerType IN ('Dealer', '') OR DealerType IS NULL);
            UPDATE Parties SET DealerType = 'WALK-IN CUSTOMER' WHERE PartyCode = 'WRJ0106115' AND (DealerType IN ('Dealer', '') OR DealerType IS NULL);
        """, cancellationToken);


        // Check if there are actually any parties in the DB that need their dealer types seeded/updated
        var needsUpdate = await db.Parties
            .AnyAsync(p => p.DealerType == "Dealer" || p.DealerType == "Slab-Based" || string.IsNullOrEmpty(p.DealerType), cancellationToken);

        if (!needsUpdate)
        {
            // All parties have already been successfully updated with correct dealer types. No need to parse large Excel files!
            return;
        }

        // Now, dynamically parse Excel files to update all other parties in bulk!
        var files = new[]
        {
            @"C:\Users\ACER\Desktop\Incentive Portal\Dynamic_Incenitve.xlsx",
            @"C:\Users\ACER\Desktop\Incentive Portal\Last Year 2025-26.xlsx"
        };

        var partyTypeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in files)
        {
            if (!System.IO.File.Exists(path)) continue;

            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook(path);
                foreach (var ws in workbook.Worksheets)
                {
                    var firstRow = ws.Row(1);
                    var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 1; i <= ws.ColumnsUsed().Count(); i++)
                    {
                        var h = firstRow.Cell(i).GetString()?.Trim();
                        if (!string.IsNullOrEmpty(h))
                        {
                            headers[h] = i;
                        }
                    }

                    int partyCodeCol = -1;
                    if (headers.TryGetValue("Cons Party Code", out var col1)) partyCodeCol = col1;
                    else if (headers.TryGetValue("Party Code", out var col2)) partyCodeCol = col2;
                    else if (headers.TryGetValue("Alternate Code", out var col3)) partyCodeCol = col3;

                    int partyTypeCol = -1;
                    if (headers.TryGetValue("Party Type", out var ptCol)) partyTypeCol = ptCol;

                    if (partyCodeCol != -1 && partyTypeCol != -1)
                    {
                        var rows = ws.RowsUsed().Skip(1);
                        foreach (var row in rows)
                        {
                            var code = row.Cell(partyCodeCol).GetString()?.Trim();
                            var type = row.Cell(partyTypeCol).GetString()?.Trim();
                            if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(type))
                            {
                                partyTypeMap[code] = type;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SeedDealerTypes] Error parsing excel '{path}': {ex.Message}");
            }
        }

        if (partyTypeMap.Count > 0)
        {
            var partiesToUpdate = await db.Parties
                .Where(p => p.DealerType == "Dealer" || p.DealerType == "Slab-Based" || string.IsNullOrEmpty(p.DealerType))
                .ToListAsync(cancellationToken);

            var updatedCount = 0;
            foreach (var party in partiesToUpdate)
            {
                if (partyTypeMap.TryGetValue(party.PartyCode, out var correctType))
                {
                    party.DealerType = correctType;
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                Console.WriteLine($"[SeedDealerTypes] Dynamically updated {updatedCount} parties with correct types from Excel.");
            }
        }
    }

    private async Task EnsureCashMasterItemsAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('CashMasterItems', 'U') IS NULL
            BEGIN
                CREATE TABLE CashMasterItems (
                    Id INT IDENTITY PRIMARY KEY,
                    ItemType NVARCHAR(60) NOT NULL,
                    Name NVARCHAR(100) NOT NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_CashMasterItems_Type ON CashMasterItems(ItemType, IsActive) WHERE IsDeleted = 0;
            END
            """, cancellationToken);

        var exists = await db.CashMasterItems.IgnoreQueryFilters().AnyAsync(cancellationToken);
        if (!exists)
        {
            var defaultItems = new List<CashMasterItem>
            {
                // Receipt Types (Cash In)
                new() { ItemType = "ReceiptType", Name = "Customer Payment", IsActive = true },
                new() { ItemType = "ReceiptType", Name = "Dealer Collection", IsActive = true },
                new() { ItemType = "ReceiptType", Name = "Cash Collection", IsActive = true },
                new() { ItemType = "ReceiptType", Name = "Commission", IsActive = true },
                new() { ItemType = "ReceiptType", Name = "Advance", IsActive = true },
                new() { ItemType = "ReceiptType", Name = "Refund", IsActive = true },

                // Expense Categories (Cash Out)
                new() { ItemType = "ExpenseCategory", Name = "Salary", IsActive = true },
                new() { ItemType = "ExpenseCategory", Name = "Rent", IsActive = true },
                new() { ItemType = "ExpenseCategory", Name = "Electricity", IsActive = true },
                new() { ItemType = "ExpenseCategory", Name = "Travel", IsActive = true },
                new() { ItemType = "ExpenseCategory", Name = "Marketing", IsActive = true },
                new() { ItemType = "ExpenseCategory", Name = "Professional Fee", IsActive = true },
                new() { ItemType = "ExpenseCategory", Name = "Maintenance", IsActive = true },
                new() { ItemType = "ExpenseCategory", Name = "Stationery", IsActive = true },
                new() { ItemType = "ExpenseCategory", Name = "Other", IsActive = true }
            };

            db.CashMasterItems.AddRange(defaultItems);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsurePortalSettingsAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('PortalSettings', 'U') IS NULL
            BEGIN
                CREATE TABLE PortalSettings (
                    Id INT IDENTITY PRIMARY KEY,
                    [Key] NVARCHAR(100) NOT NULL,
                    [Value] NVARCHAR(500) NOT NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_PortalSettings_Key ON PortalSettings([Key]) WHERE IsDeleted = 0;
            END
            """, cancellationToken);

        // Seed settings if empty
        var hasSettings = await db.PortalSettings.CountAsync(x => !x.IsDeleted, cancellationToken);
        if (hasSettings == 0)
        {
            var defaults = new Dictionary<string, string>
            {
                { "--brand", "#ff2e93" },
                { "--brand-2", "#ff2e93" },
                { "--canvas", "#f8fafc" },
                { "--panel", "#ffffff" },
                { "--line", "#e2e8f0" },
                { "--ink", "#1e293b" },
                { "--muted", "#64748b" },
                { "--nav", "#0d1636" },
                { "--nav-2", "#080d24" },
                
                // Dark theme variants
                { "--brand-dark", "#ff2e93" },
                { "--brand-2-dark", "#38bdf8" },
                { "--canvas-dark", "#090e24" },
                { "--panel-dark", "#111a3d" },
                { "--line-dark", "#202b5e" },
                { "--ink-dark", "#f8fafc" },
                { "--muted-dark", "#94a3b8" },
                { "--nav-dark", "#080c1d" },
                { "--nav-2-dark", "#03050c" }
            };

            foreach (var kvp in defaults)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO PortalSettings ([Key], [Value], IsDeleted, CreatedAt, CreatedBy) VALUES ({0}, {1}, 0, GETUTCDATE(), 'system')",
                    kvp.Key, kvp.Value);
            }
        }
    }

    // EnsureExternalIncentiveTablesAsync commented out
    /*
    private async Task EnsureExternalIncentiveTablesAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('ExternalIncentiveUploads', 'U') IS NULL
            BEGIN
                CREATE TABLE ExternalIncentiveUploads (
                    Id INT IDENTITY PRIMARY KEY,
                    FileName NVARCHAR(260) NOT NULL,
                    Month INT NOT NULL,
                    Year INT NOT NULL,
                    MonthLabel NVARCHAR(50) NOT NULL DEFAULT '',
                    TotalRows INT NOT NULL DEFAULT 0,
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Completed',
                    Remarks NVARCHAR(500) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_ExternalIncentiveUploads_Period ON ExternalIncentiveUploads(Year, Month) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID('ExternalIncentiveRecords', 'U') IS NULL
            BEGIN
                CREATE TABLE ExternalIncentiveRecords (
                    Id INT IDENTITY PRIMARY KEY,
                    UploadId INT NOT NULL CONSTRAINT FK_ExternalIncentiveRecords_Upload REFERENCES ExternalIncentiveUploads(Id),
                    Month INT NOT NULL,
                    Year INT NOT NULL,
                    MonthLabel NVARCHAR(50) NOT NULL DEFAULT '',
                    ConsPartyCode NVARCHAR(40) NOT NULL DEFAULT '',
                    ConsPartyName NVARCHAR(180) NOT NULL DEFAULT '',
                    Location NVARCHAR(40) NOT NULL DEFAULT '',
                    NetRetailSelling DECIMAL(18,2) NOT NULL DEFAULT 0,
                    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Slab NVARCHAR(20) NOT NULL DEFAULT '',
                    Incentive DECIMAL(18,2) NOT NULL DEFAULT 0,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_ExternalIncentiveRecords_Period ON ExternalIncentiveRecords(Year, Month, ConsPartyCode) WHERE IsDeleted = 0;
                CREATE INDEX IX_ExternalIncentiveRecords_Upload ON ExternalIncentiveRecords(UploadId) WHERE IsDeleted = 0;
            END
            """, cancellationToken);
    }
    */

    private async Task EnsureControlTowerTablesAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('TdsRules', 'U') IS NULL
            BEGIN
                CREATE TABLE TdsRules (
                    Id INT IDENTITY PRIMARY KEY,
                    EffectiveFrom DATETIME2 NOT NULL,
                    EffectiveTo DATETIME2 NOT NULL,
                    AnnualThreshold DECIMAL(18,2) NOT NULL,
                    RateWithPan DECIMAL(6,4) NOT NULL,
                    RateNoPan DECIMAL(6,4) NOT NULL,
                    Section NVARCHAR(20) NOT NULL DEFAULT '194H',
                    Notes NVARCHAR(500) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_TdsRules_Effective ON TdsRules(EffectiveFrom, EffectiveTo);
            END

            IF OBJECT_ID('ColumnMappingRules', 'U') IS NULL
            BEGIN
                CREATE TABLE ColumnMappingRules (
                    Id INT IDENTITY PRIMARY KEY,
                    ExcelHeader NVARCHAR(120) NOT NULL,
                    PortalField NVARCHAR(120) NOT NULL,
                    UploadContext NVARCHAR(60) NOT NULL DEFAULT 'MonthlySales',
                    IsActive BIT NOT NULL DEFAULT 1,
                    Notes NVARCHAR(300) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_ColumnMappingRules_HeaderContext ON ColumnMappingRules(ExcelHeader, UploadContext) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID('OutstandingRules', 'U') IS NULL
            BEGIN
                CREATE TABLE OutstandingRules (
                    Id INT IDENTITY PRIMARY KEY,
                    Name NVARCHAR(80) NOT NULL,
                    DeductionRate DECIMAL(5,4) NOT NULL,
                    ThresholdAmount DECIMAL(18,2) NOT NULL,
                    Description NVARCHAR(200) NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    Priority INT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            IF OBJECT_ID('NotificationSettings', 'U') IS NULL
            BEGIN
                CREATE TABLE NotificationSettings (
                    Id INT IDENTITY PRIMARY KEY,
                    Provider NVARCHAR(40) NOT NULL DEFAULT 'SMTP',
                    SmtpHost NVARCHAR(200) NOT NULL DEFAULT '',
                    SmtpPort INT NOT NULL DEFAULT 587,
                    SmtpUseSsl BIT NOT NULL DEFAULT 1,
                    SmtpUser NVARCHAR(160) NOT NULL DEFAULT '',
                    SmtpPassEncrypted NVARCHAR(256) NOT NULL DEFAULT '',
                    FromEmail NVARCHAR(160) NOT NULL DEFAULT '',
                    FromName NVARCHAR(120) NOT NULL DEFAULT '',
                    SmsApiKey NVARCHAR(80) NOT NULL DEFAULT '',
                    SmsSenderId NVARCHAR(20) NOT NULL DEFAULT '',
                    EmailEnabled BIT NOT NULL DEFAULT 0,
                    SmsEnabled BIT NOT NULL DEFAULT 0,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            IF OBJECT_ID('RolePermissions', 'U') IS NULL
            BEGIN
                CREATE TABLE RolePermissions (
                    Id INT IDENTITY PRIMARY KEY,
                    RoleName NVARCHAR(60) NOT NULL,
                    Module NVARCHAR(80) NOT NULL,
                    Action NVARCHAR(40) NOT NULL,
                    IsAllowed BIT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_RolePermissions_RoleModuleAction ON RolePermissions(RoleName, Module, Action) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID('TallyIntegrationSettings', 'U') IS NULL
            BEGIN
                CREATE TABLE TallyIntegrationSettings (
                    Id INT IDENTITY PRIMARY KEY,
                    BaseUrl NVARCHAR(200) NOT NULL DEFAULT 'http://localhost:9000',
                    Port INT NOT NULL DEFAULT 9000,
                    CompanyName NVARCHAR(80) NOT NULL DEFAULT '',
                    IsEnabled BIT NOT NULL DEFAULT 0,
                    TimeoutSeconds INT NOT NULL DEFAULT 30,
                    Username NVARCHAR(100) NULL,
                    PasswordEncrypted NVARCHAR(256) NULL,
                    LastSyncAt DATETIME2 NULL,
                    LastSyncStatus NVARCHAR(40) NOT NULL DEFAULT 'Never',
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            IF OBJECT_ID('ReportColumnConfigs', 'U') IS NULL
            BEGIN
                CREATE TABLE ReportColumnConfigs (
                    Id INT IDENTITY PRIMARY KEY,
                    ReportName NVARCHAR(60) NOT NULL,
                    ColumnKey NVARCHAR(80) NOT NULL,
                    DisplayName NVARCHAR(120) NOT NULL,
                    IsVisible BIT NOT NULL DEFAULT 1,
                    SortOrder INT NOT NULL DEFAULT 0,
                    Format NVARCHAR(30) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_ReportColumnConfigs_ReportKey ON ReportColumnConfigs(ReportName, ColumnKey) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID('HelpTexts', 'U') IS NULL
            BEGIN
                CREATE TABLE HelpTexts (
                    Id INT IDENTITY PRIMARY KEY,
                    FieldKey NVARCHAR(100) NOT NULL,
                    Text NVARCHAR(500) NOT NULL,
                    Page NVARCHAR(60) NOT NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            IF OBJECT_ID('BankStatementRecords', 'U') IS NOT NULL AND COL_LENGTH('BankStatementRecords', 'AccountNumber') IS NULL
            BEGIN
                DROP TABLE BankStatementRecords;
            END

            IF OBJECT_ID('BankStatementRecords', 'U') IS NULL
            BEGIN
                CREATE TABLE BankStatementRecords (
                    Id INT IDENTITY PRIMARY KEY,
                    Month INT NOT NULL,
                    Year INT NOT NULL,
                    FileName NVARCHAR(260) NOT NULL,
                    RowNumber INT NOT NULL,
                    BeneficiaryName NVARCHAR(180) NULL,
                    AccountNumber NVARCHAR(50) NULL,
                    IFSC NVARCHAR(50) NULL,
                    Amount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Status NVARCHAR(50) NULL,
                    UTR NVARCHAR(100) NULL,
                    PaymentDate DATETIME2 NULL,
                    PartyCode NVARCHAR(100) NULL,
                    IsReconciled BIT NOT NULL DEFAULT 0,
                    RawRowJson NVARCHAR(MAX) NOT NULL,
                    ImportLogId INT NULL FOREIGN KEY REFERENCES ImportLogs(Id),
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE INDEX IX_BankStatementRecords_Period ON BankStatementRecords(Year, Month) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID('PartyCodeMappings', 'U') IS NULL
            BEGIN
                CREATE TABLE PartyCodeMappings (
                    Id INT IDENTITY PRIMARY KEY,
                    OriginalCode NVARCHAR(40) NOT NULL,
                    AlternativeCode NVARCHAR(40) NOT NULL,
                    Notes NVARCHAR(300) NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_PartyCodeMappings_Alt ON PartyCodeMappings(AlternativeCode) WHERE IsDeleted = 0 AND IsActive = 1;
                CREATE INDEX IX_PartyCodeMappings_Orig ON PartyCodeMappings(OriginalCode) WHERE IsDeleted = 0;
            END
            """, cancellationToken);

        // Seed Default TDS rules
        if (!await db.TdsRules.AnyAsync(cancellationToken))
        {
            db.TdsRules.Add(new TdsRule
            {
                EffectiveFrom = new DateTime(2000, 1, 1),
                EffectiveTo = new DateTime(2099, 12, 31),
                AnnualThreshold = 15000m,
                RateWithPan = 0.05m,
                RateNoPan = 0.20m,
                Section = "194H",
                Notes = "Default standard TDS section 194H rules (5% with PAN, 20% without PAN, 15000 threshold)"
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        // Seed Default Column Mapping Rules for MonthlySales
        if (!await db.ColumnMappingRules.AnyAsync(cancellationToken))
        {
            var defaultMappings = new List<ColumnMappingRule>
            {
                new() { ExcelHeader = "Dealer Sub Type", PortalField = "DealerSubType", UploadContext = "MonthlySales", IsActive = true, Notes = "Dealer Sub Type mappings" },
                new() { ExcelHeader = "Dealer Type", PortalField = "DealerType", UploadContext = "MonthlySales", IsActive = true, Notes = "Dealer Type mappings" },
                new() { ExcelHeader = "Consignee Name", PortalField = "Consignee", UploadContext = "MonthlySales", IsActive = true, Notes = "Consignee mappings" },
                new() { ExcelHeader = "Fiscal Year", PortalField = "FiscalYear", UploadContext = "MonthlySales", IsActive = true, Notes = "Fiscal Year mappings" },
                new() { ExcelHeader = "Quarter", PortalField = "Quarter", UploadContext = "MonthlySales", IsActive = true, Notes = "Quarter mappings" },
                new() { ExcelHeader = "Doc Num", PortalField = "DocumentNum", UploadContext = "MonthlySales", IsActive = true, Notes = "Document Number mappings" },
                new() { ExcelHeader = "Remarks", PortalField = "Remarks", UploadContext = "MonthlySales", IsActive = true, Notes = "Remarks mappings" },
                new() { ExcelHeader = "Net Retail Ddl", PortalField = "NetRetailDdl", UploadContext = "MonthlySales", IsActive = true, Notes = "Net Retail Ddl mappings" }
            };
            db.ColumnMappingRules.AddRange(defaultMappings);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Seed Default Outstanding rules
        if (!await db.OutstandingRules.AnyAsync(cancellationToken))
        {
            db.OutstandingRules.Add(new OutstandingRule
            {
                Name = "Default Outstanding Rule",
                DeductionRate = 0.00m, // 0% deduction default (no deduction)
                ThresholdAmount = 0m,
                Description = "Default standard outstanding rule",
                IsActive = true,
                Priority = 1
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        // Seed Default Notification Setting
        if (!await db.NotificationSettings.AnyAsync(cancellationToken))
        {
            db.NotificationSettings.Add(new NotificationSetting
            {
                Provider = "SMTP",
                SmtpHost = "smtp.mailtrap.io",
                SmtpPort = 2525,
                SmtpUseSsl = false,
                SmtpUser = "testuser",
                SmtpPassEncrypted = "", // Will be configured by admin
                FromEmail = "no-reply@incentiveportal.local",
                FromName = "Incentive Portal Admin",
                EmailEnabled = false,
                SmsEnabled = false
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        // Seed Default Tally Integration Setting
        if (!await db.TallyIntegrationSettings.AnyAsync(cancellationToken))
        {
            db.TallyIntegrationSettings.Add(new TallyIntegrationSetting
            {
                BaseUrl = "http://localhost:9000",
                Port = 9000,
                CompanyName = "My Tally Company",
                IsEnabled = false,
                TimeoutSeconds = 30
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        // Seed Default Role Permissions (Super Admin allowed everything by default)
        if (!await db.RolePermissions.AnyAsync(cancellationToken))
        {
            var defaultPermissions = new List<RolePermission>
            {
                new() { RoleName = AppRoles.SuperAdmin, Module = "ControlTower", Action = "Access", IsAllowed = true },
                new() { RoleName = AppRoles.SuperAdmin, Module = "PortalSettings", Action = "Edit", IsAllowed = true },
                new() { RoleName = AppRoles.SuperAdmin, Module = "TdsRules", Action = "Edit", IsAllowed = true },
                new() { RoleName = AppRoles.SuperAdmin, Module = "ColumnMappings", Action = "Edit", IsAllowed = true },
                new() { RoleName = AppRoles.SuperAdmin, Module = "NotificationSettings", Action = "Edit", IsAllowed = true },
                new() { RoleName = AppRoles.SuperAdmin, Module = "RolePermissions", Action = "Edit", IsAllowed = true },
                new() { RoleName = AppRoles.SuperAdmin, Module = "TallySettings", Action = "Edit", IsAllowed = true },
                new() { RoleName = AppRoles.SuperAdmin, Module = "AuditLogs", Action = "View", IsAllowed = true }
            };
            db.RolePermissions.AddRange(defaultPermissions);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Seed Default Report Column Configs
        if (!await db.ReportColumnConfigs.AnyAsync(cancellationToken))
        {
            // E.g. for Incentive Register
            var registerCols = new List<ReportColumnConfig>
            {
                new() { ReportName = "IncentiveRegister", ColumnKey = "PartyCode", DisplayName = "Party Code", IsVisible = true, SortOrder = 1 },
                new() { ReportName = "IncentiveRegister", ColumnKey = "OriginalPartyCode", DisplayName = "Original Party Code", IsVisible = true, SortOrder = 2 },
                new() { ReportName = "IncentiveRegister", ColumnKey = "PartyName", DisplayName = "Party Name", IsVisible = true, SortOrder = 3 },
                new() { ReportName = "IncentiveRegister", ColumnKey = "BranchCode", DisplayName = "Branch Code", IsVisible = true, SortOrder = 4 },
                new() { ReportName = "IncentiveRegister", ColumnKey = "SaleValue", DisplayName = "Sale Value", IsVisible = true, SortOrder = 5, Format = "C" },
                new() { ReportName = "IncentiveRegister", ColumnKey = "SlabPercent", DisplayName = "Slab %", IsVisible = true, SortOrder = 6, Format = "P" },
                new() { ReportName = "IncentiveRegister", ColumnKey = "GrossIncentive", DisplayName = "Gross Incentive", IsVisible = true, SortOrder = 7, Format = "C" },
                new() { ReportName = "IncentiveRegister", ColumnKey = "TdsAmount", DisplayName = "TDS Amount", IsVisible = true, SortOrder = 8, Format = "C" },
                new() { ReportName = "IncentiveRegister", ColumnKey = "AdjustedAmount", DisplayName = "Adjusted Amount", IsVisible = true, SortOrder = 9, Format = "C" },
                new() { ReportName = "IncentiveRegister", ColumnKey = "TransferAmount", DisplayName = "Transfer Amount", IsVisible = true, SortOrder = 10, Format = "C" }
            };
            db.ReportColumnConfigs.AddRange(registerCols);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureEngineTablesAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            -- ── 1. RULE ENGINE TABLES ──────────────────────────────────────────
            IF OBJECT_ID('RuleMasters', 'U') IS NULL
            BEGIN
                CREATE TABLE RuleMasters (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Code NVARCHAR(50) NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    RuleType NVARCHAR(30) NOT NULL DEFAULT 'Scheme',
                    IsActive BIT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_RuleMasters_Code ON RuleMasters(Code) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID('RuleVersions', 'U') IS NULL
            BEGIN
                CREATE TABLE RuleVersions (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    RuleMasterId INT NOT NULL FOREIGN KEY REFERENCES RuleMasters(Id),
                    VersionNo INT NOT NULL,
                    EffectiveFrom DATETIME2 NOT NULL,
                    EffectiveTo DATETIME2 NOT NULL,
                    FormulaExpression NVARCHAR(MAX) NOT NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_RuleVersions_MasterVersion ON RuleVersions(RuleMasterId, VersionNo) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID('RuleConditions', 'U') IS NULL
            BEGIN
                CREATE TABLE RuleConditions (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    RuleVersionId INT NOT NULL FOREIGN KEY REFERENCES RuleVersions(Id),
                    FieldName NVARCHAR(100) NOT NULL,
                    Operator NVARCHAR(20) NOT NULL,
                    ValueExpression NVARCHAR(200) NOT NULL,
                    LogicalOperator NVARCHAR(10) NOT NULL DEFAULT 'AND',
                    SortOrder INT NOT NULL DEFAULT 0
                );
            END

            -- ── 2. DYNAMIC UPLOAD ENGINE TABLES ──────────────────────────────────
            IF OBJECT_ID('ImportTemplates', 'U') IS NULL
            BEGIN
                CREATE TABLE ImportTemplates (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Code NVARCHAR(50) NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    TargetTable NVARCHAR(100) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_ImportTemplates_Code ON ImportTemplates(Code) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID('ImportColumns', 'U') IS NULL
            BEGIN
                CREATE TABLE ImportColumns (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ImportTemplateId INT NOT NULL FOREIGN KEY REFERENCES ImportTemplates(Id),
                    ColumnName NVARCHAR(100) NOT NULL,
                    DataType NVARCHAR(50) NOT NULL,
                    IsRequired BIT NOT NULL DEFAULT 0,
                    MaxLength INT NULL
                );
            END

            IF OBJECT_ID('ImportMappings', 'U') IS NULL
            BEGIN
                CREATE TABLE ImportMappings (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ImportTemplateId INT NOT NULL FOREIGN KEY REFERENCES ImportTemplates(Id),
                    SourceHeader NVARCHAR(150) NOT NULL,
                    DestinationColumn NVARCHAR(100) NOT NULL
                );
            END

            IF OBJECT_ID('ImportValidationRules', 'U') IS NULL
            BEGIN
                CREATE TABLE ImportValidationRules (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ImportTemplateId INT NOT NULL FOREIGN KEY REFERENCES ImportTemplates(Id),
                    ColumnName NVARCHAR(100) NOT NULL,
                    ValidationType NVARCHAR(50) NOT NULL,
                    ValidationConfig NVARCHAR(MAX) NOT NULL
                );
            END

            -- ── 3. WORKFLOW ENGINE TABLES ───────────────────────────────────────
            IF OBJECT_ID('WorkflowDefinitions', 'U') IS NULL
            BEGIN
                CREATE TABLE WorkflowDefinitions (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Code NVARCHAR(50) NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_WorkflowDefinitions_Code ON WorkflowDefinitions(Code) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID('WorkflowSteps', 'U') IS NULL
            BEGIN
                CREATE TABLE WorkflowSteps (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    WorkflowDefinitionId INT NOT NULL FOREIGN KEY REFERENCES WorkflowDefinitions(Id),
                    StepNumber INT NOT NULL,
                    StepName NVARCHAR(100) NOT NULL,
                    RoleAllowed NVARCHAR(100) NOT NULL,
                    RequiredApprovalsCount INT NOT NULL DEFAULT 1,
                    SlaHours INT NOT NULL DEFAULT 48
                );
            END
            ELSE
            BEGIN
                IF COL_LENGTH('WorkflowSteps', 'SlaHours') IS NULL
                    ALTER TABLE WorkflowSteps ADD SlaHours INT NOT NULL CONSTRAINT DF_WorkflowSteps_SlaHours DEFAULT(48);
            END

            IF OBJECT_ID('WorkflowAssignments', 'U') IS NULL
            BEGIN
                CREATE TABLE WorkflowAssignments (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    WorkflowDefinitionId INT NOT NULL FOREIGN KEY REFERENCES WorkflowDefinitions(Id),
                    TargetEntityId NVARCHAR(100) NOT NULL,
                    TargetEntityType NVARCHAR(100) NOT NULL,
                    CurrentStepNumber INT NOT NULL DEFAULT 1,
                    Status NVARCHAR(30) NOT NULL DEFAULT 'Pending',
                    IsActive BIT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL,
                    DueDate DATETIME2 NULL,
                    EscalatedTo NVARCHAR(100) NULL,
                    EscalationDate DATETIME2 NULL
                );
                CREATE INDEX IX_WorkflowAssignments_Target ON WorkflowAssignments(TargetEntityId, TargetEntityType) WHERE IsDeleted = 0;
            END
            ELSE
            BEGIN
                IF COL_LENGTH('WorkflowAssignments', 'DueDate') IS NULL
                    ALTER TABLE WorkflowAssignments ADD DueDate DATETIME2 NULL;
                IF COL_LENGTH('WorkflowAssignments', 'EscalatedTo') IS NULL
                    ALTER TABLE WorkflowAssignments ADD EscalatedTo NVARCHAR(100) NULL;
                IF COL_LENGTH('WorkflowAssignments', 'EscalationDate') IS NULL
                    ALTER TABLE WorkflowAssignments ADD EscalationDate DATETIME2 NULL;
            END

            IF OBJECT_ID('WorkflowHistories', 'U') IS NULL
            BEGIN
                CREATE TABLE WorkflowHistories (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    WorkflowAssignmentId INT NOT NULL FOREIGN KEY REFERENCES WorkflowAssignments(Id),
                    StepNumber INT NOT NULL,
                    Action NVARCHAR(30) NOT NULL,
                    PerformedBy NVARCHAR(100) NOT NULL,
                    PerformedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    Remarks NVARCHAR(500) NULL
                );
            END
            """, cancellationToken);
    }

    private async Task EnsureCostCenterCashesTableAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('CostCenterCashes', 'U') IS NULL
            BEGIN
                CREATE TABLE CostCenterCashes (
                    Id INT IDENTITY PRIMARY KEY,
                    [Year] INT NOT NULL,
                    [Month] INT NOT NULL,
                    CostCenterName NVARCHAR(100) NOT NULL,
                    OpeningBalance DECIMAL(18,2) NOT NULL,
                    Debit DECIMAL(18,2) NOT NULL,
                    Credit DECIMAL(18,2) NOT NULL,
                    ClosingBalance DECIMAL(18,2) NOT NULL,
                    SyncedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_CostCenterCashes_PeriodCC ON CostCenterCashes(Year, Month, CostCenterName) WHERE IsDeleted = 0;
            END

            -- ── Asset Register ──────────────────────────────────────────────────
            IF OBJECT_ID('AssetItems', 'U') IS NULL
            BEGIN
                CREATE TABLE AssetItems (
                    Id                      INT IDENTITY(1,1) PRIMARY KEY,
                    BranchId                INT NOT NULL FOREIGN KEY REFERENCES Branches(Id),
                    AssetCode               NVARCHAR(40)  NOT NULL DEFAULT '',
                    Category                NVARCHAR(30)  NOT NULL DEFAULT '',
                    Name                    NVARCHAR(200) NOT NULL DEFAULT '',
                    Description             NVARCHAR(500) NOT NULL DEFAULT '',
                    Manufacturer            NVARCHAR(60)  NOT NULL DEFAULT '',
                    ModelNumber             NVARCHAR(60)  NOT NULL DEFAULT '',
                    SerialNumber            NVARCHAR(60)  NOT NULL DEFAULT '',
                    PurchaseDate            DATETIME2 NULL,
                    PurchaseCost            DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Vendor                  NVARCHAR(120) NOT NULL DEFAULT '',
                    InvoiceNumber           NVARCHAR(40)  NOT NULL DEFAULT '',
                    DepreciationRatePercent DECIMAL(9,4)  NOT NULL DEFAULT 0,
                    CurrentValue            DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Condition               NVARCHAR(30)  NOT NULL DEFAULT 'Good',
                    Status                  NVARCHAR(30)  NOT NULL DEFAULT 'Active',
                    AssetLocation           NVARCHAR(200) NOT NULL DEFAULT '',
                    AssignedTo              NVARCHAR(100) NOT NULL DEFAULT '',
                    WarrantyExpiryDate      DATETIME2 NULL,
                    Remarks                 NVARCHAR(1000) NOT NULL DEFAULT '',
                    IsDeleted               BIT           NOT NULL DEFAULT 0,
                    CreatedAt               DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy               NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt               DATETIME2 NULL,
                    UpdatedBy               NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_AssetItems_BranchCode ON AssetItems(BranchId, AssetCode) WHERE IsDeleted = 0;
            END
            """, cancellationToken);
    }

    private async Task EnsureSaasTablesAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            -- 1. HelpdeskTickets
            IF OBJECT_ID('HelpdeskTickets', 'U') IS NULL
            BEGIN
                CREATE TABLE HelpdeskTickets (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Title NVARCHAR(150) NOT NULL,
                    Description NVARCHAR(MAX) NOT NULL,
                    Priority NVARCHAR(20) NOT NULL DEFAULT 'Medium',
                    Status NVARCHAR(20) NOT NULL DEFAULT 'New',
                    AssignedTo NVARCHAR(100) NOT NULL DEFAULT '',
                    Category NVARCHAR(60) NOT NULL DEFAULT 'General',
                    AttachmentPath NVARCHAR(300) NULL,
                    SlaExpiry DATETIME2 NULL,
                    Remarks NVARCHAR(500) NULL,
                    AssociatedPartyCode NVARCHAR(40) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            -- 2. TicketComments
            IF OBJECT_ID('TicketComments', 'U') IS NULL
            BEGIN
                CREATE TABLE TicketComments (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TicketId INT NOT NULL FOREIGN KEY REFERENCES HelpdeskTickets(Id),
                    CommentText NVARCHAR(MAX) NOT NULL,
                    IsInternal BIT NOT NULL DEFAULT 0,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            -- 3. DocumentItems
            IF OBJECT_ID('DocumentItems', 'U') IS NULL
            BEGIN
                CREATE TABLE DocumentItems (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    FileName NVARCHAR(250) NOT NULL,
                    FilePath NVARCHAR(300) NOT NULL,
                    Category NVARCHAR(60) NOT NULL DEFAULT 'General',
                    Version INT NOT NULL DEFAULT 1,
                    ExpiryDate DATETIME2 NULL,
                    Owner NVARCHAR(100) NOT NULL DEFAULT 'system',
                    SizeBytes BIGINT NOT NULL DEFAULT 0,
                    AssociatedPartyCode NVARCHAR(40) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            -- 4. CustomerTasks
            IF OBJECT_ID('CustomerTasks', 'U') IS NULL
            BEGIN
                CREATE TABLE CustomerTasks (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    PartyCode NVARCHAR(40) NOT NULL,
                    Title NVARCHAR(150) NOT NULL,
                    Description NVARCHAR(MAX) NOT NULL DEFAULT '',
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
                    Priority NVARCHAR(20) NOT NULL DEFAULT 'Medium',
                    DueDate DATETIME2 NULL,
                    AssignedTo NVARCHAR(100) NOT NULL DEFAULT '',
                    TaskType NVARCHAR(20) NOT NULL DEFAULT 'Call',
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            -- 5. SystemNotifications
            IF OBJECT_ID('SystemNotifications', 'U') IS NULL
            BEGIN
                CREATE TABLE SystemNotifications (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TargetUser NVARCHAR(100) NOT NULL,
                    Title NVARCHAR(150) NOT NULL,
                    Message NVARCHAR(MAX) NOT NULL,
                    IsRead BIT NOT NULL DEFAULT 0,
                    NotificationType NVARCHAR(30) NOT NULL DEFAULT 'System',
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            -- 6. AutomationRules
            IF OBJECT_ID('AutomationRules', 'U') IS NULL
            BEGIN
                CREATE TABLE AutomationRules (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    RuleName NVARCHAR(100) NOT NULL,
                    TriggerType NVARCHAR(50) NOT NULL,
                    ActionType NVARCHAR(50) NOT NULL,
                    ConditionsJson NVARCHAR(MAX) NOT NULL DEFAULT '{{}}',
                    IsActive BIT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            -- 7. KnowledgeBaseArticles
            IF OBJECT_ID('KnowledgeBaseArticles', 'U') IS NULL
            BEGIN
                CREATE TABLE KnowledgeBaseArticles (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Title NVARCHAR(200) NOT NULL,
                    Content NVARCHAR(MAX) NOT NULL,
                    Category NVARCHAR(60) NOT NULL DEFAULT 'General',
                    ViewsCount INT NOT NULL DEFAULT 0,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            -- 8. CashVerifications
            IF OBJECT_ID('CashVerifications', 'U') IS NULL
            BEGIN
                CREATE TABLE CashVerifications (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    BranchId INT NOT NULL FOREIGN KEY REFERENCES Branches(Id),
                    VerificationDate DATETIME2 NOT NULL,
                    OpeningCash DECIMAL(18,2) NOT NULL,
                    ExpectedClosingCash DECIMAL(18,2) NOT NULL,
                    PhysicalClosingCash DECIMAL(18,2) NOT NULL,
                    Difference DECIMAL(18,2) NOT NULL,
                    Remarks NVARCHAR(500) NOT NULL DEFAULT '',
                    Status NVARCHAR(20) NOT NULL DEFAULT 'Verified',
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END
            """, cancellationToken);
    }

    private async Task EnsureDynamicReportBuilderTablesAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('CustomReportLayouts', 'U') IS NULL
            BEGIN
                CREATE TABLE CustomReportLayouts (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(120) NOT NULL,
                    ReportType NVARCHAR(20) NOT NULL DEFAULT 'Tabular',
                    SelectedFieldsJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                    PivotRowsJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                    PivotColumnsJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                    PivotValuesJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                    FiltersJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                    SortsJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                    GroupsJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            IF OBJECT_ID('ReportSchedules', 'U') IS NULL
            BEGIN
                CREATE TABLE ReportSchedules (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    LayoutId INT NOT NULL FOREIGN KEY REFERENCES CustomReportLayouts(Id),
                    RecipientEmails NVARCHAR(300) NOT NULL,
                    Frequency NVARCHAR(20) NOT NULL DEFAULT 'Daily',
                    CronExpression NVARCHAR(50) NOT NULL DEFAULT '0 8 * * *',
                    IsActive BIT NOT NULL DEFAULT 1,
                    LastRunJobId NVARCHAR(100) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END
            """, cancellationToken);
    }

    private async Task EnsureItPortalTablesAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('ItMasterDatas', 'U') IS NULL
            BEGIN
                CREATE TABLE ItMasterDatas (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Type NVARCHAR(100) NOT NULL,
                    Code NVARCHAR(100) NOT NULL,
                    Name NVARCHAR(250) NOT NULL,
                    ParentId INT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    IsActive BIT NOT NULL DEFAULT 1,
                    SortOrder INT NOT NULL DEFAULT 0,
                    Description NVARCHAR(500) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                
                -- Seed Master Data
                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('Location', 'LOC-HO', 'Head Office', 1),
                ('Location', 'LOC-DEL', 'Warehouse Delhi', 2),
                ('Location', 'LOC-MUM', 'Branch Mumbai', 3),
                ('Location', 'LOC-BLR', 'Branch Bangalore', 4);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('Department', 'DEPT-IT', 'IT Support', 1),
                ('Department', 'DEPT-FIN', 'Finance & Accounts', 2),
                ('Department', 'DEPT-SLS', 'Sales & Marketing', 3),
                ('Department', 'DEPT-HR', 'Human Resources', 4),
                ('Department', 'DEPT-LOG', 'Logistics & Ops', 5);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('Employee', 'EMP-001', 'Shailendra Kumar (IT Lead)', 1),
                ('Employee', 'EMP-002', 'Amit Sharma (Sales Exec)', 2),
                ('Employee', 'EMP-003', 'Priya Patel (Finance Manager)', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('Vendor', 'VND-DELL', 'Dell Technologies India', 1),
                ('Vendor', 'VND-HP', 'HP India Sales Pvt Ltd', 2),
                ('Vendor', 'VND-MSFT', 'Microsoft Cloud Services', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('AssetCategory', 'CAT-HW', 'IT Hardware', 1),
                ('AssetCategory', 'CAT-SW', 'Software & Licensing', 2),
                ('AssetCategory', 'CAT-NET', 'Networking Equipment', 3),
                ('AssetCategory', 'CAT-FUR', 'Office Furniture', 4);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('AssetType', 'TYP-LPT', 'Laptop', 1),
                ('AssetType', 'TYP-DSK', 'Desktop PC', 2),
                ('AssetType', 'TYP-RTR', 'Network Router', 3),
                ('AssetType', 'TYP-SWT', 'Network Switch', 4),
                ('AssetType', 'TYP-CHR', 'Ergonomic Chair', 5);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('AssetBrand', 'BRD-DELL', 'Dell', 1),
                ('AssetBrand', 'BRD-HP', 'HP', 2),
                ('AssetBrand', 'BRD-CSCO', 'Cisco', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('AssetModel', 'MDL-LAT34', 'Latitude 3420', 1),
                ('AssetModel', 'MDL-ELT84', 'EliteBook 840 G8', 2),
                ('AssetModel', 'MDL-ISR43', 'ISR 4331 Router', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('OperatingSystem', 'OS-WIN11', 'Windows 11 Pro', 1),
                ('OperatingSystem', 'OS-MACOS', 'macOS Sequoia', 2),
                ('OperatingSystem', 'OS-LINUX', 'Ubuntu 22.04 LTS', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('ProcessorType', 'CPU-I5', 'Intel Core i5', 1),
                ('ProcessorType', 'CPU-I7', 'Intel Core i7', 2),
                ('ProcessorType', 'CPU-R5', 'AMD Ryzen 5', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('RAMConfiguration', 'RAM-8G', '8 GB DDR4', 1),
                ('RAMConfiguration', 'RAM-16G', '16 GB DDR4', 2),
                ('RAMConfiguration', 'RAM-32G', '32 GB DDR4', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('StorageType', 'ST-256S', '256 GB NVMe SSD', 1),
                ('StorageType', 'ST-512S', '512 GB NVMe SSD', 2),
                ('StorageType', 'ST-1TS', '1 TB NVMe SSD', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('SoftwareCategory', 'SWCAT-OS', 'Operating System', 1),
                ('SoftwareCategory', 'SWCAT-PRD', 'Productivity Suite', 2),
                ('SoftwareCategory', 'SWCAT-SEC', 'Endpoint Security', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('SoftwareVendor', 'SWVND-MSFT', 'Microsoft', 1),
                ('SoftwareVendor', 'SWVND-ADBE', 'Adobe Inc', 2);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('LicenseType', 'LIC-OEM', 'OEM License', 1),
                ('LicenseType', 'LIC-VOL', 'Volume Licensing', 2),
                ('LicenseType', 'LIC-SaaS', 'Subscription (SaaS)', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('Priority', 'PRI-LOW', 'Low', 1),
                ('Priority', 'PRI-MED', 'Medium', 2),
                ('Priority', 'PRI-HIGH', 'High', 3),
                ('Priority', 'PRI-CRIT', 'Critical', 4);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('Severity', 'SEV-LOW', 'Low', 1),
                ('Severity', 'SEV-MED', 'Medium', 2),
                ('Severity', 'SEV-HIGH', 'High', 3),
                ('Severity', 'SEV-CRIT', 'Critical', 4);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('Impact', 'IMP-LOW', 'Low (Single User)', 1),
                ('Impact', 'IMP-MED', 'Medium (Department)', 2),
                ('Impact', 'IMP-HIGH', 'High (Branch/Enterprise)', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('TicketCategory', 'TC-HW', 'Hardware Issue', 1),
                ('TicketCategory', 'TC-SW', 'Software Issue', 2),
                ('TicketCategory', 'TC-NET', 'Network Connection', 3),
                ('TicketCategory', 'TC-ACC', 'Access & Security', 4);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('TicketSubCategory', 'TSC-PASS', 'Password Reset', 1),
                ('TicketSubCategory', 'TSC-VPN', 'VPN Disconnection', 2),
                ('TicketSubCategory', 'TSC-WIFI', 'Wi-Fi Slow', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('RootCause', 'RC-HWFAIL', 'Hardware Component Failure', 1),
                ('RootCause', 'RC-BUG', 'Software Glitch/Bug', 2),
                ('RootCause', 'RC-USER', 'User Education Required', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('ResolutionType', 'RT-REST', 'Rebooted / Restarted Device', 1),
                ('ResolutionType', 'RT-PART', 'Replaced Hardware Part', 2),
                ('ResolutionType', 'RT-INST', 'Reinstalled Software App', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('IssueType', 'IT-INC', 'Incident Ticket', 1),
                ('IssueType', 'IT-REQ', 'Service Request', 2);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('Status', 'ST-NEW', 'New', 1),
                ('Status', 'ST-ASG', 'Assigned', 2),
                ('Status', 'ST-PRG', 'In Progress', 3),
                ('Status', 'ST-WFU', 'Waiting for User', 4),
                ('Status', 'ST-WFV', 'Waiting for Vendor', 5),
                ('Status', 'ST-RES', 'Resolved', 6),
                ('Status', 'ST-CLD', 'Closed', 7);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('SlaPolicy', 'SLA-STD', 'Standard Enterprise SLA', 1);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('AMCProvider', 'AMC-AIRT', 'Airtel Enterprise Care', 1),
                ('AMCProvider', 'AMC-TATA', 'Tata Communications Support', 2);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('WarrantyProvider', 'WR-DELL', 'Dell ProSupport Plus', 1),
                ('WarrantyProvider', 'WR-HP', 'HP Care Pack Support', 2);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('DisposalReason', 'DR-EOL', 'End of Useful Life', 1),
                ('DisposalReason', 'DR-DAM', 'Accidental Physical Damage', 2),
                ('DisposalReason', 'DR-STOL', 'Stolen / Lost', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('PurchaseType', 'PT-CAP', 'Direct Capital Purchase', 1),
                ('PurchaseType', 'PT-LSE', 'Operational Leasing', 2);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('CostCenter', 'CC-IT', 'CC - Corporate IT', 1),
                ('CostCenter', 'CC-HO', 'CC - Head Office Ops', 2);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('ApprovalLevels', 'AL-L1', 'Level 1 (IT Lead)', 1),
                ('ApprovalLevels', 'AL-L2', 'Level 2 (Branch Manager)', 2),
                ('ApprovalLevels', 'AL-L3', 'Level 3 (Corporate CFO)', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('UserRoles', 'RL-ADMIN', 'IT Lead Administrator', 1),
                ('UserRoles', 'RL-ENG', 'Support Engineer', 2),
                ('UserRoles', 'RL-COORD', 'Branch IT Coordinator', 3);

                INSERT INTO ItMasterDatas (Type, Code, Name, SortOrder) VALUES 
                ('NotificationTemplates', 'NT-NEWTKT', 'New Ticket Raised Template', 1),
                ('NotificationTemplates', 'NT-ASGTKT', 'Ticket Assignment Template', 2);
            END

            IF OBJECT_ID('ItAssets', 'U') IS NULL
            BEGIN
                CREATE TABLE ItAssets (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    AssetCode NVARCHAR(40) NOT NULL,
                    Name NVARCHAR(200) NOT NULL,
                    CategoryId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    TypeId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    BrandId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    ModelId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    SerialNumber NVARCHAR(100) NOT NULL,
                    AssetTag NVARCHAR(100) NOT NULL,
                    PurchaseDate DATETIME2 NOT NULL,
                    PurchaseCost DECIMAL(18,2) NOT NULL DEFAULT 0,
                    VendorId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    InvoiceNumber NVARCHAR(100) NOT NULL,
                    WarrantyStart DATETIME2 NULL,
                    WarrantyEnd DATETIME2 NULL,
                    AmcStart DATETIME2 NULL,
                    AmcEnd DATETIME2 NULL,
                    AmcProviderId INT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    WarrantyProviderId INT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    BranchId INT NOT NULL FOREIGN KEY REFERENCES Branches(Id),
                    LocationId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    DepartmentId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    AssignedEmployeeId INT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    AssetStatusId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    Condition NVARCHAR(50) NOT NULL DEFAULT 'Good',
                    CurrentUserId INT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    PurchaseOrder NVARCHAR(100) NOT NULL DEFAULT '',
                    DepreciationRatePercent DECIMAL(9,4) NOT NULL DEFAULT 0,
                    InsuranceDetails NVARCHAR(500) NOT NULL DEFAULT '',
                    DisposalDate DATETIME2 NULL,
                    DisposalReasonId INT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    CostCenterId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    Remarks NVARCHAR(1000) NOT NULL DEFAULT '',
                    AttachmentPath NVARCHAR(300) NULL,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_ItAssets_Code ON ItAssets(AssetCode) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID('ItAssetHistories', 'U') IS NULL
            BEGIN
                CREATE TABLE ItAssetHistories (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    AssetId INT NOT NULL FOREIGN KEY REFERENCES ItAssets(Id),
                    ActionType NVARCHAR(50) NOT NULL,
                    FromBranchId INT NULL,
                    ToBranchId INT NULL,
                    FromUserId INT NULL,
                    ToUserId INT NULL,
                    TransactionDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    Reason NVARCHAR(500) NOT NULL DEFAULT '',
                    ApprovalStatus NVARCHAR(30) NOT NULL DEFAULT 'Approved',
                    ApprovedBy NVARCHAR(100) NOT NULL DEFAULT '',
                    AttachmentPath NVARCHAR(300) NULL,
                    Details NVARCHAR(1000) NOT NULL DEFAULT '',
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            IF OBJECT_ID('ItSoftwareLicenses', 'U') IS NULL
            BEGIN
                CREATE TABLE ItSoftwareLicenses (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    SoftwareName NVARCHAR(150) NOT NULL,
                    VendorId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    LicenseKey NVARCHAR(200) NOT NULL,
                    Version NVARCHAR(50) NOT NULL DEFAULT '',
                    InstallationDate DATETIME2 NOT NULL,
                    ExpiryDate DATETIME2 NULL,
                    AssetId INT NULL FOREIGN KEY REFERENCES ItAssets(Id),
                    TotalLicenses INT NOT NULL DEFAULT 1,
                    LicenseTypeId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    IsActive BIT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            IF OBJECT_ID('ItTickets', 'U') IS NULL
            BEGIN
                CREATE TABLE ItTickets (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TicketNumber NVARCHAR(40) NOT NULL,
                    Requester NVARCHAR(100) NOT NULL,
                    BranchId INT NOT NULL FOREIGN KEY REFERENCES Branches(Id),
                    DepartmentId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    CategoryId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    SubCategoryId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    PriorityId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    SeverityId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    ImpactId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    Subject NVARCHAR(250) NOT NULL,
                    Description NVARCHAR(MAX) NOT NULL,
                    AttachmentPath NVARCHAR(300) NULL,
                    AssignedEngineer NVARCHAR(100) NOT NULL DEFAULT '',
                    Status NVARCHAR(30) NOT NULL DEFAULT 'New',
                    ResolutionText NVARCHAR(MAX) NOT NULL DEFAULT '',
                    RootCauseId INT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    ResolutionTypeId INT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    ClosureDate DATETIME2 NULL,
                    UserFeedbackScore INT NULL,
                    UserFeedbackText NVARCHAR(500) NULL,
                    SlaDeadline DATETIME2 NOT NULL,
                    SlaBreached BIT NOT NULL DEFAULT 0,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
                CREATE UNIQUE INDEX UX_ItTickets_Number ON ItTickets(TicketNumber) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID('ItTicketComments', 'U') IS NULL
            BEGIN
                CREATE TABLE ItTicketComments (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TicketId INT NOT NULL FOREIGN KEY REFERENCES ItTickets(Id),
                    CommentText NVARCHAR(MAX) NOT NULL,
                    IsInternal BIT NOT NULL DEFAULT 0,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            IF OBJECT_ID('ItSlaPolicies', 'U') IS NULL
            BEGIN
                CREATE TABLE ItSlaPolicies (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(100) NOT NULL,
                    PriorityId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    ResponseTimeHours INT NOT NULL,
                    ResolutionTimeHours INT NOT NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );

                -- Seed default SLA policies linked to ItMasterDatas priority IDs
                DECLARE @LowId INT = (SELECT TOP 1 Id FROM ItMasterDatas WHERE Type = 'Priority' AND Code = 'PRI-LOW');
                DECLARE @MedId INT = (SELECT TOP 1 Id FROM ItMasterDatas WHERE Type = 'Priority' AND Code = 'PRI-MED');
                DECLARE @HighId INT = (SELECT TOP 1 Id FROM ItMasterDatas WHERE Type = 'Priority' AND Code = 'PRI-HIGH');
                DECLARE @CritId INT = (SELECT TOP 1 Id FROM ItMasterDatas WHERE Type = 'Priority' AND Code = 'PRI-CRIT');

                IF @LowId IS NOT NULL AND @MedId IS NOT NULL AND @HighId IS NOT NULL AND @CritId IS NOT NULL
                BEGIN
                    INSERT INTO ItSlaPolicies (Name, PriorityId, ResponseTimeHours, ResolutionTimeHours) VALUES
                    ('Low Priority Policy', @LowId, 24, 72),
                    ('Medium Priority Policy', @MedId, 8, 24),
                    ('High Priority Policy', @HighId, 2, 8),
                    ('Critical Priority Policy', @CritId, 1, 4);
                END
            END

            IF OBJECT_ID('ItMaintenanceSchedules', 'U') IS NULL
            BEGIN
                CREATE TABLE ItMaintenanceSchedules (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    AssetId INT NOT NULL FOREIGN KEY REFERENCES ItAssets(Id),
                    Frequency NVARCHAR(30) NOT NULL DEFAULT 'Quarterly',
                    LastDoneDate DATETIME2 NOT NULL,
                    NextDueDate DATETIME2 NOT NULL,
                    AssignedEngineer NVARCHAR(100) NOT NULL DEFAULT '',
                    ChecklistJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
                    Status NVARCHAR(30) NOT NULL DEFAULT 'Pending',
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END

            IF OBJECT_ID('ItKbArticles', 'U') IS NULL
            BEGIN
                CREATE TABLE ItKbArticles (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Title NVARCHAR(250) NOT NULL,
                    Content NVARCHAR(MAX) NOT NULL,
                    CategoryId INT NOT NULL FOREIGN KEY REFERENCES ItMasterDatas(Id),
                    Tags NVARCHAR(200) NOT NULL DEFAULT '',
                    ViewsCount INT NOT NULL DEFAULT 0,
                    IsDeleted BIT NOT NULL DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CreatedBy NVARCHAR(100) NOT NULL DEFAULT 'system',
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy NVARCHAR(100) NULL
                );
            END
            """, cancellationToken);
    }
}
