using System.Text.Json;
using IncentivePortal.Data;
using IncentivePortal.Models.CacheDTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace IncentivePortal.Infrastructure.Cache;

public class RedisLookupCacheService : ILookupCacheService
{
    private readonly IDistributedCache _cache;
    private readonly IncentiveDbContext _db;

    // Cache Keys
    private const string CacheKey_ValidParties      = "Lookups:ValidParties";
    private const string CacheKey_ValidBranches     = "Lookups:ValidBranches";
    private const string CacheKeyPrefix_Party       = "Lookups:Party:";
    private const string CacheKeyPrefix_Branch      = "Lookups:Branch:";
    private const string CacheKeyPrefix_Consignee   = "Lookups:ConsigneeBranches:";
    private const string CacheKeyPrefix_AnchorYear  = "Dashboard:AnchorYear:";
    private const string CacheKeyPrefix_LatestMonth = "Dashboard:LatestMonthInfo:";

    // TTLs
    private readonly DistributedCacheEntryOptions _12h = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12) };
    private readonly DistributedCacheEntryOptions _10m = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) };
    private readonly DistributedCacheEntryOptions _5m  = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };

    public RedisLookupCacheService(IDistributedCache cache, IncentiveDbContext db)
    {
        _cache = cache;
        _db    = db;
    }

    // ── Lookup: Party/Branch HashSets ─────────────────────────────────────────

    public async Task<HashSet<string>> GetValidPartyCodesAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetStringAsync(CacheKey_ValidParties, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
            return JsonSerializer.Deserialize<HashSet<string>>(cached) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var parties = await _db.Parties.IgnoreQueryFilters().Select(p => p.PartyCode).ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);
        await _cache.SetStringAsync(CacheKey_ValidParties, JsonSerializer.Serialize(parties), _12h, cancellationToken);
        return parties;
    }

    public async Task<HashSet<string>> GetValidBranchCodesAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetStringAsync(CacheKey_ValidBranches, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
            return JsonSerializer.Deserialize<HashSet<string>>(cached) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var branches = await _db.Branches.IgnoreQueryFilters().Select(b => b.Code).ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);
        await _cache.SetStringAsync(CacheKey_ValidBranches, JsonSerializer.Serialize(branches), _12h, cancellationToken);
        return branches;
    }

    // ── Lookup: Single entity by code ─────────────────────────────────────────

    public async Task<PartyCacheDto?> GetPartyByCodeAsync(string partyCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partyCode)) return null;

        var key    = $"{CacheKeyPrefix_Party}{partyCode.ToUpperInvariant()}";
        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
            return JsonSerializer.Deserialize<PartyCacheDto>(cached);

        var party = await _db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.PartyCode == partyCode, cancellationToken);
        if (party == null) return null;

        var dto = new PartyCacheDto { Id = party.Id, PartyCode = party.PartyCode, DealerType = party.DealerType ?? string.Empty, BranchId = party.BranchId };
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(dto), _12h, cancellationToken);
        return dto;
    }

    public async Task<BranchCacheDto?> GetBranchByCodeAsync(string branchCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branchCode)) return null;

        var key    = $"{CacheKeyPrefix_Branch}{branchCode.ToUpperInvariant()}";
        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
            return JsonSerializer.Deserialize<BranchCacheDto>(cached);

        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Code == branchCode, cancellationToken);
        if (branch == null) return null;

        var dto = new BranchCacheDto { Id = branch.Id, Code = branch.Code, Name = branch.Name, Region = branch.Region ?? string.Empty };
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(dto), _12h, cancellationToken);
        return dto;
    }

    public async Task<BranchCacheDto?> GetBranchByIdAsync(int branchId, IncentivePortal.Data.IncentiveDbContext db, CancellationToken cancellationToken = default)
    {
        // Key by ID so dashboard can resolve the current user's branch without knowing the code upfront.
        var idKey  = $"{CacheKeyPrefix_Branch}id:{branchId}";
        var cached = await _cache.GetStringAsync(idKey, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
            return JsonSerializer.Deserialize<BranchCacheDto>(cached);

        var branch = await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);
        if (branch == null) return null;

        var dto = new BranchCacheDto { Id = branch.Id, Code = branch.Code, Name = branch.Name, Region = branch.Region ?? string.Empty };
        var json = JsonSerializer.Serialize(dto);

        // Store under both the ID key and the code key so future GetBranchByCodeAsync calls are also served from cache.
        await _cache.SetStringAsync(idKey, json, _12h, cancellationToken);
        await _cache.SetStringAsync($"{CacheKeyPrefix_Branch}{branch.Code.ToUpperInvariant()}", json, _12h, cancellationToken);
        return dto;
    }

    // ── Sprint 19: Dashboard-specific cache methods ───────────────────────────

    /// <inheritdoc/>
    public async Task<List<string>> GetBranchCodesByConsigneeAsync(IReadOnlyList<string> consignees, CancellationToken cancellationToken = default)
    {
        if (consignees.Count == 0) return new List<string>();

        var keyPart = string.Join("_", consignees.OrderBy(c => c).Select(c => c.ToUpperInvariant()));
        var key     = $"{CacheKeyPrefix_Consignee}{keyPart}";

        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
            return JsonSerializer.Deserialize<List<string>>(cached) ?? new List<string>();

        var codes = await _db.Branches.AsNoTracking()
            .Where(b => b.Consignee != null && consignees.Contains(b.Consignee))
            .Select(b => b.Code)
            .ToListAsync(cancellationToken);

        await _cache.SetStringAsync(key, JsonSerializer.Serialize(codes), _12h, cancellationToken);
        return codes;
    }

    /// <inheritdoc/>
    public async Task<int?> GetDashboardAnchorYearAsync(string cacheKeySuffix, CancellationToken cancellationToken = default)
    {
        var key    = $"{CacheKeyPrefix_AnchorYear}{cacheKeySuffix}";
        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(cached) && int.TryParse(cached, out int parsedYear))
            return parsedYear;

        var latestLedger = await _db.SsIncentives
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.ImportLogId > 0)
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Select(x => new { x.Year, x.Month })
            .FirstOrDefaultAsync(cancellationToken);

        int? anchorYear = latestLedger == null
            ? null
            : (latestLedger.Month >= 4 ? latestLedger.Year : latestLedger.Year - 1);

        if (anchorYear.HasValue)
            await _cache.SetStringAsync(key, anchorYear.Value.ToString(), _5m, cancellationToken);

        return anchorYear;
    }

    /// <inheritdoc/>
    public async Task<(int LatestMonth, int? MaxDay)?> GetLatestMonthInfoAsync(int targetYear, string cacheKeySuffix, CancellationToken cancellationToken = default)
    {
        var key    = $"{CacheKeyPrefix_LatestMonth}{targetYear}:{cacheKeySuffix}";
        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            var hit = JsonSerializer.Deserialize<LatestMonthInfoDto>(cached);
            if (hit != null) return (hit.LatestMonth, hit.MaxDay);
        }

        var fiscalOrder   = new List<int> { 4, 5, 6, 7, 8, 9, 10, 11, 12, 1, 2, 3 };
        var presentMonths = await _db.Raws.AsNoTracking()
            .Where(x => !x.IsDeleted && x.ImportLogId > 0 &&
                        (x.YearNumber == targetYear || x.YearNumber == targetYear + 1))
            .Select(x => x.MonthNumber ?? 0)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (presentMonths.Count == 0) return null;

        int latestM      = presentMonths.OrderByDescending(m => fiscalOrder.IndexOf(m)).First();
        int calendarYear = latestM <= 3 ? targetYear + 1 : targetYear;

        int? maxDay = await _db.Raws.AsNoTracking()
            .Where(x => !x.IsDeleted && x.ImportLogId > 0
                        && x.YearNumber == calendarYear && x.MonthNumber == latestM && x.Day.HasValue)
            .Select(x => x.Day)
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync(cancellationToken);

        var dto = new LatestMonthInfoDto { LatestMonth = latestM, MaxDay = maxDay };
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(dto), _10m, cancellationToken);
        return (latestM, maxDay);
    }

    // ── Invalidation ──────────────────────────────────────────────────────────

    public async Task InvalidatePartiesCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(CacheKey_ValidParties, cancellationToken);
    }

    public async Task InvalidateBranchesCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(CacheKey_ValidBranches, cancellationToken);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private sealed class LatestMonthInfoDto
    {
        public int  LatestMonth { get; set; }
        public int? MaxDay      { get; set; }
    }
}
