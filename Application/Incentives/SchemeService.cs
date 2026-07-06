using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

/// <summary>
/// Service interface for configuring and managing incentive schemes and slabs.
/// </summary>
public interface ISchemeService
{
    /// <summary>
    /// Creates a new incentive scheme with ordered, non-overlapping slab boundaries.
    /// </summary>
    Task<IncentiveScheme> CreateAsync(SchemeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies scheme configuration from a previous period to reduce manual entry errors.
    /// </summary>
    Task<IncentiveScheme> CopyPreviousAsync(int month, int year, CancellationToken cancellationToken = default);
}

public sealed class SchemeService(IncentiveDbContext db) : ISchemeService
{
    public async Task<IncentiveScheme> CreateAsync(SchemeRequest request, CancellationToken cancellationToken = default)
    {
        ValidateSlabs(request.Details);
        var existingVersion = await db.IncentiveSchemes
            .Where(x => x.SchemeMonth == request.SchemeMonth && x.SchemeYear == request.SchemeYear)
            .MaxAsync(x => (int?)x.Version, cancellationToken) ?? 0;
        var scheme = new IncentiveScheme
        {
            Name = request.Name,
            SchemeMonth = request.SchemeMonth,
            SchemeYear = request.SchemeYear,
            Version = existingVersion + 1,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Details = request.Details.Select(x => new IncentiveSchemeDetail
            {
                MinAchievementPercent = x.MinAchievementPercent,
                MaxAchievementPercent = x.MaxAchievementPercent,
                FixedAmount = x.FixedAmount,
                Percentage = x.Percentage,
                RuleName = x.RuleName
            }).ToList()
        };
        db.IncentiveSchemes.Add(scheme);
        await db.SaveChangesAsync(cancellationToken);
        return scheme;
    }

    public async Task<IncentiveScheme> CopyPreviousAsync(int month, int year, CancellationToken cancellationToken = default)
    {
        var previous = await db.IncentiveSchemes.Include(x => x.Details)
            .OrderByDescending(x => x.SchemeYear).ThenByDescending(x => x.SchemeMonth).ThenByDescending(x => x.Version)
            .FirstAsync(x => x.SchemeYear < year || x.SchemeMonth < month, cancellationToken);
        return await CreateAsync(new SchemeRequest($"{previous.Name} Copy", month, year, new DateTime(year, month, 1), new DateTime(year, month, DateTime.DaysInMonth(year, month)),
            previous.Details.Select(x => new SchemeDetailRequest(x.MinAchievementPercent, x.MaxAchievementPercent, x.FixedAmount, x.Percentage, x.RuleName)).ToList()), cancellationToken);
    }

    private static void ValidateSlabs(IEnumerable<SchemeDetailRequest> details)
    {
        var slabs = details.OrderBy(x => x.MinAchievementPercent).ToList();
        for (var i = 0; i < slabs.Count; i++)
        {
            if (slabs[i].MinAchievementPercent > slabs[i].MaxAchievementPercent)
                throw new InvalidOperationException("Slab minimum cannot be greater than maximum.");
            if (i > 0 && slabs[i].MinAchievementPercent <= slabs[i - 1].MaxAchievementPercent)
                throw new InvalidOperationException("Overlapping slabs are not allowed.");
        }
    }
}
