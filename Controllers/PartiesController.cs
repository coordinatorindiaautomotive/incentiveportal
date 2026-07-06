using IncentivePortal.Data;
using IncentivePortal.Models;
using IncentivePortal.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class PartiesController(IPartyService partyService, IncentiveDbContext db) : Controller
{
    // =========================================================
    // PARTIES LIST / REGISTRY
    // =========================================================
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.Branches = await db.Branches.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var query = partyService.QueryForCurrentUser()
            .Where(x => x.DealerType == "Fixed Incentive");
        return View(await query.OrderBy(x => x.PartyName).Take(500).ToListAsync(cancellationToken));
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> Save(Party party, CancellationToken cancellationToken)
    {
        party.GST = party.GST ?? string.Empty;
        party.Mobile = party.Mobile ?? string.Empty;
        party.Address = party.Address ?? string.Empty;
        party.OriginalPartyCode = party.OriginalPartyCode ?? string.Empty;
        party.DealerType = party.DealerType ?? "Fixed Incentive";
        party.Status = party.Status ?? "Active";

        if (party.Id == 0)
            await partyService.CreateAsync(party, cancellationToken);
        else
        {
            db.Parties.Update(party);
            await db.SaveChangesAsync(cancellationToken);
        }
        return Json(new { ok = true, message = "Party saved." });
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await db.Parties.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing == null) return NotFound(new { message = "Party not found." });

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = User.Identity?.Name ?? "system";
        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "Party deleted successfully." });
    }

    // =========================================================
    // ALTERNATE MAPPINGS MASTER
    // =========================================================
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> AlternateMappings(CancellationToken cancellationToken)
    {
        ViewBag.Branches = await db.Branches.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var mappings = await db.Parties
            .Include(x => x.Branch)
            .Where(x => !string.IsNullOrEmpty(x.OriginalPartyCode) && !x.IsDeleted)
            .OrderBy(x => x.PartyCode)
            .ToListAsync(cancellationToken);
        return View(mappings);
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> SaveAlternateMapping(Party party, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(party.PartyCode) || string.IsNullOrWhiteSpace(party.OriginalPartyCode))
            return BadRequest(new { message = "Both Alternate Code and Original Target Code are required." });

        var existing = await db.Parties.FirstOrDefaultAsync(x => x.PartyCode == party.PartyCode, cancellationToken);
        if (existing == null)
        {
            party.PartyName = !string.IsNullOrWhiteSpace(party.PartyName) ? party.PartyName : $"Alternate Party {party.PartyCode}";
            party.DealerType = "Slab-Based";
            party.Status = "Active";
            db.Parties.Add(party);
        }
        else
        {
            existing.OriginalPartyCode = party.OriginalPartyCode.Trim();
            if (!string.IsNullOrWhiteSpace(party.PartyName)) existing.PartyName = party.PartyName.Trim();
            existing.BranchId = party.BranchId;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name ?? "system";
        }

        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "Alternate mapping saved successfully." });
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> DeleteAlternateMapping(int id, CancellationToken cancellationToken)
    {
        var existing = await db.Parties.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing == null)
            return NotFound(new { message = "Mapping not found." });

        existing.OriginalPartyCode = string.Empty;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = User.Identity?.Name ?? "system";
        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "Alternate mapping removed." });
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> ImportAlternateMappingsBatch([FromBody] List<AlternateMappingImportItem> items, CancellationToken cancellationToken)
    {
        if (items == null || items.Count == 0)
            return BadRequest(new { message = "No records provided." });

        var branches = await db.Branches.ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var defaultBranchId = branches.Values.FirstOrDefault();

        var altCodes = items
            .Where(item => !string.IsNullOrWhiteSpace(item.AlternateCode) && !string.IsNullOrWhiteSpace(item.OriginalCode))
            .Select(item => item.AlternateCode.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Query all existing parties matching the incoming AlternateCodes, including soft-deleted ones, to avoid duplicate key exceptions.
        var existingParties = await db.Parties
            .IgnoreQueryFilters()
            .Where(x => altCodes.Contains(x.PartyCode))
            .ToDictionaryAsync(x => x.PartyCode, x => x, StringComparer.OrdinalIgnoreCase, cancellationToken);

        int successCount = 0;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.AlternateCode) || string.IsNullOrWhiteSpace(item.OriginalCode))
                continue;

            var trimmedAltCode = item.AlternateCode.Trim();
            if (existingParties.TryGetValue(trimmedAltCode, out var existing))
            {
                existing.OriginalPartyCode = item.OriginalCode.Trim();
                if (!string.IsNullOrWhiteSpace(item.PartyName))
                {
                    existing.PartyName = item.PartyName.Trim();
                }
                existing.IsDeleted = false; // Restore if it was soft-deleted
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = User.Identity?.Name ?? "system";
            }
            else
            {
                int bId = defaultBranchId;
                if (!string.IsNullOrWhiteSpace(item.BranchCode) && branches.TryGetValue(item.BranchCode.Trim(), out var matchedId))
                {
                    bId = matchedId;
                }

                var newParty = new Party
                {
                    PartyCode = trimmedAltCode,
                    PartyName = !string.IsNullOrWhiteSpace(item.PartyName) ? item.PartyName.Trim() : $"Party {trimmedAltCode}",
                    OriginalPartyCode = item.OriginalCode.Trim(),
                    BranchId = bId,
                    Status = "Active",
                    DealerType = "Slab-Based",
                    IsDeleted = false
                };
                db.Parties.Add(newParty);
                existingParties[trimmedAltCode] = newParty; // Prevent adding duplicates in the same batch
            }
            successCount++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = $"Successfully imported {successCount} alternate mappings." });
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> UploadAlternateMappingsExcel(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Please select a valid Excel file." });

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new InvalidOperationException("Workbook has no sheets.");
            
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = sheet.Row(1);
            for (int i = 1; i <= sheet.ColumnsUsed().Count(); i++)
            {
                var txt = headerRow.Cell(i).CachedValue.ToString().Trim();
                if (!string.IsNullOrEmpty(txt)) map[txt] = i;
            }

            int altCol = map.ContainsKey("Alternate Code") ? map["Alternate Code"] : (map.ContainsKey("Cons Party Code") ? map["Cons Party Code"] : -1);
            int origCol = map.ContainsKey("Original Code") ? map["Original Code"] : (map.ContainsKey("OriginalPartyCode") ? map["OriginalPartyCode"] : -1);
            int nameCol = map.ContainsKey("Party Name") ? map["Party Name"] : (map.ContainsKey("Cons Party Name") ? map["Cons Party Name"] : -1);
            int branchCol = map.ContainsKey("Branch Code") ? map["Branch Code"] : (map.ContainsKey("Location") ? map["Location"] : -1);

            if (altCol == -1 || origCol == -1)
                return BadRequest(new { message = "Excel file must contain at least 'Alternate Code' and 'Original Code' columns." });

            var items = new List<AlternateMappingImportItem>();
            var rows = sheet.RowsUsed().Skip(1);
            foreach (var r in rows)
            {
                var alt = r.Cell(altCol).CachedValue.ToString().Trim();
                var orig = r.Cell(origCol).CachedValue.ToString().Trim();
                var name = nameCol != -1 ? r.Cell(nameCol).CachedValue.ToString().Trim() : null;
                var branch = branchCol != -1 ? r.Cell(branchCol).CachedValue.ToString().Trim() : null;

                if (!string.IsNullOrEmpty(alt) && !string.IsNullOrEmpty(orig))
                {
                    items.Add(new AlternateMappingImportItem
                    {
                        AlternateCode = alt,
                        OriginalCode = orig,
                        PartyName = name,
                        BranchCode = branch
                    });
                }
            }

            return await ImportAlternateMappingsBatch(items, cancellationToken);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =========================================================
    // BANK MASTER DETAILS
    // =========================================================
    public async Task<IActionResult> BankDetails(CancellationToken cancellationToken)
    {
        ViewBag.Parties = await db.Parties
            .Include(x => x.BankDetails)
            .Where(x => !x.IsDeleted && !x.BankDetails.Any(b => !b.IsDeleted))
            .OrderBy(x => x.PartyName)
            .ToListAsync(cancellationToken);
        var details = await db.BankDetails
            .Include(x => x.Party)
            .ThenInclude(x => x.Branch)
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Party.PartyCode)
            .ToListAsync(cancellationToken);
        return View(details);
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> SaveBankDetail(BankDetail bankDetail, CancellationToken cancellationToken)
    {
        if (bankDetail.PartyId == 0 || string.IsNullOrWhiteSpace(bankDetail.AccountNumber) || string.IsNullOrWhiteSpace(bankDetail.IFSC))
            return BadRequest(new { message = "Party, Account Number, and IFSC are required." });

        if (bankDetail.Id == 0)
        {
            // De-primary older bank details
            var primaries = await db.BankDetails.Where(x => x.PartyId == bankDetail.PartyId && x.IsPrimary && !x.IsDeleted).ToListAsync(cancellationToken);
            foreach (var p in primaries) p.IsPrimary = false;

            bankDetail.ApprovalStatus = "Approved";
            bankDetail.IsPrimary = true;
            bankDetail.CreatedAt = DateTime.UtcNow;
            bankDetail.CreatedBy = User.Identity?.Name ?? "system";
            db.BankDetails.Add(bankDetail);
        }
        else
        {
            var existing = await db.BankDetails.FirstOrDefaultAsync(x => x.Id == bankDetail.Id, cancellationToken);
            if (existing == null) return NotFound(new { message = "Bank record not found." });

            existing.AccountHolder = bankDetail.AccountHolder;
            existing.AccountNumber = bankDetail.AccountNumber;
            existing.IFSC = bankDetail.IFSC.Trim().ToUpperInvariant();
            existing.BankName = bankDetail.BankName;
            existing.BranchName = bankDetail.BranchName;
            existing.PAN = bankDetail.PAN ?? string.Empty;
            existing.Mobile = bankDetail.Mobile ?? string.Empty;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name ?? "system";
        }

        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "Bank detail saved successfully." });
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> DeleteBankDetail(int id, CancellationToken cancellationToken)
    {
        var existing = await db.BankDetails.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing == null) return NotFound(new { message = "Bank detail not found." });

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = User.Identity?.Name ?? "system";
        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "Bank record deleted." });
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> ImportBankDetailsBatch([FromBody] List<BankDetailImportItem> items, CancellationToken cancellationToken)
    {
        if (items == null || items.Count == 0)
            return BadRequest(new { message = "No records provided." });

        static string SafeTruncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var trimmed = value.Trim();
            return trimmed.Length > maxLength ? trimmed.Substring(0, maxLength) : trimmed;
        }

        var partyCodes = items
            .Where(item => !string.IsNullOrWhiteSpace(item.PartyCode) && !string.IsNullOrWhiteSpace(item.AccountNumber) && !string.IsNullOrWhiteSpace(item.IFSC))
            .Select(item => item.PartyCode!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var accountNumbers = items
            .Where(item => !string.IsNullOrWhiteSpace(item.PartyCode) && !string.IsNullOrWhiteSpace(item.AccountNumber) && !string.IsNullOrWhiteSpace(item.IFSC))
            .Select(item => item.AccountNumber!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 1. Fetch matching parties
        var parties = await db.Parties
            .Where(x => partyCodes.Contains(x.PartyCode))
            .ToDictionaryAsync(x => x.PartyCode, x => x, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // 2. Fetch existing active bank details for these accounts to avoid unique constraint violations
        var existingBankDetails = await db.BankDetails
            .Where(x => accountNumbers.Contains(x.AccountNumber) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.AccountNumber, x => x, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // 3. De-primary older bank details for these parties in bulk
        var partyIds = parties.Values.Select(p => p.Id).ToList();
        var existingPrimaries = await db.BankDetails
            .Where(x => partyIds.Contains(x.PartyId) && x.IsPrimary && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var p in existingPrimaries)
        {
            p.IsPrimary = false;
        }

        int successCount = 0;
        int notFoundCount = 0;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.PartyCode) || string.IsNullOrWhiteSpace(item.AccountNumber) || string.IsNullOrWhiteSpace(item.IFSC))
                continue;

            var trimmedPartyCode = item.PartyCode!.Trim();
            var trimmedAccount = item.AccountNumber!.Trim();

            if (!parties.TryGetValue(trimmedPartyCode, out var party))
            {
                notFoundCount++;
                continue;
            }

            if (existingBankDetails.TryGetValue(trimmedAccount, out var existingBank))
            {
                // Update existing bank detail instead of inserting a duplicate AccountNumber
                existingBank.PartyId = party.Id;
                existingBank.AccountHolder = SafeTruncate(!string.IsNullOrWhiteSpace(item.AccountHolder) ? item.AccountHolder!.Trim() : party.PartyName, 160);
                existingBank.IFSC = SafeTruncate(item.IFSC!.Trim().ToUpperInvariant(), 15);
                existingBank.BankName = SafeTruncate(!string.IsNullOrWhiteSpace(item.BankName) ? item.BankName!.Trim() : "Imported Bank", 120);
                existingBank.BranchName = SafeTruncate(!string.IsNullOrWhiteSpace(item.BranchName) ? item.BranchName!.Trim() : "Imported Branch", 120);
                existingBank.IsPrimary = true;
                existingBank.PAN = SafeTruncate(item.PAN, 20);
                existingBank.Mobile = SafeTruncate(item.Mobile, 20);
                existingBank.UpdatedAt = DateTime.UtcNow;
                existingBank.UpdatedBy = User.Identity?.Name ?? "system";
            }
            else
            {
                var newBank = new BankDetail
                {
                    PartyId = party.Id,
                    AccountHolder = SafeTruncate(!string.IsNullOrWhiteSpace(item.AccountHolder) ? item.AccountHolder!.Trim() : party.PartyName, 160),
                    AccountNumber = SafeTruncate(trimmedAccount, 30),
                    IFSC = SafeTruncate(item.IFSC!.Trim().ToUpperInvariant(), 15),
                    BankName = SafeTruncate(!string.IsNullOrWhiteSpace(item.BankName) ? item.BankName!.Trim() : "Imported Bank", 120),
                    BranchName = SafeTruncate(!string.IsNullOrWhiteSpace(item.BranchName) ? item.BranchName!.Trim() : "Imported Branch", 120),
                    ApprovalStatus = "Approved",
                    IsPrimary = true,
                    PAN = SafeTruncate(item.PAN, 20),
                    Mobile = SafeTruncate(item.Mobile, 20),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "system"
                };
                db.BankDetails.Add(newBank);
                existingBankDetails[trimmedAccount] = newBank; // Prevent duplicate additions in the same batch
            }
            successCount++;
        }

        await db.SaveChangesAsync(cancellationToken);

        var msg = $"Successfully imported {successCount} bank records.";
        if (notFoundCount > 0) msg += $" {notFoundCount} skipped because dealer codes were not registered.";
        return Json(new { ok = true, message = msg });
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> UploadBankDetailsExcel(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Please select a valid Excel file." });

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new InvalidOperationException("Workbook has no sheets.");
            
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = sheet.Row(1);
            for (int i = 1; i <= sheet.ColumnsUsed().Count(); i++)
            {
                var txt = headerRow.Cell(i).CachedValue.ToString().Trim();
                if (!string.IsNullOrEmpty(txt)) map[txt] = i;
            }

            int partyCol = map.ContainsKey("Party Code") ? map["Party Code"] : (map.ContainsKey("Cons Party Code") ? map["Cons Party Code"] : -1);
            int holderCol = map.ContainsKey("Account Holder") ? map["Account Holder"] : (map.ContainsKey("AccountHolder") ? map["AccountHolder"] : -1);
            int numCol = map.ContainsKey("Account Number") ? map["Account Number"] : (map.ContainsKey("AccountNumber") ? map["AccountNumber"] : -1);
            int ifscCol = map.ContainsKey("IFSC") ? map["IFSC"] : (map.ContainsKey("IFSC Code") ? map["IFSC Code"] : -1);
            int bankCol = map.ContainsKey("Bank Name") ? map["Bank Name"] : (map.ContainsKey("BankName") ? map["BankName"] : -1);
            int branchCol = map.ContainsKey("Branch Name") ? map["Branch Name"] : (map.ContainsKey("BranchName") ? map["BranchName"] : -1);
            int panCol = map.ContainsKey("PAN") ? map["PAN"] : (map.ContainsKey("PAN Number") ? map["PAN Number"] : -1);
            int mobCol = map.ContainsKey("Mobile") ? map["Mobile"] : (map.ContainsKey("Mobile Number") ? map["Mobile Number"] : -1);

            if (partyCol == -1 || numCol == -1 || ifscCol == -1)
                return BadRequest(new { message = "Excel file must contain at least 'Party Code', 'Account Number', and 'IFSC' columns." });

            var items = new List<BankDetailImportItem>();
            var rows = sheet.RowsUsed().Skip(1);
            foreach (var r in rows)
            {
                var partyCode = r.Cell(partyCol).CachedValue.ToString().Trim();
                var num = r.Cell(numCol).CachedValue.ToString().Trim();
                var ifsc = r.Cell(ifscCol).CachedValue.ToString().Trim();
                
                var holder = holderCol != -1 ? r.Cell(holderCol).CachedValue.ToString().Trim() : "";
                var bank = bankCol != -1 ? r.Cell(bankCol).CachedValue.ToString().Trim() : "";
                var branch = branchCol != -1 ? r.Cell(branchCol).CachedValue.ToString().Trim() : "";
                var pan = panCol != -1 ? r.Cell(panCol).CachedValue.ToString().Trim() : "";
                var mob = mobCol != -1 ? r.Cell(mobCol).CachedValue.ToString().Trim() : "";

                if (!string.IsNullOrEmpty(partyCode) && !string.IsNullOrEmpty(num) && !string.IsNullOrEmpty(ifsc))
                {
                    items.Add(new BankDetailImportItem
                    {
                        PartyCode = partyCode,
                        AccountNumber = num,
                        IFSC = ifsc,
                        AccountHolder = holder,
                        BankName = bank,
                        BranchName = branch,
                        PAN = pan,
                        Mobile = mob
                    });
                }
            }

            return await ImportBankDetailsBatch(items, cancellationToken);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> ImportPartiesBatch([FromBody] List<PartyImportItem> items, CancellationToken cancellationToken)
    {
        if (items == null || items.Count == 0)
            return BadRequest(new { message = "No records provided." });

        var branches = await db.Branches.ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var defaultBranchId = branches.Values.FirstOrDefault();

        var partyCodes = items
            .Where(item => !string.IsNullOrWhiteSpace(item.PartyCode))
            .Select(item => item.PartyCode.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingParties = await db.Parties
            .IgnoreQueryFilters()
            .Where(x => partyCodes.Contains(x.PartyCode))
            .ToDictionaryAsync(x => x.PartyCode, x => x, StringComparer.OrdinalIgnoreCase, cancellationToken);

        int successCount = 0;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.PartyCode) || string.IsNullOrWhiteSpace(item.PartyName))
                continue;

            var trimmedCode = item.PartyCode.Trim();
            
            int bId = defaultBranchId;
            if (!string.IsNullOrWhiteSpace(item.BranchCode) && branches.TryGetValue(item.BranchCode.Trim(), out var matchedId))
            {
                bId = matchedId;
            }

            var fixedPct = item.FixedIncentivePercent ?? 8.00m;

            if (existingParties.TryGetValue(trimmedCode, out var existing))
            {
                existing.PartyName = item.PartyName.Trim();
                existing.BranchId = bId;
                existing.DealerType = "Fixed Incentive";
                existing.FixedIncentivePercent = fixedPct;
                existing.GST = item.GST?.Trim() ?? string.Empty;
                existing.Mobile = item.Mobile?.Trim() ?? string.Empty;
                existing.Address = item.Address?.Trim() ?? string.Empty;
                existing.OriginalPartyCode = item.OriginalPartyCode?.Trim() ?? string.Empty;
                existing.Status = "Active";
                existing.IsDeleted = false;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = User.Identity?.Name ?? "system";
            }
            else
            {
                var newParty = new Party
                {
                    PartyCode = trimmedCode,
                    PartyName = item.PartyName.Trim(),
                    BranchId = bId,
                    DealerType = "Fixed Incentive",
                    FixedIncentivePercent = fixedPct,
                    GST = item.GST?.Trim() ?? string.Empty,
                    Mobile = item.Mobile?.Trim() ?? string.Empty,
                    Address = item.Address?.Trim() ?? string.Empty,
                    OriginalPartyCode = item.OriginalPartyCode?.Trim() ?? string.Empty,
                    Status = "Active",
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "system"
                };
                db.Parties.Add(newParty);
                existingParties[trimmedCode] = newParty;
            }
            successCount++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = $"Successfully imported {successCount} fixed incentive parties." });
    }

    [HttpGet]
    public async Task<IActionResult> GetPartyDetails(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { ok = false, message = "Code is required" });
        }

        var party = await db.Parties
            .Include(p => p.Branch)
            .FirstOrDefaultAsync(x => x.PartyCode == code.Trim() && !x.IsDeleted, cancellationToken);

        if (party == null)
        {
            return NotFound(new { ok = false, message = "Party not found" });
        }

        return Json(new { 
            ok = true, 
            partyName = party.PartyName, 
            branchCode = party.Branch?.Code ?? "" 
        });
    }
}

public sealed class AlternateMappingImportItem
{
    public string AlternateCode { get; set; } = string.Empty;
    public string OriginalCode { get; set; } = string.Empty;
    public string? PartyName { get; set; }
    public string? BranchCode { get; set; }
}

public sealed class BankDetailImportItem
{
    public string? PartyCode { get; set; }
    public string? AccountHolder { get; set; }
    public string? AccountNumber { get; set; }
    public string? IFSC { get; set; }
    public string? BankName { get; set; }
    public string? BranchName { get; set; }
    public string? PAN { get; set; }
    public string? Mobile { get; set; }
}

public sealed class PartyImportItem
{
    public string PartyCode { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string? BranchCode { get; set; }
    public decimal? FixedIncentivePercent { get; set; }
    public string? GST { get; set; }
    public string? Mobile { get; set; }
    public string? Address { get; set; }
    public string? OriginalPartyCode { get; set; }
}
