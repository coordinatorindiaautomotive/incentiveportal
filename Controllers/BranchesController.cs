using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Controllers;

[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance}")]
public sealed class BranchesController(IncentiveDbContext db) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var branches = await db.Branches.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        
        var distinctTypes = await db.Branches
            .Where(x => !string.IsNullOrEmpty(x.BranchType))
            .Select(x => x.BranchType)
            .Distinct()
            .ToListAsync(cancellationToken);

        var defaults = new List<string> { "MW", "AW", "RO" };
        foreach (var d in defaults)
        {
            if (!distinctTypes.Contains(d))
                distinctTypes.Add(d);
        }

        ViewBag.BranchTypes = distinctTypes.OrderBy(x => x).ToList();
        return View(branches);
    }

    [HttpPost]
    public async Task<IActionResult> Save(Branch branch, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(branch.Code))
            return BadRequest(new { message = "Branch Code is required." });

        var existing = await db.Branches.FirstOrDefaultAsync(x => x.Code == branch.Code, cancellationToken);
        if (existing is null)
        {
            db.Branches.Add(branch);
        }
        else
        {
            existing.Name = branch.Name;
            existing.Region = branch.Region ?? string.Empty;
            existing.BranchType = branch.BranchType ?? string.Empty;
            existing.Consignee = branch.Consignee ?? string.Empty;
            existing.Incharge = branch.Incharge ?? string.Empty;
            existing.MobileNo = branch.MobileNo ?? string.Empty;
            existing.EmailID = branch.EmailID ?? string.Empty;
            existing.Address = branch.Address ?? string.Empty;
            existing.OpeningYear = branch.OpeningYear ?? string.Empty;
            existing.Area = branch.Area ?? string.Empty;
            existing.Longitude = branch.Longitude ?? string.Empty;
            existing.Latitude = branch.Latitude ?? string.Empty;
            existing.Status = branch.Status;
            existing.AllowedCategories = branch.AllowedCategories ?? "AA,M";
            existing.AllowedPartyTypes = branch.AllowedPartyTypes ?? "INDEPENDENT WORKSHOP";
            existing.TallyOutletCode = branch.TallyOutletCode ?? string.Empty;

            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = "system";
        }
        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "Branch saved successfully." });
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await db.Branches.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing == null) return NotFound(new { message = "Branch not found." });

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = User.Identity?.Name ?? "system";
        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "Branch deleted successfully." });
    }

    [HttpGet]
    public async Task<IActionResult> GetAutocompleteMetadata(CancellationToken cancellationToken)
    {
        var categories = await db.SsIncentives
            .Where(x => !x.IsDeleted && !string.IsNullOrEmpty(x.PartCategoryCode))
            .Select(x => x.PartCategoryCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        var defaultCats = new[] { "AA", "M", "AG" };
        foreach (var c in defaultCats)
        {
            if (!categories.Contains(c)) categories.Add(c);
        }
        var partyTypes = await db.Parties
            .Where(x => !x.IsDeleted && !string.IsNullOrEmpty(x.DealerType) && x.DealerType != "IMPORTED")
            .Select(x => x.DealerType)
            .Distinct()
            .ToListAsync(cancellationToken);

        var defaultTypes = new[] { "INDEPENDENT WORKSHOP", "MASS", "FIXED INCENTIVE" };
        foreach (var t in defaultTypes)
        {
            if (!partyTypes.Contains(t)) partyTypes.Add(t);
        }

        return Json(new { categories = categories.OrderBy(x => x), partyTypes = partyTypes.OrderBy(x => x) });
    }
}
