using System.Text;
using Hangfire;
using Hangfire.SqlServer;
using IncentivePortal.Data;
using IncentivePortal.Helpers;
using IncentivePortal.Middleware;
using IncentivePortal.Repositories;
using IncentivePortal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using IncentivePortal.Application.Reports;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 250_000_000; // 250 MB
});
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 250_000_000; // 250 MB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 250_000_000; // 250 MB
});
builder.Services.AddMemoryCache();
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "IncentivePortal_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddScoped<IncentivePortal.Infrastructure.Cache.ILookupCacheService, IncentivePortal.Infrastructure.Cache.RedisLookupCacheService>();
builder.Services.AddHttpClient();

// ── Response Compression (Brotli + Gzip) ─────────────────────────
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["text/html", "text/css", "application/javascript", "application/json", "text/plain"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.AddSingleton<IncentivePortal.Data.AuditSaveChangesInterceptor>();

builder.Services.AddDbContextFactory<IncentiveDbContext>((sp, options) =>
{
    var auditInterceptor = sp.GetRequiredService<IncentivePortal.Data.AuditSaveChangesInterceptor>();
    options.AddInterceptors(auditInterceptor);
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.CommandTimeout(300);
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        });
});

builder.Services.AddScoped(p => p.GetRequiredService<IDbContextFactory<IncentiveDbContext>>().CreateDbContext());

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"), new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer();

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Jwt:Key is not configured. Set a real 64+ char secret via user-secrets or environment variable before running.");
}

var adminPassword = builder.Configuration["SeedData:AdminPassword"];
var seedEnabled = builder.Configuration.GetValue<bool>("SeedData:Enabled");
if (seedEnabled && (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException("SeedData:AdminPassword is not configured. Set a real secret via user-secrets or environment variable before running.");
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        options.SlidingExpiration = true;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddControllers();
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IDashboardQueriesService, DashboardQueriesService>();
builder.Services.AddScoped<IPartyService, PartyService>();
builder.Services.AddScoped<IBankService, BankService>();
builder.Services.AddScoped<ISchemeService, SchemeService>();
builder.Services.AddScoped<ISalesImportService, SalesImportService>();
builder.Services.AddScoped<IImportsAppService, ImportsAppService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<IControlTowerService, ControlTowerService>();
builder.Services.AddScoped<IImportMappingService, ImportMappingService>();
builder.Services.AddScoped<IDistributedLockService, SqlServerDistributedLockService>();
builder.Services.AddScoped<IImportValidationService, ImportValidationService>();
builder.Services.AddScoped<IImportDuplicateCheckService, ImportDuplicateCheckService>();
builder.Services.AddScoped<IImportCommitService, ImportCommitService>();
builder.Services.AddScoped<IImportRollbackService, ImportRollbackService>();
builder.Services.AddScoped<IImportPreviewService, ImportPreviewService>();
builder.Services.AddScoped<IIncentiveCalculationService, IncentiveCalculationService>();
builder.Services.AddScoped<IAnalyticsRefreshService, AnalyticsRefreshService>();
builder.Services.AddScoped<IPartyBranchMappingService, PartyBranchMappingService>();
builder.Services.AddScoped<ITallyIntegrationService, TallyIntegrationService>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new System.IO.DirectoryInfo(System.IO.Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys")));
builder.Services.AddScoped<IFormulaEngineService, FormulaEngineService>();
builder.Services.AddScoped<IRuleEngineService, RuleEngineService>();
builder.Services.AddScoped<IIncentiveEngineService, IncentiveEngineService>();
builder.Services.AddScoped<IUploadEngineService, UploadEngineService>();
builder.Services.AddScoped<IWorkflowEngineService, WorkflowEngineService>();
builder.Services.AddScoped<IAuditEngineService, AuditEngineService>();
builder.Services.AddScoped<IReportEngineService, ReportEngineService>();
builder.Services.AddScoped<IReportBuilderService, ReportBuilderService>();
builder.Services.AddScoped<ICustomer360Service, Customer360Service>();
builder.Services.AddScoped<IReportExportService, ReportExportService>();
builder.Services.AddScoped<ICashManagementService, CashManagementService>();
builder.Services.AddScoped<IncentivePortal.Application.CashManagement.Services.IBankPaymentImportService,
                            IncentivePortal.Application.CashManagement.Services.BankPaymentImportService>();
builder.Services.AddScoped<IBackgroundJobExecutor, BackgroundJobExecutor>();
builder.Services.AddScoped<IDynamicReportService, DynamicReportService>();

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<SeedDataInitializer>();

builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.SchemaName = "dbo";
    options.TableName = "SessionCache";
});
builder.Services.AddWebOptimizer(pipeline =>
{
    pipeline.AddCssBundle("/css/core.min.css", "lib/bootstrap/dist/css/bootstrap.min.css");
    pipeline.AddCssBundle("/css/app.min.css", 
        "css/design-tokens.css",
        "css/reset.css",
        "css/typography.css",
        "css/layout.css",
        "css/sidebar.css",
        "css/topbar.css",
        "css/dashboard.css",
        "css/cards.css",
        "css/tables.css",
        "css/forms.css",
        "css/modals.css",
        "css/alerts.css",
        "css/utilities.css",
        "css/dark-theme.css",
        "css/responsive.css",
        "css/saas-upgrades.css",
        "css/components.css");
});

builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<SeedDataInitializer>().EnsureSeedDataAsync();

    // Warm up primary branch mapping cache in background (non-blocking)
    _ = Task.Run(async () =>
    {
        try
        {
            using var bgScope = app.Services.CreateScope();
            await bgScope.ServiceProvider
                .GetRequiredService<IPartyBranchMappingService>()
                .RefreshAsync();
            Console.WriteLine("[STARTUP] PartyPrimaryBranch cache refreshed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[STARTUP] PartyPrimaryBranch cache refresh failed: {ex.Message}");
        }
    });
}



