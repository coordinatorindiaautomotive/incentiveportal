using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using ClosedXML.Excel;
using Hangfire;
using Microsoft.Extensions.Caching.Memory;
using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Models;
using IncentivePortal.Helpers;

namespace IncentivePortal.Services;

public interface IImportsAppService
{
    Task<(List<Branch> Branches, List<object> AlternateMappings)> GetMonthlySalesDataAsync(CancellationToken cancellationToken);
    
    Task<object> ParseExcelMetadataAsync(IFormFile file, ICurrentUser currentUser, CancellationToken cancellationToken);
    
    Task<(string PreviewToken, List<SalesImportRow> DisplayRows)> PreviewAsync(
        IFormFile file, 
        string uploadMode, 
        string? branchRulesJson, 
        string? alternateCodesJson, 
        ICurrentUser currentUser, 
        CancellationToken cancellationToken);

    Task<string> CommitAsync(
        IFormFile file,
        string uploadMode,
        string? branchRulesJson,
        string? alternateCodesJson,
        string? changeReason,
        int? previousImportLogId,
        string? previewToken,
        bool rewriteSales,
        ICurrentUser currentUser,
        CancellationToken cancellationToken);

    Task<(List<string> Locations, List<string> Categories, List<string> PartyTypes)> GetCalcMetadataAsync(int month, int year, CancellationToken cancellationToken);
    
    Task<string> RunCalculationJobAsync(
        int month, 
        int year, 
        bool forceRecalculate, 
        string? branchRulesJson, 
        string? partyMappingsJson, 
        ICurrentUser currentUser, 
        CancellationToken cancellationToken);
    
    Task<object> PreviewCalculationAsync(
        int month,
        int year,
        bool forceRecalculate,
        string? branchRulesJson,
        string? partyMappingsJson,
        ICurrentUser currentUser,
        CancellationToken cancellationToken);


    Task<object> GetSaleRowAsync(string partyCode, int month, int year, ICurrentUser currentUser, CancellationToken ct);
    
    Task EditSaleRowAsync(int id, decimal saleValue, decimal discount, string? remarks, ICurrentUser currentUser, CancellationToken ct);
    
    Task DeleteSaleRowAsync(int id, ICurrentUser currentUser, CancellationToken ct);
    
    Task<byte[]> ExportCalculationPreviewAsync(
        int month, 
        int year, 
        bool forceRecalculate, 
        string? branchRulesJson, 
        string? partyMappingsJson, 
        ICurrentUser currentUser, 
        CancellationToken cancellationToken);

    Task<int> ApproveCalculationAsync(int month, int year, CancellationToken ct);
}

