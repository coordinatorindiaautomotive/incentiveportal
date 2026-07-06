using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Models;
using IncentivePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Controllers;

[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance}")]
public sealed class SchemesController(IncentiveDbContext db, ISchemeService schemeService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await db.IncentiveSchemes.Include(x => x.Details).OrderByDescending(x => x.SchemeYear).ThenByDescending(x => x.SchemeMonth).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SchemeRequest request, CancellationToken cancellationToken)
    {
        var scheme = await schemeService.CreateAsync(request, cancellationToken);
        return Json(new { ok = true, scheme.Id, message = "Scheme version created." });
    }

    [HttpPost]
    public async Task<IActionResult> CopyPrevious(int month, int year, CancellationToken cancellationToken)
    {
        var scheme = await schemeService.CopyPreviousAsync(month, year, cancellationToken);
        return Json(new { ok = true, scheme.Id, message = "Previous scheme copied." });
    }

    [Authorize(Roles = AppRoles.SuperAdmin)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await db.IncentiveSchemes.Include(x => x.Details).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing == null) return NotFound(new { message = "Scheme not found." });

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = User.Identity?.Name ?? "system";

        foreach (var detail in existing.Details)
        {
            detail.IsDeleted = true;
            detail.UpdatedAt = DateTime.UtcNow;
            detail.UpdatedBy = User.Identity?.Name ?? "system";
        }

        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "Scheme deleted successfully." });
    }
}
