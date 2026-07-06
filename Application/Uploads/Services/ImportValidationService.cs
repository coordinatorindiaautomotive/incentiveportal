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

public interface IImportValidationService
{
    Task<bool> IsPeriodLockedAsync(int year, int month, bool isHistorical, CancellationToken cancellationToken);
    
    ImportRowResult ValidateRow(
        int rowNumber,
        string partyCode,
        string partyName,
        string originalPartyCode,
        string location,
        string partCategoryCode,
        string partyType,
        int month,
        int year,
        decimal saleValue,
        string? documentNum,
        bool isDynamic,
        bool hasScheme,
        bool hasSlab,
        Dictionary<string, (string AllowedCategories, string AllowedPartyTypes)> branches,
        Dictionary<string, Party> partiesInSheet,
        Dictionary<string, bool> existingSalesLookup,
        Dictionary<string, bool> existingLedgersLookup,
        HashSet<string> processedKeys
    );
}

public sealed class ImportValidationService(IncentiveDbContext db) : IImportValidationService
{
    public async Task<bool> IsPeriodLockedAsync(int year, int month, bool isHistorical, CancellationToken cancellationToken)
    {
        var isLocked = await db.MonthLocks
            .AnyAsync(x => x.LockYear == year && (x.LockMonth == month || x.LockMonth == 0) && x.IsLocked, cancellationToken);
        return isLocked && !isHistorical;
    }

    public ImportRowResult ValidateRow(
        int rowNumber,
        string partyCode,
        string partyName,
        string originalPartyCode,
        string location,
        string partCategoryCode,
        string partyType,
        int month,
        int year,
        decimal saleValue,
        string? documentNum,
        bool isDynamic,
        bool hasScheme,
        bool hasSlab,
        Dictionary<string, (string AllowedCategories, string AllowedPartyTypes)> branches,
        Dictionary<string, Party> partiesInSheet,
        Dictionary<string, bool> existingSalesLookup,
        Dictionary<string, bool> existingLedgersLookup,
        HashSet<string> processedKeys
    )
    {
        var result = new ImportRowResult(
            rowNumber,
            partyCode,
            month,
            year,
            saleValue,
            "Valid",
            null
        );

        // 1. Missing fields check
        if (string.IsNullOrWhiteSpace(partyCode))
        {
            return result with { ValidationStatus = "MissingField", ErrorMessage = "Party code is required." };
        }
        if (string.IsNullOrWhiteSpace(partyName))
        {
            return result with { ValidationStatus = "MissingField", ErrorMessage = "Party name is required." };
        }
        if (string.IsNullOrWhiteSpace(location))
        {
            return result with { ValidationStatus = "MissingField", ErrorMessage = "Location is required." };
        }

        // 2. Party Code existence in Master (IsDeleted = false)
        var targetPartyCode = !string.IsNullOrEmpty(originalPartyCode) ? originalPartyCode : partyCode;

        if (isDynamic)
        {
            // For raw Sales uploads every row is an independent transaction (invoice line).
            // A dealer can legitimately have many rows in the same month (different documents,
            // categories, amounts). The only period-level gate is MonthLock, which is already
            // enforced at preview step 4 — so NO batch-duplicate check here.

            // Silent "overwrite" marker so the UI can show "X rows will be replaced" info.
            var lookupKey1 = $"{year}-{month}-{targetPartyCode}";
            if (existingSalesLookup.ContainsKey(lookupKey1))
            {
                return result with { ValidationStatus = "Duplicate", ErrorMessage = null };
            }

            return result;
        }

        if (!partiesInSheet.TryGetValue(targetPartyCode, out var party) || party.IsDeleted)
        {
            return result with { ValidationStatus = "InvalidParty", ErrorMessage = $"PartyCode '{targetPartyCode}' not found in master. Row will be skipped." };
        }

        // 3. Scheme check
        if (party.DealerType != "Fixed Incentive" && !hasScheme)
        {
            return result with { ValidationStatus = "MissingField", ErrorMessage = "No incentive scheme available for the import period." };
        }

        // 4. Slab check
        if (party.DealerType != "Fixed Incentive" && !hasSlab && saleValue >= 0)
        {
            return result with { ValidationStatus = "MissingField", ErrorMessage = "No slab found for the uploaded sales value." };
        }

        // 5. Branch Routing allowed check (Allowed categories / Party types)
        if (branches.TryGetValue(location, out var branchRule))
        {
            if (!string.IsNullOrEmpty(partCategoryCode))
            {
                var allowedCats = branchRule.AllowedCategories
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (allowedCats.Length > 0 && !allowedCats.Contains(partCategoryCode, StringComparer.OrdinalIgnoreCase))
                {
                    return result with { ValidationStatus = "InvalidParty", ErrorMessage = $"Part category '{partCategoryCode}' is whitelisted out of Branch '{location}' rules." };
                }
            }

            if (!string.IsNullOrEmpty(partyType))
            {
                var allowedTypes = branchRule.AllowedPartyTypes
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var pt = partyType.Trim();
                if (pt.Equals("Fixed Incentive", StringComparison.OrdinalIgnoreCase) ||
                    pt.Equals("Slab-Based", StringComparison.OrdinalIgnoreCase) ||
                    pt.Equals("Dealer", StringComparison.OrdinalIgnoreCase))
                {
                    pt = "INDEPENDENT WORKSHOP";
                }

                if (allowedTypes.Length > 0 && !allowedTypes.Contains(pt, StringComparer.OrdinalIgnoreCase))
                {
                    return result with { ValidationStatus = "InvalidParty", ErrorMessage = $"Party Type '{partyType}' is whitelisted out of Branch '{location}' rules." };
                }
            }
        }

        // 6. Locked row check
        var lookupKey = $"{year}-{month}-{targetPartyCode}";
        bool isCurrentMonth = year == DateTime.Today.Year && month == DateTime.Today.Month;
        if (!isCurrentMonth && existingSalesLookup.TryGetValue(lookupKey, out var isLocked) && isLocked)
        {
            return result with { ValidationStatus = "Duplicate", ErrorMessage = "Month-party sales row is locked and cannot be overwritten." };
        }

        // 7. Approved/Paid check
        if (!isCurrentMonth && existingLedgersLookup.TryGetValue(lookupKey, out var isPaidOrApproved) && isPaidOrApproved)
        {
            return result with { ValidationStatus = "Duplicate", ErrorMessage = "Duplicate import detected: Incentive transaction already approved or paid for this period." };
        }

        // 8. Batch duplicate check — include documentNum + partCategoryCode so multiple
        //    invoices for the same dealer in the same month are NOT falsely flagged.
        var docPart = $"{documentNum?.Trim()}-{partCategoryCode?.Trim()}";
        var batchKey = $"{year}-{month}-{partyCode}-{location}-{docPart}";
        if (processedKeys.Contains(batchKey))
        {
            return result with { ValidationStatus = "Duplicate", ErrorMessage = "Duplicate row detected in the uploaded batch." };
        }
        processedKeys.Add(batchKey);

        // 9. Overwrite duplicate check (if exists in db but not locked/paid)
        if (existingSalesLookup.ContainsKey(lookupKey))
        {
            return result with { ValidationStatus = "Duplicate", ErrorMessage = null };
        }

        return result;
    }
}
