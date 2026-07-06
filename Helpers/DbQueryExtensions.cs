using System.Linq;
using IncentivePortal.Data;
using IncentivePortal.Models;

namespace IncentivePortal.Helpers;

public static class DbQueryExtensions
{
    // Helper to query performances since EF Core maps to the plural name
    public static IQueryable<DealerMonthlyPerformance> DealerDealerMonthlyPerformances(this IncentiveDbContext db)
        => db.DealerMonthlyPerformances;
}