// Configure the HTTP request pipeline.
app.UseResponseCompression();
app.UseWebOptimizer();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache versioned static assets for 1 year; unversioned for 1 hour
        var path = ctx.File.Name;
        var headers = ctx.Context.Response.Headers;
        if (path.Contains('?') || ctx.Context.Request.Query.ContainsKey("v"))
            headers["Cache-Control"] = "public, max-age=31536000, immutable";
        else
            headers["Cache-Control"] = "public, max-age=3600";
    }
});

app.UseMiddleware<IncentivePortal.Middleware.GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseRouting();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<AjaxExceptionMiddleware>();
app.UseSession();
app.UseAuthentication();
app.UseMiddleware<IncentivePortal.Middleware.DynamicAuthorizationMiddleware>();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new IncentivePortal.Helpers.HangfireDashboardAuthorizationFilter() }
});

// ── STARTUP: Purge stale Hangfire jobs (opt-in) ──
var purgeStaleJobs = app.Configuration.GetValue<bool>("Hangfire:PurgeStaleJobsOnStartup");
if (app.Environment.IsDevelopment() || purgeStaleJobs)
{
    try
    {
        var thresholdMin = app.Configuration.GetValue<int>("Hangfire:StaleJobThresholdMinutes");
        if (thresholdMin <= 0) thresholdMin = 60;

        var connStr = app.Configuration.GetConnectionString("DefaultConnection")!;
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
        await conn.OpenAsync();
        
        using var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = $"""
            SELECT Id FROM [HangFire].[Job] 
            WHERE [StateName] = 'Processing' 
            AND CreatedAt < DATEADD(minute, -{thresholdMin}, GETUTCDATE())
            """;
        var purgedIds = new System.Collections.Generic.List<string>();
        using (var reader = await selectCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                purgedIds.Add(reader[0].ToString()!);
            }
        }

        if (purgedIds.Count > 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                DELETE s FROM [HangFire].[State] s
                    INNER JOIN [HangFire].[Job] j ON s.JobId = j.Id
                    WHERE j.[StateName] = 'Processing' AND j.CreatedAt < DATEADD(minute, -{thresholdMin}, GETUTCDATE());
                DELETE FROM [HangFire].[Job] 
                    WHERE [StateName] = 'Processing' AND CreatedAt < DATEADD(minute, -{thresholdMin}, GETUTCDATE());
                """;
            var affected = await cmd.ExecuteNonQueryAsync();
            Console.WriteLine($"[STARTUP] Purged {purgedIds.Count} stale 'Processing' Hangfire jobs (rows affected: {affected}). IDs: {string.Join(", ", purgedIds)}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[STARTUP] Failed to purge stale Hangfire jobs: {ex.Message}");
    }
}

app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
