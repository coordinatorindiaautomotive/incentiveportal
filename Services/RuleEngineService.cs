using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Services;

public interface IRuleEngineService
{
    Task<List<RuleVersion>> GetActiveRulesAsync(DateTime targetDate);
    bool EvaluateConditions(RuleVersion ruleVersion, Dictionary<string, object> context);
}

public sealed class RuleEngineService(IncentiveDbContext db) : IRuleEngineService
{
    public async Task<List<RuleVersion>> GetActiveRulesAsync(DateTime targetDate)
    {
        return await db.RuleVersions
            .Include(v => v.RuleMaster)
            .Include(v => v.Conditions)
            .Where(v => v.IsActive && !v.IsDeleted && v.EffectiveFrom <= targetDate && v.EffectiveTo >= targetDate)
            .Where(v => v.RuleMaster.IsActive && !v.RuleMaster.IsDeleted)
            .ToListAsync();
    }

    public bool EvaluateConditions(RuleVersion ruleVersion, Dictionary<string, object> context)
    {
        if (ruleVersion.Conditions == null || !ruleVersion.Conditions.Any())
            return true;

        var sortedConditions = ruleVersion.Conditions.OrderBy(c => c.SortOrder).ToList();
        
        // Logical evaluation accumulator: start with the outcome of the first condition
        bool result = EvaluateCondition(sortedConditions[0], context);

        for (int i = 1; i < sortedConditions.Count; i++)
        {
            var cond = sortedConditions[i];
            bool conditionValue = EvaluateCondition(cond, context);
            
            // Check logical operator of the PREVIOUS step to join with current step.
            // E.g., if condition[i-1] says AND, we do result = result && conditionValue.
            var logOp = sortedConditions[i - 1].LogicalOperator;
            if (logOp.Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                result = result || conditionValue;
            }
            else
            {
                result = result && conditionValue;
            }
        }

        return result;
    }

    private bool EvaluateCondition(RuleCondition condition, Dictionary<string, object> context)
    {
        if (!context.TryGetValue(condition.FieldName, out var rawVal) || rawVal == null)
            return false;

        var actualStr = rawVal.ToString()?.Trim() ?? string.Empty;
        var op = condition.Operator.Trim().ToUpperInvariant();
        var targetExpr = condition.ValueExpression.Trim();

        // 1. IN Operator (comma separated strings, e.g. "'AA','M'" or "HO,ALW")
        if (op == "IN")
        {
            var items = targetExpr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                  .Select(x => x.Trim('\'', '"'))
                                  .ToList();
            return items.Contains(actualStr, StringComparer.OrdinalIgnoreCase);
        }

        // 2. Numeric comparison
        if (decimal.TryParse(actualStr, out var actualDec) && decimal.TryParse(targetExpr, out var targetDec))
        {
            return op switch
            {
                ">=" => actualDec >= targetDec,
                "<=" => actualDec <= targetDec,
                ">" => actualDec > targetDec,
                "<" => actualDec < targetDec,
                "==" or "=" => actualDec == targetDec,
                "!=" or "<>" => actualDec != targetDec,
                _ => false
            };
        }

        // 3. String comparison fallback
        return op switch
        {
            "==" or "=" => actualStr.Equals(targetExpr.Trim('\'', '"'), StringComparison.OrdinalIgnoreCase),
            "!=" or "<>" => !actualStr.Equals(targetExpr.Trim('\'', '"'), StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
