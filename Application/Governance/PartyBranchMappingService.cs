using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

/// <summary>
/// Computes and caches the Primary Branch (dominant purchase location) for each party
/// by analyzing the Raw sales table. Raw data is NEVER modified by this service.
/// The primary branch is the Loc code from which a party has made the maximum
/// number of purchases (transaction count).
/// </summary>
public interface IPartyBranchMappingService
{
    /// <summary>
    /// Rebuilds the PartyPrimaryBranch cache from the Raw table.
    /// Safe to call at any time — idempotent and non-destructive.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a dictionary of PartyCode to PrimaryBranchCode for fast lookup.
    /// Falls back to empty dict if cache is not yet populated.
    /// </summary>
    Task<System.Collections.Generic.Dictionary<string, string>> GetPrimaryBranchMapAsync(
        CancellationToken cancellationToken = default);
}

public sealed class PartyBranchMappingService(IncentiveDbContext db) : IPartyBranchMappingService
{
    /// <inheritdoc/>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Step 1: Aggregate Raw table - group by (PartyCode, Loc)
        // Uses OriginalCode when available (governor-resolved), falls back to ConsPartyCode.
        // This runs as a single SQL GROUP BY - Raw table is only READ, never modified.
        var grouped = await db.Raws
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.ImportLogId > 0 && r.Loc != null && r.Loc != "")
            .GroupBy(r => new
            {
                PartyCode = (r.OriginalCode != null && r.OriginalCode != "") ? r.OriginalCode : r.ConsPartyCode,
                Loc = r.Loc
            })
            .Select(g => new
            {
                g.Key.PartyCode,
                g.Key.Loc,
                TxCount = g.Count(),
                TotalSales = g.Sum(r => r.NetRetailSelling)
            })
            .Where(x => x.PartyCode != null && x.PartyCode != "")
            .ToListAsync(cancellationToken);

        // Step 2: For each party, find the Loc with maximum transactions
        var primaryBranchPerParty = grouped
            .GroupBy(x => x.PartyCode!, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var dominant = g.OrderByDescending(x => x.TxCount)
                                .ThenByDescending(x => x.TotalSales)
                                .First();
                return new
                {
                    PartyCode = g.Key,
                    PrimaryBranchCode = dominant.Loc!,
                    TransactionCount = dominant.TxCount,
                    TotalSales = dominant.TotalSales
                };
            })
            .ToList();

        if (primaryBranchPerParty.Count == 0) return;

        // Step 3: Load existing cache rows for upsert
        var existingRows = await db.PartyPrimaryBranches
            .ToDictionaryAsync(x => x.PartyCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var now = DateTime.UtcNow;
        db.DisableAuditLogs = true;

        try
        {
            foreach (var item in primaryBranchPerParty)
            {
                if (existingRows.TryGetValue(item.PartyCode, out var existing))
                {
                    // Update only if something changed
                    if (existing.PrimaryBranchCode != item.PrimaryBranchCode
                        || existing.TransactionCount != item.TransactionCount
                        || existing.TotalSales != item.TotalSales)
                    {
                        existing.PrimaryBranchCode = item.PrimaryBranchCode;
                        existing.TransactionCount = item.TransactionCount;
                        existing.TotalSales = item.TotalSales;
                        existing.LastRefreshedAt = now;
                        existing.UpdatedAt = now;
                    }
                }
                else
                {
                    db.PartyPrimaryBranches.Add(new PartyPrimaryBranch
                    {
                        PartyCode = item.PartyCode,
                        PrimaryBranchCode = item.PrimaryBranchCode,
                        TransactionCount = item.TransactionCount,
                        TotalSales = item.TotalSales,
                        LastRefreshedAt = now,
                        CreatedAt = now
                    });
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            db.DisableAuditLogs = false;
        }
    }

    /// <inheritdoc/>
    public async Task<System.Collections.Generic.Dictionary<string, string>> GetPrimaryBranchMapAsync(
        CancellationToken cancellationToken = default)
    {
        return await db.PartyPrimaryBranches
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .ToDictionaryAsync(
                x => x.PartyCode,
                x => x.PrimaryBranchCode,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
    }
}
