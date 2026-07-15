using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.Helpers;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

/// <summary>
/// Service interface for managing dealer (party) records with branch-level access control.
/// </summary>
public interface IPartyService
{
    /// <summary>
    /// Returns a queryable collection of parties, filtered by the current user's branch rights.
    /// </summary>
    IQueryable<Party> QueryForCurrentUser();

    /// <summary>
    /// Creates or updates a party record, verifying branch authorization.
    /// </summary>
    Task<Party> CreateAsync(Party party, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalculates the base locations for all parties based on historical sales.
    /// </summary>
    Task CalculateBaseLocationsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Sealed implementation of <see cref="IPartyService"/> with branch isolation logic.
/// </summary>
public sealed class PartyService(IncentiveDbContext db, ICurrentUser currentUser) : IPartyService
{
    public IQueryable<Party> QueryForCurrentUser()
    {
        var query = db.Parties.Include(x => x.Branch).AsQueryable();
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
            query = query.Where(x => x.BranchId == currentUser.BranchId);
        return query;
    }

    public async Task<Party> CreateAsync(Party party, CancellationToken cancellationToken = default)
    {
        if (!currentUser.CanAccessBranch(party.BranchId))
            throw new UnauthorizedAccessException("Branch access denied.");
        
        party.GST = party.GST ?? string.Empty;
        party.Mobile = party.Mobile ?? string.Empty;
        party.Address = party.Address ?? string.Empty;
        party.OriginalPartyCode = party.OriginalPartyCode ?? string.Empty;
        
        var existing = await db.Parties.FirstOrDefaultAsync(x => x.PartyCode == party.PartyCode, cancellationToken);
        if (existing != null)
        {
            existing.PartyName = party.PartyName;
            existing.GST = party.GST ?? string.Empty;
            existing.Mobile = party.Mobile ?? string.Empty;
            existing.BranchId = party.BranchId;
            existing.Address = party.Address ?? string.Empty;
            existing.DealerType = party.DealerType;
            existing.FixedIncentivePercent = party.FixedIncentivePercent;
            existing.Status = party.Status;
            existing.OriginalPartyCode = party.OriginalPartyCode ?? string.Empty;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = currentUser.UserName ?? "system";
            
            await db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        db.Parties.Add(party);
        await db.SaveChangesAsync(cancellationToken);
        return party;
    }

    public async Task CalculateBaseLocationsAsync(CancellationToken cancellationToken = default)
    {
        // 1. Calculate sales sum per party and branch
        var salesData = await db.SsIncentives
            .Where(x => !x.IsDeleted && !string.IsNullOrEmpty(x.PartyCode) && !string.IsNullOrEmpty(x.SourceLocation))
            .GroupBy(x => new { x.PartyCode, x.SourceLocation })
            .Select(g => new
            {
                g.Key.PartyCode,
                g.Key.SourceLocation,
                TotalSales = g.Sum(x => x.SaleValue)
            })
            .ToListAsync(cancellationToken);

        // 2. Find the highest sales location per party
        var maxSalesLocations = salesData
            .GroupBy(x => x.PartyCode)
            .Select(g => g.OrderByDescending(x => x.TotalSales).First())
            .ToList();

        // 3. Get all branches mapping code -> id
        var branchDict = await db.Branches
            .Where(x => !x.IsDeleted)
            .ToDictionaryAsync(x => x.Code, x => x.Id, cancellationToken);

        // 4. Update the parties
        var parties = await db.Parties.Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
        foreach (var p in parties)
        {
            var bestSales = maxSalesLocations.FirstOrDefault(x => x.PartyCode == p.PartyCode);
            if (bestSales != null && branchDict.TryGetValue(bestSales.SourceLocation, out int autoBranchId))
            {
                p.AutoBaseBranchId = autoBranchId;
                
                // Effective mapping respects manual override
                if (!p.IsManuallyMapped)
                {
                    p.BranchId = autoBranchId;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
