using System.Security.Claims;
using System.Text.Json;
using IncentivePortal.Models;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Data;

public sealed class IncentiveDbContext : DbContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public IncentiveDbContext(DbContextOptions<IncentiveDbContext> options, IHttpContextAccessor? httpContextAccessor = null) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<BankDetail> BankDetails => Set<BankDetail>();
    public DbSet<BankApprovalRequest> BankApprovalRequests => Set<BankApprovalRequest>();
    public DbSet<IncentiveScheme> IncentiveSchemes => Set<IncentiveScheme>();
    public DbSet<IncentiveSchemeDetail> IncentiveSchemeDetails => Set<IncentiveSchemeDetail>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ImportLog> ImportLogs => Set<ImportLog>();
    public DbSet<DealerMonthlyPerformance> DealerMonthlyPerformances => Set<DealerMonthlyPerformance>();
    public DbSet<IncentiveSummary> IncentiveSummaries => Set<IncentiveSummary>();
    public DbSet<DealerSlabProgress> DealerSlabProgresses => Set<DealerSlabProgress>();
    public DbSet<DealerGrowthAnalytics> DealerGrowthAnalytics => Set<DealerGrowthAnalytics>();
    public DbSet<MonthLock> MonthLocks => Set<MonthLock>();
    public DbSet<IncentivePeriodLock> IncentivePeriodLocks => Set<IncentivePeriodLock>();
    public DbSet<PartyExecutiveMapping> PartyExecutiveMappings => Set<PartyExecutiveMapping>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<PortalSetting> PortalSettings => Set<PortalSetting>();
    public DbSet<CategorySalesAggregate> CategorySalesAggregates => Set<CategorySalesAggregate>();
    public DbSet<RawRecord> Raws => Set<RawRecord>();
    public DbSet<ExternalIncentiveUpload> ExternalIncentiveUploads => Set<ExternalIncentiveUpload>();
    public DbSet<ExternalIncentiveRecord> ExternalIncentiveRecords => Set<ExternalIncentiveRecord>();
    public DbSet<IncentivePeriod> IncentivePeriods => Set<IncentivePeriod>();
    public DbSet<SsIncentive> SsIncentives => Set<SsIncentive>();
    public DbSet<BankStatementRecord> BankStatementRecords => Set<BankStatementRecord>();
    public DbSet<DealerOutstanding> DealerOutstandings => Set<DealerOutstanding>();
    public DbSet<DealerTarget> DealerTargets => Set<DealerTarget>();
    public DbSet<AssetItem> AssetItems => Set<AssetItem>();

    // ── IT Operations Module ─────────────────────────────────────────
    public DbSet<ItMasterData> ItMasterDatas => Set<ItMasterData>();
    public DbSet<ItAsset> ItAssets => Set<ItAsset>();
    public DbSet<ItAssetHistory> ItAssetHistories => Set<ItAssetHistory>();
    public DbSet<ItSoftwareLicense> ItSoftwareLicenses => Set<ItSoftwareLicense>();
    public DbSet<ItTicket> ItTickets => Set<ItTicket>();
    public DbSet<ItTicketComment> ItTicketComments => Set<ItTicketComment>();
    public DbSet<ItSlaPolicy> ItSlaPolicies => Set<ItSlaPolicy>();
    public DbSet<ItMaintenanceSchedule> ItMaintenanceSchedules => Set<ItMaintenanceSchedule>();
    public DbSet<ItKbArticle> ItKbArticles => Set<ItKbArticle>();

    // ── SaaS Modules Extensions ──────────────────────────────────────
    public DbSet<HelpdeskTicket> HelpdeskTickets => Set<HelpdeskTicket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<DocumentItem> DocumentItems => Set<DocumentItem>();
    public DbSet<CustomerTask> CustomerTasks => Set<CustomerTask>();
    public DbSet<SystemNotification> SystemNotifications => Set<SystemNotification>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles => Set<KnowledgeBaseArticle>();
    public DbSet<CashVerification> CashVerifications => Set<CashVerification>();

    // ── Governor Engine ─────────────────────────────────────────────
    public DbSet<PartyCodeMapping> PartyCodeMappings => Set<PartyCodeMapping>();


    public DbSet<TdsRule> TdsRules => Set<TdsRule>();
    public DbSet<ColumnMappingRule> ColumnMappingRules => Set<ColumnMappingRule>();
    public DbSet<OutstandingRule> OutstandingRules => Set<OutstandingRule>();
    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<TallyIntegrationSetting> TallyIntegrationSettings => Set<TallyIntegrationSetting>();
    public DbSet<ReportColumnConfig> ReportColumnConfigs => Set<ReportColumnConfig>();
    public DbSet<HelpText> HelpTexts => Set<HelpText>();
    public DbSet<CustomReportLayout> CustomReportLayouts => Set<CustomReportLayout>();
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();

    // ── Engine DbSets ───────────────────────────────────────────────
    public DbSet<RuleMaster> RuleMasters => Set<RuleMaster>();
    public DbSet<RuleVersion> RuleVersions => Set<RuleVersion>();
    public DbSet<RuleCondition> RuleConditions => Set<RuleCondition>();
    public DbSet<ImportTemplate> ImportTemplates => Set<ImportTemplate>();
    public DbSet<ImportColumn> ImportColumns => Set<ImportColumn>();
    public DbSet<ImportMapping> ImportMappings => Set<ImportMapping>();
    public DbSet<ImportValidationRule> ImportValidationRules => Set<ImportValidationRule>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowAssignment> WorkflowAssignments => Set<WorkflowAssignment>();
    public DbSet<WorkflowHistory> WorkflowHistories => Set<WorkflowHistory>();


    // ── Cash Management Module ──────────────────────────────────────
    public DbSet<CashInTransaction>  CashInTransactions  => Set<CashInTransaction>();
    public DbSet<CashOutTransaction> CashOutTransactions => Set<CashOutTransaction>();
    public DbSet<CashReconRecord>    CashReconRecords    => Set<CashReconRecord>();
    public DbSet<CashException>      CashExceptions      => Set<CashException>();
    public DbSet<CashPeriodControl>  CashPeriodControls  => Set<CashPeriodControl>();
    public DbSet<CashMasterItem>     CashMasterItems     => Set<CashMasterItem>();
    public DbSet<CostCenterCash>     CostCenterCashes    => Set<CostCenterCash>();

    // ── Governor Engine ──────────────────────────────────────────────────────────
    public DbSet<ProductCodeMapping> ProductCodeMappings => Set<ProductCodeMapping>();

    // ── Branch Mapping Analytics Cache ───────────────────────────────────────────
    public DbSet<PartyPrimaryBranch> PartyPrimaryBranches => Set<PartyPrimaryBranch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        modelBuilder.Entity<UserRole>().HasQueryFilter(x => !x.User.IsDeleted && !x.Role.IsDeleted);
        modelBuilder.Entity<Role>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<User>().HasIndex(x => x.UserName).IsUnique();
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<Branch>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Party>().HasIndex(x => x.PartyCode).IsUnique();
        modelBuilder.Entity<BankDetail>().HasIndex(x => x.AccountNumber).IsUnique().HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<IncentiveScheme>().HasIndex(x => new { x.SchemeYear, x.SchemeMonth, x.Version }).IsUnique().HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<DealerMonthlyPerformance>().HasIndex(x => new { x.Year, x.Month, x.PartyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<IncentiveSummary>().HasIndex(x => new { x.Year, x.Month, x.PartyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<DealerSlabProgress>().HasIndex(x => new { x.Year, x.Month, x.PartyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<DealerGrowthAnalytics>().HasIndex(x => new { x.Year, x.Month, x.PartyId }).IsUnique().HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<MonthLock>().HasIndex(x => new { x.LockYear, x.LockMonth }).IsUnique().HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<RawRecord>().HasIndex(x => new { x.YearNumber, x.MonthNumber, x.ConsPartyCode }).IsUnique(false).HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<RawRecord>().HasIndex(x => x.ImportLogId).HasFilter("[IsDeleted] = 0");

        // ── Performance: Critical composite indexes on Raw table ─────────────────
        // These indexes are the single biggest performance win — covers every
        // monthly sales query and location filter without full table scans.
        modelBuilder.Entity<RawRecord>()
            .HasIndex(x => new { x.YearNumber, x.MonthNumber })
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Raw_Year_Month");

        modelBuilder.Entity<RawRecord>()
            .HasIndex(x => x.OriginalCode)
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Raw_OriginalCode");

        modelBuilder.Entity<RawRecord>()
            .HasIndex(x => x.Loc)
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Raw_Loc");

        // ── PartyPrimaryBranch: unique per party code ─────────────────────────────
        modelBuilder.Entity<PartyPrimaryBranch>()
            .HasIndex(x => x.PartyCode)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_PartyPrimaryBranch_PartyCode");

        modelBuilder.Entity<AssetItem>().HasIndex(x => new { x.BranchId, x.AssetCode }).IsUnique().HasFilter("[IsDeleted] = 0");


        modelBuilder.Entity<ExternalIncentiveRecord>()
            .HasOne(r => r.Upload)
            .WithMany(u => u.Records)
            .HasForeignKey(r => r.UploadId);
        modelBuilder.Entity<ExternalIncentiveRecord>()
            .HasIndex(x => new { x.Year, x.Month, x.ConsPartyCode, x.UploadId })
            .HasFilter("[IsDeleted] = 0");

        modelBuilder.Entity<TdsRule>().HasIndex(x => new { x.EffectiveFrom, x.EffectiveTo });
        modelBuilder.Entity<ColumnMappingRule>().HasIndex(x => new { x.ExcelHeader, x.UploadContext }).HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<RolePermission>().HasIndex(x => new { x.RoleName, x.Module, x.Action }).HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<ReportColumnConfig>().HasIndex(x => new { x.ReportName, x.ColumnKey }).HasFilter("[IsDeleted] = 0");

        // ── Governor Engine — PartyCodeMappings ─────────────────────
        // AlternativeCode is unique per active mapping (one alt → one original)
        modelBuilder.Entity<PartyCodeMapping>()
            .HasIndex(x => x.AlternativeCode)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
        // Allow fast lookup in the reverse direction (find all alts for an original)
        modelBuilder.Entity<PartyCodeMapping>()
            .HasIndex(x => x.OriginalCode)
            .HasFilter("[IsDeleted] = 0");

        // ── Engine Mappings ─────────────────────────────────────────────
        modelBuilder.Entity<RuleMaster>().HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<RuleVersion>().HasIndex(x => new { x.RuleMasterId, x.VersionNo }).IsUnique().HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<ImportTemplate>().HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<WorkflowDefinition>().HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<WorkflowAssignment>().HasIndex(x => new { x.TargetEntityId, x.TargetEntityType, x.WorkflowDefinitionId }).HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<IncentivePeriod>().HasIndex(x => new { x.Year, x.Month }).IsUnique().HasFilter("[IsDeleted] = 0");


        // SsIncentive Mappings
        modelBuilder.Entity<SsIncentive>().HasIndex(x => new { x.Year, x.Month, x.PartyCode, x.PartCategoryCode }).IsUnique().HasFilter("[IsDeleted] = 0");
        modelBuilder.Entity<SsIncentive>().HasIndex(x => x.Status);

        // DealerOutstanding Mappings
        modelBuilder.Entity<DealerOutstanding>().HasIndex(x => new { x.Year, x.Month, x.PartyCode }).IsUnique().HasFilter("[IsDeleted] = 0");

        // CostCenterCash Mappings
        modelBuilder.Entity<CostCenterCash>().HasIndex(x => new { x.Year, x.Month, x.CostCenterName }).IsUnique().HasFilter("[IsDeleted] = 0");



        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(t => typeof(AuditableEntity).IsAssignableFrom(t.ClrType)))
        {
            modelBuilder.Entity(entity.ClrType).HasQueryFilter(CreateSoftDeleteFilter(entity.ClrType));
        }

        modelBuilder.Seed();
    }

    private static readonly HashSet<string> ExcludedFromAudit = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(AuditLog),
        nameof(DealerMonthlyPerformance),
        nameof(IncentiveSummary),
        nameof(DealerSlabProgress),
        nameof(DealerGrowthAnalytics),
        nameof(CategorySalesAggregate)
    };

    public bool DisableAuditLogs { get; set; }

    private class DbAuditEntry(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        public Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry { get; } = entry;
        public string EntityName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public Dictionary<string, object?> OldValues { get; } = [];
        public Dictionary<string, object?> NewValues { get; } = [];

        public AuditLog ToAuditLog(string user, DateTime now, string? ip)
        {
            var auditLog = new AuditLog
            {
                EntityName = EntityName,
                Action = Action,
                ChangedBy = user,
                ChangedAt = now,
                IpAddress = ip,
                OldValue = JsonSerializer.Serialize(OldValues),
                NewValue = JsonSerializer.Serialize(NewValues)
            };

            var primaryKey = Entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            auditLog.EntityId = primaryKey?.CurrentValue?.ToString() ?? "0";

            return auditLog;
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var user = _httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
        var ip = _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString();

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = user;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = user;
            }
        }

        if (DisableAuditLogs)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        var auditEntries = new List<DbAuditEntry>();
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => !ExcludedFromAudit.Contains(e.Metadata.ClrType.Name))
            .ToList();

        foreach (var entry in entries)
        {
            var auditEntry = new DbAuditEntry(entry)
            {
                EntityName = entry.Metadata.ClrType.Name,
                Action = entry.State.ToString()
            };
            auditEntries.Add(auditEntry);

            foreach (var property in entry.Properties)
            {
                string propertyName = property.Metadata.Name;

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        break;

                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        break;
                }
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        if (auditEntries.Count > 0)
        {
            var auditLogs = auditEntries.Select(ae => ae.ToAuditLog(user, now, ip)).ToList();
            AuditLogs.AddRange(auditLogs);
            
            DisableAuditLogs = true;
            try
            {
                await base.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                DisableAuditLogs = false;
            }
        }

        return result;
    }

    private static System.Linq.Expressions.LambdaExpression CreateSoftDeleteFilter(Type type)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(type, "e");
        var property = System.Linq.Expressions.Expression.Property(parameter, nameof(AuditableEntity.IsDeleted));
        var body = System.Linq.Expressions.Expression.Equal(property, System.Linq.Expressions.Expression.Constant(false));
        return System.Linq.Expressions.Expression.Lambda(body, parameter);
    }
}