public sealed class ImportsAppService(
    IncentiveDbContext db,
    ISalesImportService importService,
    IIncentiveCalculationService calculationService,
    IDashboardService dashboardService,
    IMemoryCache memoryCache
) : IImportsAppService
{
    public async Task<(List<Branch> Branches, List<object> AlternateMappings)> GetMonthlySalesDataAsync(CancellationToken cancellationToken)
    {
        var branches = await db.Branches
            .Where(b => !b.IsDeleted)
            .OrderBy(b => b.Code)
            .ToListAsync(cancellationToken);

        var alternateMappings = await db.Parties
            .Where(p => !p.IsDeleted && !string.IsNullOrEmpty(p.OriginalPartyCode))
            .Select(p => new { alternateCode = p.PartyCode, originalCode = p.OriginalPartyCode })
            .Cast<object>()
            .ToListAsync(cancellationToken);

        return (branches, alternateMappings);
    }

    public async Task<object> ParseExcelMetadataAsync(IFormFile file, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);

        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Loc"] = "Location",
            ["Incentive "] = "Incentive",
            ["Achievement %"] = "Achievement Percent",
            ["AchievementPercent"] = "Achievement Percent"
        };

        var rawSheet = workbook.Worksheets.FirstOrDefault(x => x.Name.Equals("Raw", StringComparison.OrdinalIgnoreCase));
        var summarySheet = workbook.Worksheets.FirstOrDefault(x => x.Name.Equals("Summary", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.First();

        var metaSheet = rawSheet ?? summarySheet;
        var metaMap = metaSheet.Row(1).CellsUsed().ToDictionary(
            x => aliases.GetValueOrDefault(x.Value.ToString().Trim(), x.Value.ToString().Trim()),
            x => x.Address.ColumnNumber,
            StringComparer.OrdinalIgnoreCase);

        if (!metaMap.ContainsKey("Location") && rawSheet != null)
        {
            metaSheet = summarySheet;
            metaMap = metaSheet.Row(1).CellsUsed().ToDictionary(
                x => aliases.GetValueOrDefault(x.Value.ToString().Trim(), x.Value.ToString().Trim()),
                x => x.Address.ColumnNumber,
                StringComparer.OrdinalIgnoreCase);
        }

        var hasLocation = metaMap.ContainsKey("Location");
        var hasCatCode = metaMap.ContainsKey("Part Category Code");
        var hasPartyType = metaMap.ContainsKey("Party Type");

        string? allowedBranchCode = null;
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole("Super Admin") && !currentUser.IsInRole("HO Finance") && !currentUser.IsInRole("Auditor"))
        {
            var userBranch = await db.Branches.FindAsync(currentUser.BranchId.Value);
            if (userBranch != null)
            {
                allowedBranchCode = userBranch.Code;
            }
        }

        var distinctLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var distinctCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var distinctPartyTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var rows = metaSheet.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            if (hasLocation)
            {
                var loc = row.Cell(metaMap["Location"]).Value.ToString().Trim();
                if (!string.IsNullOrEmpty(loc))
                {
                    if (allowedBranchCode != null && !loc.Equals(allowedBranchCode, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    distinctLocations.Add(loc);
                }
            }

            if (hasCatCode)
            {
                var cat = row.Cell(metaMap["Part Category Code"]).Value.ToString().Trim();
                if (!string.IsNullOrEmpty(cat)) distinctCategories.Add(cat);
            }

            if (hasPartyType)
            {
                var type = row.Cell(metaMap["Party Type"]).Value.ToString().Trim();
                if (!string.IsNullOrEmpty(type)) distinctPartyTypes.Add(type);
            }
        }

        if (distinctLocations.Count == 0)
        {
            var allBranches = await db.Branches.ToListAsync(cancellationToken);
            foreach (var br in allBranches)
            {
                if (allowedBranchCode != null && !br.Code.Equals(allowedBranchCode, StringComparison.OrdinalIgnoreCase))
                    continue;
                distinctLocations.Add(br.Code);
            }
        }

        var dbBranches = await db.Branches.ToListAsync(cancellationToken);
        var seededBranches = dbBranches.ToDictionary(
            x => x.Code, 
            x => new { allowedCategories = x.AllowedCategories, allowedPartyTypes = x.AllowedPartyTypes }, 
            StringComparer.OrdinalIgnoreCase);
        
        var dbParties = await db.Parties
            .Where(x => !string.IsNullOrEmpty(x.OriginalPartyCode))
            .ToListAsync(cancellationToken);
        var seededPartyMappings = dbParties.Select(x => new { alternateCode = x.PartyCode, originalCode = x.OriginalPartyCode }).ToList();

        return new
        {
            locations = distinctLocations.OrderBy(x => x).ToList(),
            categories = distinctCategories.OrderBy(x => x).ToList(),
            partyTypes = distinctPartyTypes.OrderBy(x => x).ToList(),
            seededBranches = seededBranches,
            seededPartyMappings = seededPartyMappings
        };
    }

    public async Task<(string PreviewToken, List<SalesImportRow> DisplayRows)> PreviewAsync(
        IFormFile file, 
        string uploadMode, 
        string? branchRulesJson, 
        string? alternateCodesJson, 
        ICurrentUser currentUser, 
        CancellationToken cancellationToken)
    {
        var allRows = await importService.PreviewAsync(
            file,
            uploadMode,
            branchRulesJson,
            alternateCodesJson,
            cancellationToken,
            limit: null);

        var previewToken = Guid.NewGuid().ToString("N");
        memoryCache.Set($"PreviewRows_{previewToken}", allRows, TimeSpan.FromHours(2));

        var result = allRows;
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole("Super Admin") && !currentUser.IsInRole("HO Finance") && !currentUser.IsInRole("Auditor"))
        {
            var userBranch = await db.Branches.FindAsync(currentUser.BranchId.Value);
            if (userBranch != null)
            {
                result = result.Where(x => x.Location.Equals(userBranch.Code, StringComparison.OrdinalIgnoreCase)).ToList().AsReadOnly();
            }
        }

        var displayRows = result.Count > 5000 ? result.Take(5000).ToList() : result.ToList();
        return (previewToken, displayRows);
    }

    public async Task<string> CommitAsync(
        IFormFile file,
        string uploadMode,
        string? branchRulesJson,
        string? alternateCodesJson,
        string? changeReason,
        int? previousImportLogId,
        string? previewToken,
        bool rewriteSales,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(branchRulesJson))
        {
            var branchRules = JsonSerializer.Deserialize<List<BranchCalcRule>>(
                branchRulesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (branchRules != null && branchRules.Count > 0)
            {
                var dbBranches = await db.Branches.Where(b => !b.IsDeleted).ToListAsync(cancellationToken);
                foreach (var rule in branchRules)
                {
                    var br = dbBranches.FirstOrDefault(b => b.Code.Equals(rule.Location, StringComparison.OrdinalIgnoreCase));
                    if (br != null)
                    {
                        br.AllowedCategories = rule.AllowedCategories ?? string.Empty;
                        br.AllowedPartyTypes = rule.AllowedPartyTypes ?? string.Empty;
                        br.UpdatedAt = DateTime.UtcNow;
                        br.UpdatedBy = currentUser.UserName;
                    }
                }
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads");
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }
        
        try
        {
            foreach (var oldFile in Directory.GetFiles(tempDir))
            {
                if (File.GetCreationTimeUtc(oldFile) < DateTime.UtcNow.AddHours(-12))
                {
                    try { File.Delete(oldFile); } catch { }
                }
            }
        }
        catch { }

        var tempFileName = $"{Guid.NewGuid()}_{file.FileName}";
        var tempFilePath = Path.Combine(tempDir, tempFileName);

        using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream, cancellationToken);
        }

        var jobId = Guid.NewGuid().ToString("N");
        BackgroundJob.Enqueue<IBackgroundJobExecutor>(x =>
            x.RunImportJobAsync(
                jobId,
                tempFilePath,
                file.FileName,
                uploadMode,
                branchRulesJson,
                alternateCodesJson,
                changeReason,
                previousImportLogId,
                rewriteSales,
                currentUser.UserName,
                previewToken));

        var state = new BackgroundJobState
        {
            JobId = jobId,
            Status = "Pending",
            Message = "Queueing import job in background worker..."
        };
        memoryCache.Set($"JobStatus_{jobId}", state, TimeSpan.FromHours(1));

        dashboardService.InvalidateCache();

        return jobId;
    }

    public async Task<(List<string> Locations, List<string> Categories, List<string> PartyTypes)> GetCalcMetadataAsync(int month, int year, CancellationToken cancellationToken)
    {
        var locations = await db.Raws
            .Where(x => x.MonthNumber == month && x.YearNumber == year && !x.IsDeleted && !string.IsNullOrEmpty(x.Loc))
            .Select(x => x.Loc!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var categories = await db.Raws
            .Where(x => x.MonthNumber == month && x.YearNumber == year && !x.IsDeleted && !string.IsNullOrEmpty(x.PartCategoryCode))
            .Select(x => x.PartCategoryCode!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var partyTypes = await db.Raws
            .Where(x => x.MonthNumber == month && x.YearNumber == year && !x.IsDeleted && !string.IsNullOrEmpty(x.PartyType))
            .Select(x => x.PartyType!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        if (partyTypes.Count == 0)
        {
            partyTypes = await db.Parties
                .Where(x => !x.IsDeleted && !string.IsNullOrEmpty(x.DealerType))
                .Select(x => x.DealerType)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }

        if (locations.Count == 0)
        {
            locations = await db.Branches
                .Where(x => !x.IsDeleted)
                .Select(x => x.Code)
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }

        return (locations, categories, partyTypes);
    }

    public async Task<string> RunCalculationJobAsync(int month, int year, bool forceRecalculate, string? branchRulesJson, string? partyMappingsJson, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(branchRulesJson))
        {
            var branchRules = JsonSerializer.Deserialize<List<BranchCalcRule>>(
                branchRulesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (branchRules != null && branchRules.Count > 0)
            {
                var dbBranches = await db.Branches.Where(b => !b.IsDeleted).ToListAsync(cancellationToken);
                foreach (var rule in branchRules)
                {
                    var br = dbBranches.FirstOrDefault(b => b.Code.Equals(rule.Location, StringComparison.OrdinalIgnoreCase));
                    if (br != null)
                    {
                        br.AllowedCategories = rule.AllowedCategories ?? string.Empty;
                        br.AllowedPartyTypes = rule.AllowedPartyTypes ?? string.Empty;
                        br.UpdatedAt = DateTime.UtcNow;
                        br.UpdatedBy = currentUser.UserName;
                    }
                }
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var jobId = Guid.NewGuid().ToString("N");
        BackgroundJob.Enqueue<IBackgroundJobExecutor>(x =>
            x.RunCalculateJobAsync(
                jobId,
                month,
                year,
                forceRecalculate,
                branchRulesJson,
                partyMappingsJson,
                currentUser.UserName));

        var state = new BackgroundJobState
        {
            JobId = jobId,
            Status = "Pending",
            Message = "Queueing calculation job in background worker..."
        };
        memoryCache.Set($"JobStatus_{jobId}", state, TimeSpan.FromHours(1));

        dashboardService.InvalidateCache();

        return jobId;
    }

    public async Task<object> PreviewCalculationAsync(
        int month,
        int year,
        bool forceRecalculate,
        string? branchRulesJson,
        string? partyMappingsJson,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<BranchCalcRule>? branchRules = null;
        if (!string.IsNullOrWhiteSpace(branchRulesJson))
        {
            branchRules = JsonSerializer.Deserialize<List<BranchCalcRule>>(
                branchRulesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        IReadOnlyList<PartyMappingRule>? customMappings = null;
        if (!string.IsNullOrWhiteSpace(partyMappingsJson))
        {
            customMappings = JsonSerializer.Deserialize<List<PartyMappingRule>>(
                partyMappingsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        var strategy = db.Database.CreateExecutionStrategy();
        object? previewRows = null;

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            
            await calculationService.CalculateMonthAsync(
                month,
                year,
                forceRecalculate,
                branchRules,
                false,
                customMappings,
                cancellationToken);

            var ledgers = await db.SsIncentives
                .Where(x => x.Month == month && x.Year == year && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            if (currentUser.BranchId.HasValue && !currentUser.IsInRole("Super Admin") && !currentUser.IsInRole("HO Finance") && !currentUser.IsInRole("Auditor"))
            {
                var userBranch = await db.Branches.FindAsync(currentUser.BranchId.Value);
                if (userBranch != null && !string.IsNullOrEmpty(userBranch.Code))
                {
                    var branchParties = await db.Parties.Where(p => p.BranchId == userBranch.Id).Select(p => p.PartyCode).ToListAsync(cancellationToken);
                    ledgers = ledgers.Where(x => branchParties.Contains(x.PartyCode)).ToList();
                }
            }

            previewRows = ledgers.Select(l => {
                var netEligible = Math.Max(0, l.GrossIncentive - l.TdsAmount);
                var adjusted = Math.Max(0, netEligible - l.NetTransferAmount);
                return new {
                    partyCode = l.PartyCode,
                    partyName = l.PartyName,
                    location = l.SourceLocation,
                    saleValue = l.SaleValue,
                    discount = l.OnBillDiscount,
                    slabApplied = (l.SlabPercent * 100).ToString("F2") + "%",
                    grossIncentive = l.GrossIncentive,
                    tdsPercent = l.GrossIncentive > 0 ? Math.Round((l.TdsAmount / l.GrossIncentive) * 100, 2) : 0m,
                    tdsAmount = l.TdsAmount,
                    adjustedAmount = adjusted,
                    netTransfer = l.NetTransferAmount,
                    status = l.Status,
                    isEdited = l.IsEdited,
                    remarks = l.Remarks
                };
            }).ToList();

            await transaction.RollbackAsync(cancellationToken);
        });

        return previewRows!;
    }



    public async Task<object> GetSaleRowAsync(string partyCode, int month, int year, ICurrentUser currentUser, CancellationToken ct)
    {
        var inc = await db.SsIncentives
            .Where(s => s.PartyCode == partyCode && s.Month == month && s.Year == year && !s.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (inc == null)
            throw new KeyNotFoundException($"No incentive record found for {partyCode} in {month:D2}/{year}.");

        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance))
        {
            var userBranch = await db.Branches.FindAsync(currentUser.BranchId.Value);
            if (userBranch != null && !inc.SourceLocation.Equals(userBranch.Code, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException();
        }

        return new
        {
            id = inc.Id,
            partyCode = inc.PartyCode,
            partyName = inc.PartyName,
            location = inc.SourceLocation,
            saleValue = inc.SaleValue,
            discount = inc.OnBillDiscount,
            month = inc.Month,
            year = inc.Year,
            isLocked = inc.Status == "Posted"
        };
    }

    public async Task EditSaleRowAsync(int id, decimal saleValue, decimal discount, string? remarks, ICurrentUser currentUser, CancellationToken ct)
    {
        var inc = await db.SsIncentives
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Incentive record not found.");

        if (inc.Status == "Posted")
            throw new InvalidOperationException($"Period {inc.Month:D2}/{inc.Year} is locked. Unlock it first from Control Tower.");

        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance))
        {
            var userBranch = await db.Branches.FindAsync(currentUser.BranchId.Value);
            if (userBranch != null && !inc.SourceLocation.Equals(userBranch.Code, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException();
        }

        inc.SaleValue = saleValue;
        inc.OnBillDiscount = discount;
        inc.IsEdited = true;
        if (!string.IsNullOrWhiteSpace(remarks))
            inc.Remarks = remarks;
        inc.UpdatedAt = DateTime.UtcNow;
        inc.UpdatedBy = currentUser.UserName;

        await db.SaveChangesAsync(ct);
        dashboardService.InvalidateCache();
    }

    public async Task DeleteSaleRowAsync(int id, ICurrentUser currentUser, CancellationToken ct)
    {
        var inc = await db.SsIncentives
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Incentive record not found.");

        if (inc.Status == "Posted")
            throw new InvalidOperationException($"Period {inc.Month:D2}/{inc.Year} is locked. Unlock it first from Control Tower.");

        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance))
        {
            var userBranch = await db.Branches.FindAsync(currentUser.BranchId.Value);
            if (userBranch != null && !inc.SourceLocation.Equals(userBranch.Code, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException();
        }

        inc.IsDeleted = true;
        inc.UpdatedAt = DateTime.UtcNow;
        inc.UpdatedBy = currentUser.UserName;

        await db.SaveChangesAsync(ct);
        dashboardService.InvalidateCache();
    }

    public async Task<byte[]> ExportCalculationPreviewAsync(
        int month, 
        int year, 
        bool forceRecalculate, 
        string? branchRulesJson, 
        string? partyMappingsJson, 
        ICurrentUser currentUser, 
        CancellationToken cancellationToken)
    {
        IReadOnlyList<BranchCalcRule>? branchRules = null;
        if (!string.IsNullOrWhiteSpace(branchRulesJson))
        {
            branchRules = JsonSerializer.Deserialize<List<BranchCalcRule>>(
                branchRulesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        IReadOnlyList<PartyMappingRule>? customMappings = null;
        if (!string.IsNullOrWhiteSpace(partyMappingsJson))
        {
            customMappings = JsonSerializer.Deserialize<List<PartyMappingRule>>(
                partyMappingsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        var strategy = db.Database.CreateExecutionStrategy();
        byte[]? excelContent = null;

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await calculationService.CalculateMonthAsync(
                month,
                year,
                forceRecalculate,
                branchRules,
                false,
                customMappings,
                cancellationToken: cancellationToken);

            var ledgers = await db.SsIncentives
                .Where(x => x.Month == month && x.Year == year && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            if (currentUser.BranchId.HasValue && !currentUser.IsInRole("Super Admin") && !currentUser.IsInRole("HO Finance") && !currentUser.IsInRole("Auditor"))
            {
                var userBranch = await db.Branches.FindAsync(currentUser.BranchId.Value);
                if (userBranch != null && !string.IsNullOrEmpty(userBranch.Code))
                {
                    var branchParties = await db.Parties.Where(p => p.BranchId == userBranch.Id).Select(p => p.PartyCode).ToListAsync(cancellationToken);
                    ledgers = ledgers.Where(x => branchParties.Contains(x.PartyCode)).ToList();
                }
            }

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Calculation Preview");

            var headers = new[] {
                "Party Code", "Party Name", "Branch", "Net Retail (Sales)", "Discount",
                "Slab Applied", "Gross Incentive", "TDS %", "TDS Amount", "Adjusted Amount",
                "Net Transfer", "Remarks"
            };
            for (var i = 0; i < headers.Length; i++)
            {
                sheet.Cell(1, i + 1).Value = headers[i];
                sheet.Cell(1, i + 1).Style.Font.Bold = true;
                sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            var rIndex = 2;
            foreach (var ledger in ledgers)
            {
                var netEligible = Math.Max(0, ledger.GrossIncentive - ledger.TdsAmount);
                var adjusted = Math.Max(0, netEligible - ledger.NetTransferAmount);
                var tdsPct = ledger.GrossIncentive > 0 ? ledger.TdsAmount / ledger.GrossIncentive : 0m;

                sheet.Cell(rIndex, 1).Value = ledger.PartyCode;
                sheet.Cell(rIndex, 2).Value = ledger.PartyName;
                sheet.Cell(rIndex, 3).Value = ledger.SourceLocation;
                sheet.Cell(rIndex, 4).Value = ledger.SaleValue;
                sheet.Cell(rIndex, 5).Value = ledger.OnBillDiscount;
                sheet.Cell(rIndex, 6).Value = (ledger.SlabPercent * 100).ToString("F2") + "%";
                sheet.Cell(rIndex, 7).Value = ledger.GrossIncentive;
                sheet.Cell(rIndex, 8).Value = tdsPct;
                sheet.Cell(rIndex, 8).Style.NumberFormat.Format = "0.0%";
                sheet.Cell(rIndex, 9).Value = ledger.TdsAmount;
                sheet.Cell(rIndex, 10).Value = adjusted;
                sheet.Cell(rIndex, 11).Value = ledger.NetTransferAmount;
                sheet.Cell(rIndex, 12).Value = ledger.Remarks;
                rIndex++;
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            excelContent = stream.ToArray();

            await transaction.RollbackAsync(cancellationToken);
        });

        return excelContent!;
    }

    public async Task<int> ApproveCalculationAsync(int month, int year, CancellationToken ct)
    {
        var pendingRecords = await db.SsIncentives
            .Where(x => x.Month == month && x.Year == year && x.Status == "Pending Approval")
            .ToListAsync(ct);

        if (pendingRecords.Count == 0)
            return 0;

        foreach (var rec in pendingRecords)
        {
            rec.Status = "Posted";
            rec.Remarks = "Approved by Admin";
            rec.ProcessingDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return pendingRecords.Count;
    }
}
