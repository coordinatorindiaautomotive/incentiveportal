using IncentivePortal.Models.CacheDTOs;

namespace IncentivePortal.Infrastructure.Cache;

public interface ILookupCacheService
{
    Task<HashSet<string>> GetValidPartyCodesAsync(CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetValidBranchCodesAsync(CancellationToken cancellationToken = default);

    Task<PartyCacheDto?> GetPartyByCodeAsync(string partyCode, CancellationToken cancellationToken = default);
    Task<BranchCacheDto?> GetBranchByCodeAsync(string branchCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a <see cref="BranchCacheDto"/> by integer primary key.
    /// Falls back to a DB query against <paramref name="db"/> on cache miss, then caches the result.
    /// Replaces the repeated <c>db.Branches.Where(b =&gt; b.Id == ...).FirstOrDefaultAsync()</c> calls
    /// that previously fired once per widget × 2 years = ~20-30 SQL hits per dashboard load.
    /// </summary>
    Task<BranchCacheDto?> GetBranchByIdAsync(int branchId, IncentivePortal.Data.IncentiveDbContext db, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of Branch.Code values whose Branch.Consignee matches one of the supplied consignee names.
    /// Cached for 12 hours — consignee mapping is static master data.
    /// </summary>
    Task<List<string>> GetBranchCodesByConsigneeAsync(IReadOnlyList<string> consignees, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the anchor fiscal year for the dashboard (derived from the latest SsIncentive record).
    /// Cached for 5 minutes to avoid the heavy OrderBy scan on every widget request.
    /// </summary>
    Task<int?> GetDashboardAnchorYearAsync(string cacheKeySuffix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns (latestMonth, maxDay) for a given fiscal anchor year and branch scope key.
    /// Cached for 10 minutes.
    /// </summary>
    Task<(int LatestMonth, int? MaxDay)?> GetLatestMonthInfoAsync(int targetYear, string cacheKeySuffix, CancellationToken cancellationToken = default);

    Task InvalidatePartiesCacheAsync(CancellationToken cancellationToken = default);
    Task InvalidateBranchesCacheAsync(CancellationToken cancellationToken = default);
}