public static class ModelBuilderSeedExtensions
{
    public static void Seed(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = AppRoles.SuperAdmin },
            new Role { Id = 2, Name = AppRoles.HOFinance },
            new Role { Id = 3, Name = AppRoles.BranchManager },
            new Role { Id = 4, Name = AppRoles.Associate },
            new Role { Id = 5, Name = AppRoles.Auditor },
            new Role { Id = 6, Name = AppRoles.SalesExecutive });

        modelBuilder.Entity<Branch>().HasData(
            new Branch { Id = 1, Code = "HO", Name = "Head Office", Region = "Corporate" },
            new Branch { Id = 2, Code = "BR-N", Name = "North Branch", Region = "North" });

        modelBuilder.Entity<IncentiveScheme>().HasData(new IncentiveScheme
        {
            Id = 1,
            Name = "Standard Sales Value Scheme",
            SchemeMonth = 5,
            SchemeYear = 2026,
            Version = 1,
            EffectiveFrom = new DateTime(2026, 5, 1),
            EffectiveTo = new DateTime(2026, 5, 31)
        });

        modelBuilder.Entity<IncentiveSchemeDetail>().HasData(
            new IncentiveSchemeDetail { Id = 1, IncentiveSchemeId = 1, MinAchievementPercent = 0, MaxAchievementPercent = 29999m, FixedAmount = 0, Percentage = 0, RuleName = "Slab 1" },
            new IncentiveSchemeDetail { Id = 2, IncentiveSchemeId = 1, MinAchievementPercent = 30000, MaxAchievementPercent = 49999m, FixedAmount = 0, Percentage = 3, RuleName = "Slab 2" },
            new IncentiveSchemeDetail { Id = 3, IncentiveSchemeId = 1, MinAchievementPercent = 50000, MaxAchievementPercent = 74999m, FixedAmount = 0, Percentage = 4, RuleName = "Slab 3" },
            new IncentiveSchemeDetail { Id = 4, IncentiveSchemeId = 1, MinAchievementPercent = 75000, MaxAchievementPercent = 119999m, FixedAmount = 0, Percentage = 5, RuleName = "Slab 4" },
            new IncentiveSchemeDetail { Id = 5, IncentiveSchemeId = 1, MinAchievementPercent = 120000, MaxAchievementPercent = 999999999m, FixedAmount = 0, Percentage = 6, RuleName = "Slab 5" });
    }
}

public static class AppRoles
{
    public const string SuperAdmin = "Super Admin";
    public const string HOFinance = "HO Finance";
    public const string BranchManager = "Branch Manager";
    public const string Associate = "Associate";
    public const string Auditor = "Auditor";
    public const string SalesExecutive = "Sales Executive";
}
