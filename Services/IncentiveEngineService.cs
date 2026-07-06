using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

public interface IIncentiveEngineService
{
    Task<decimal> CalculateGrossIncentiveAsync(SsIncentive sale, DateTime targetDate, Dictionary<string, decimal> additionalContext);
}

public sealed class IncentiveEngineService(
    IRuleEngineService ruleEngine,
    IFormulaEngineService formulaEngine,
    IncentiveDbContext db
) : IIncentiveEngineService
{
    public async Task<decimal> CalculateGrossIncentiveAsync(SsIncentive sale, DateTime targetDate, Dictionary<string, decimal> additionalContext)
    {
        // 1. Get active rule versions
        var activeRuleVersions = await ruleEngine.GetActiveRulesAsync(targetDate);
        
        // Filter rules that are of type "Scheme" or "Bonus"
        var schemeRules = activeRuleVersions.Where(v => v.RuleMaster.RuleType.Equals("Scheme", StringComparison.OrdinalIgnoreCase) || 
                                                        v.RuleMaster.RuleType.Equals("Bonus", StringComparison.OrdinalIgnoreCase)).ToList();

        // ── Phase 5: Resolve enriched parameter context ─────────────────
        var party = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.PartyCode == sale.PartyCode);
        var dealerCategory = party?.DealerType ?? string.Empty;
        var productGroup = sale.PartCategoryCode ?? string.Empty;
        var branchCode = sale.SourceLocation ?? string.Empty;

        // Resolve Branch region
        var branch = !string.IsNullOrEmpty(branchCode)
            ? await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Code == branchCode)
            : null;
        var region = branch?.Region ?? string.Empty;

        // Resolve growth analytics for dealer
        decimal growthPercent = 0m;
        if (party != null)
        {
            var growth = await db.DealerGrowthAnalytics.AsNoTracking()
                .Where(g => g.PartyId == party.Id && g.Year == sale.Year && g.Month == sale.Month)
                .FirstOrDefaultAsync();
            if (growth != null)
                growthPercent = growth.SalesGrowthYoY * 100m;
        }

        // Resolve collection percent: (Approved CashIn / SaleValue) * 100
        decimal collectionPercent = 0m;
        if (party != null && sale.SaleValue != 0m)
        {
            var approvedCashIn = await db.CashInTransactions.AsNoTracking()
                .Where(c => c.DealerCode == party.PartyCode
                    && c.TransactionDate.Year == sale.Year
                    && c.TransactionDate.Month == sale.Month
                    && c.Status == "Approved")
                .SumAsync(c => c.Amount);
            collectionPercent = Math.Round(approvedCashIn / sale.SaleValue * 100m, 2);
        }

        // Outstanding percent: (Outstanding / SaleValue) * 100
        decimal outstandingPercent = sale.SaleValue != 0m
            ? Math.Round(sale.Outstanding / sale.SaleValue * 100m, 2)
            : 0m;

        // Target achievement mirrors AchievementPercent
        decimal targetAchievement = sale.AchievementPercent;

        // Resolve dealer sub type from Raw table if possible
        var firstRaw = await db.Raws.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ConsPartyCode == sale.PartyCode && r.MonthNumber == sale.Month && r.YearNumber == sale.Year);
        var dealerSubType = firstRaw?.DealerSubType ?? string.Empty;

        // 2. Build context for rule evaluation
        var context = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            { "Sales", sale.SaleValue },
            { "Discount", sale.OnBillDiscount },
            { "Outstanding", sale.Outstanding },
            { "AchievementPercent", sale.AchievementPercent },
            { "Branch", branchCode },
            { "PartCategoryCode", productGroup },
            { "DealerType", dealerCategory },
            { "DealerSubType", dealerSubType },
            // Phase 5 enriched context
            { "DealerCategory", dealerCategory },
            { "ProductGroup", productGroup },
            { "Region", region },
            { "State", region },
            { "CollectionPercent", collectionPercent },
            { "OutstandingPercent", outstandingPercent },
            { "GrowthPercent", growthPercent },
            { "TargetAchievement", targetAchievement }
        };

        // Add additional context (e.g. Growth, CollectionPercent overrides)
        foreach (var kvp in additionalContext)
        {
            context[kvp.Key] = kvp.Value;
        }

        // 3. Find first rule version where all conditions match
        RuleVersion? matchedRuleVersion = null;
        foreach (var ruleVer in schemeRules)
        {
            if (ruleEngine.EvaluateConditions(ruleVer, context))
            {
                matchedRuleVersion = ruleVer;
                break;
            }
        }

        if (matchedRuleVersion == null)
        {
            return 0m; // No rule matched
        }

        // 4. Build variable dictionary for formula evaluation
        var formulaVars = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "Sales", sale.SaleValue },
            { "Discount", sale.OnBillDiscount },
            { "Outstanding", sale.Outstanding },
            { "AchievementPercent", sale.AchievementPercent },
            // Phase 5 enriched variables
            { "CollectionPercent", collectionPercent },
            { "OutstandingPercent", outstandingPercent },
            { "GrowthPercent", growthPercent },
            { "TargetAchievement", targetAchievement }
        };

        foreach (var kvp in additionalContext)
        {
            formulaVars[kvp.Key] = kvp.Value;
        }

        // 5. Evaluate the formula expression linked to the matched rule version
        decimal grossIncentive = formulaEngine.Evaluate(matchedRuleVersion.FormulaExpression, formulaVars);
        return Math.Round(Math.Max(0, grossIncentive), 0, MidpointRounding.AwayFromZero);
    }
}
