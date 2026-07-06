using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;

namespace IncentivePortal.Services;

public interface IImportDuplicateCheckService
{
    Task<Dictionary<string, bool>> GetLockedSalesLookupAsync(List<int> years, List<int> months, CancellationToken cancellationToken);
    Task<Dictionary<string, bool>> GetApprovedLedgersLookupAsync(List<int> years, List<int> months, CancellationToken cancellationToken);
    bool CheckBatchDuplicates(string partyCode, int month, int year, string location, HashSet<string> processedKeys);
}

public sealed class ImportDuplicateCheckService(IncentiveDbContext db) : IImportDuplicateCheckService
{
    public async Task<Dictionary<string, bool>> GetLockedSalesLookupAsync(List<int> years, List<int> months, CancellationToken cancellationToken)
    {
        var existingSales = await db.SsIncentives
            .Where(x => years.Contains(x.Year) && months.Contains(x.Month))
            .Select(x => new { x.Year, x.Month, x.PartyCode, x.Status })
            .ToListAsync(cancellationToken);

        var lookup = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in existingSales)
        {
            var key = $"{s.Year}-{s.Month}-{s.PartyCode}";
            lookup[key] = s.Status == "Approved" || s.Status == "Posted";
        }
        return lookup;
    }

    public async Task<Dictionary<string, bool>> GetApprovedLedgersLookupAsync(List<int> years, List<int> months, CancellationToken cancellationToken)
    {
        var existingLedgers = await db.SsIncentives
            .Where(x => years.Contains(x.Year) && months.Contains(x.Month))
            .Select(x => new { x.Year, x.Month, x.PartyCode, x.PaymentStatus })
            .ToListAsync(cancellationToken);

        var lookup = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in existingLedgers)
        {
            var key = $"{l.Year}-{l.Month}-{l.PartyCode}";
            lookup[key] = l.PaymentStatus == "Paid" || l.PaymentStatus == "Approved";
        }
        return lookup;
    }

    public bool CheckBatchDuplicates(string partyCode, int month, int year, string location, HashSet<string> processedKeys)
    {
        var key = $"{year}-{month}-{partyCode}-{location}";
        if (processedKeys.Contains(key))
        {
            return true;
        }
        processedKeys.Add(key);
        return false;
    }
}
