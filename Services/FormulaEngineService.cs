using System;
using System.Collections.Generic;

namespace IncentivePortal.Services;

public interface IFormulaEngineService
{
    decimal Evaluate(string formula, Dictionary<string, decimal> variables);
    bool ValidateFormula(string formula, out string errorMessage);
}

public sealed class FormulaEngineService : IFormulaEngineService
{
    public decimal Evaluate(string formula, Dictionary<string, decimal> variables)
    {
        return Helpers.FormulaParser.ParseAndEvaluate(formula, variables);
    }

    public bool ValidateFormula(string formula, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(formula))
        {
            errorMessage = "Formula expression cannot be empty.";
            return false;
        }

        // 1. Unbalanced Parentheses check
        int openParenthesisCount = 0;
        for (int i = 0; i < formula.Length; i++)
        {
            if (formula[i] == '(')
                openParenthesisCount++;
            else if (formula[i] == ')')
            {
                openParenthesisCount--;
                if (openParenthesisCount < 0)
                {
                    errorMessage = $"Unbalanced parentheses: Found closing parenthesis ')' at position {i + 1} without a matching opening parenthesis.";
                    return false;
                }
            }
        }
        if (openParenthesisCount != 0)
        {
            errorMessage = $"Unbalanced parentheses: There are {openParenthesisCount} unclosed opening parenthesis '('.";
            return false;
        }

        // 2. Unknown variables check
        try
        {
            var allowedVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Sales", "Growth", "Outstanding", "AchievementPercent", "CollectionPercent", "Discount",
                "DealerCategory", "ProductGroup", "Branch", "Region", "State",
                "OutstandingPercent", "GrowthPercent", "TargetAchievement"
            };
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AND", "OR", "IF", "NOT", "ROUND", "ABS", "MIN", "MAX"
            };

            var i = 0;
            while (i < formula.Length)
            {
                var c = formula[i];
                if (char.IsWhiteSpace(c) || char.IsDigit(c) || c == '.' || c == '(' || c == ')' || c == ',' || 
                    c == '+' || c == '-' || c == '*' || c == '/' || c == '>' || c == '<' || c == '=' || c == '!')
                {
                    i++;
                    continue;
                }

                if (char.IsLetter(c))
                {
                    var sb = new System.Text.StringBuilder();
                    while (i < formula.Length && (char.IsLetterOrDigit(formula[i]) || formula[i] == '_'))
                    {
                        sb.Append(formula[i]);
                        i++;
                    }
                    var tokenVal = sb.ToString();
                    if (!keywords.Contains(tokenVal) && !allowedVars.Contains(tokenVal))
                    {
                        errorMessage = $"Unknown variable or function: '{tokenVal}'. Allowed variables: Sales, Growth, Outstanding, AchievementPercent, CollectionPercent, Discount, DealerCategory, ProductGroup, Branch, Region, State, OutstandingPercent, GrowthPercent, TargetAchievement. Allowed functions: IF, AND, OR, NOT, ROUND, ABS, MIN, MAX.";
                        return false;
                    }
                }
                else
                {
                    errorMessage = $"Unexpected character in formula: '{c}'";
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Syntax error during tokenization: {ex.Message}";
            return false;
        }

        // 3. Fallback parser evaluation check
        try
        {
            var mockVars = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                { "Sales", 100000m },
                { "Growth", 25m },
                { "Outstanding", 15000m },
                { "AchievementPercent", 105m },
                { "CollectionPercent", 98m },
                { "Discount", 500m },
                { "DealerCategory", 1m },
                { "ProductGroup", 1m },
                { "Branch", 1m },
                { "Region", 1m },
                { "State", 1m },
                { "OutstandingPercent", 15m },
                { "GrowthPercent", 25m },
                { "TargetAchievement", 105m }
            };
            Evaluate(formula, mockVars);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to parse/evaluate formula structure: {ex.Message}";
            return false;
        }
    }
}
