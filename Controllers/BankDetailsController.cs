using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Models;
using IncentivePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class BankDetailsController(IBankService bankService, IDashboardService dashboardService) : Controller
{
    [HttpPost]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance},{AppRoles.BranchManager},{AppRoles.Associate}")]
    public async Task<IActionResult> RequestChange(BankDetailRequest request, CancellationToken cancellationToken)
    {
        var approval = await bankService.RequestChangeAsync(request, cancellationToken);
        return Json(new { ok = true, approval.Id, message = "Bank change sent for approval." });
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        await bankService.ApproveAsync(id, cancellationToken);
        dashboardService.InvalidateCache();
        return Json(new { ok = true, message = "Bank details approved." });
    }
}
