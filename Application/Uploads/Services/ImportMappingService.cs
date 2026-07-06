using System;
using System.Collections.Generic;
using System.Data;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

public interface IImportMappingService
{
    Dictionary<string, int> BuildDynamicColumnMap(DataTable dt, IReadOnlyList<ColumnMappingRule> dbMappings);
    Dictionary<string, int> BuildColumnMap(DataTable dt);
}

public sealed class ImportMappingService : IImportMappingService
{
    private static readonly Dictionary<string, string> _headerAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Incentive "] = "Incentive",
        ["Gross Incentive"] = "Incentive",
        ["Incentive Amount"] = "Incentive",
        ["Slab Applied"] = "Slab",
        ["Slab %"] = "Slab",
        ["Slab Rate"] = "Slab",
        ["Achievement %"] = "Achievement Percent",
        ["AchievementPercent"] = "Achievement Percent",
        ["Loc"] = "Location",
        ["Branch"] = "Location",
        ["Net Retail (Sales)"] = "Net Retail Selling",
        ["Net Retail"] = "Net Retail Selling",
        ["Party Code"] = "Cons Party Code",
        ["Party Name"] = "Cons Party Name"
    };

    private static readonly Dictionary<string, string> _fieldToKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Location"] = "Location",
        ["Loc"] = "Location",
        ["Branch"] = "Location",
        ["PartCategoryCode"] = "Part Category Code",
        ["Part Category Code"] = "Part Category Code",
        ["DealerType"] = "Party Type",
        ["PartyType"] = "Party Type",
        ["Party Type"] = "Party Type",
        ["DealerSubType"] = "Dealer Sub-Type",
        ["Dealer Sub-Type"] = "Dealer Sub-Type",
        ["DealerCode"] = "Dealer Code",
        ["Dealer Code"] = "Dealer Code",
        ["Consignee"] = "Consignee",
        ["FiscalYear"] = "Fiscal Year",
        ["Fiscal Year"] = "Fiscal Year",
        ["Quarter"] = "Quarter",
        ["DocumentNum"] = "Document Num",
        ["Document Num"] = "Document Num",
        ["Remarks"] = "Remarks",
        ["NetRetailDdl"] = "Net Retail DDL",
        ["Net Retail DDL"] = "Net Retail DDL",
        ["Net Retail Ddl"] = "Net Retail DDL",
        ["Day"] = "Day",
        ["MonthYear"] = "Month Year",
        ["Month Year"] = "Month Year",
        ["Month"] = "Month",
        ["Year"] = "Year",
        ["PartyCode"] = "Cons Party Code",
        ["ConsPartyCode"] = "Cons Party Code",
        ["Cons Party Code"] = "Cons Party Code",
        ["PartyName"] = "Cons Party Name",
        ["ConsPartyName"] = "Cons Party Name",
        ["Cons Party Name"] = "Cons Party Name",
        ["NetRetailSelling"] = "Net Retail Selling",
        ["SaleValue"] = "Net Retail Selling",
        ["Net Retail Selling"] = "Net Retail Selling",
        ["DiscountAmount"] = "Discount Amount",
        ["Discount"] = "Discount Amount",
        ["Discount Amount"] = "Discount Amount",
        ["Slab"] = "Slab",
        ["Incentive"] = "Incentive",
        ["AchievementPercent"] = "Achievement Percent",
        ["Achievement Percent"] = "Achievement Percent"
    };

    public Dictionary<string, int> BuildColumnMap(DataTable dt)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < dt.Columns.Count; c++)
        {
            var raw = (dt.Columns[c].ColumnName ?? string.Empty).Trim();
            var key = _headerAliases.TryGetValue(raw, out var alias) ? alias : raw;
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = c;
        }
        return map;
    }

    public Dictionary<string, int> BuildDynamicColumnMap(DataTable dt, IReadOnlyList<ColumnMappingRule> dbMappings)
    {
        var customHeaderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in dbMappings)
        {
            if (!string.IsNullOrWhiteSpace(m.ExcelHeader) && !string.IsNullOrWhiteSpace(m.PortalField))
            {
                customHeaderMap[m.ExcelHeader.Trim()] = m.PortalField.Trim();
            }
        }

        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < dt.Columns.Count; c++)
        {
            var raw = (dt.Columns[c].ColumnName ?? string.Empty).Trim();
            string? mappedField = null;
            if (customHeaderMap.TryGetValue(raw, out var field))
            {
                mappedField = field;
            }
            else if (_headerAliases.TryGetValue(raw, out var alias))
            {
                mappedField = alias;
            }
            else
            {
                mappedField = raw;
            }

            var key = _fieldToKey.TryGetValue(mappedField, out var k) ? k : mappedField;
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = c;
        }
        return map;
    }
}
