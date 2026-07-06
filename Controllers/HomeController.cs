using System.Diagnostics;
using IncentivePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Models;

namespace IncentivePortal.Controllers;

public class HomeController : Controller
{
    [Authorize]
    public IActionResult Index()
    {
        return RedirectToAction("PerformanceReports", "Reports");
    }

    [Authorize]
    public IActionResult Dashboard()
    {
        return RedirectToAction("PerformanceReports", "Reports");
    }

    [Authorize]
    [HttpGet]
    public IActionResult Ping() => Ok();

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Search(string q, [FromServices] IncentivePortal.Data.IncentiveDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Json(Array.Empty<object>());
        }

        q = q.Trim().ToLowerInvariant();
        var results = new List<SearchResult>();

        // 1. Static navigation matches
        var navItems = new SearchResult[]
        {
            new SearchResult("Dashboard", "Menu", "/Reports/PerformanceReports", "fa-chart-pie"),
            new SearchResult("Customer 360° Hub", "Menu", "/Customer360/Index", "fa-user-gear"),
            new SearchResult("Incentive Register", "Reports", "/Reports/IncentiveRegister", "fa-file-invoice-dollar"),
            new SearchResult("Outstanding Balances Master", "Reports", "/Reports/OutstandingMaster", "fa-wallet"),
            new SearchResult("Workbook Import", "Tools", "/Imports/MonthlySales", "fa-file-excel"),
            new SearchResult("Upload Management", "Tools", "/UploadManagement/Index", "fa-cloud-arrow-up"),
            new SearchResult("Parties (Distributors)", "Master Data", "/Parties/Index", "fa-users"),
            new SearchResult("Branches", "Master Data", "/Branches/Index", "fa-sitemap"),
            new SearchResult("Incentive Schemes", "Schemes", "/Schemes/Index", "fa-gift"),
            new SearchResult("Control Tower Console", "Administration", "/ControlTower/Index", "fa-tower-control-tower"),
            new SearchResult("Helpdesk Center", "Menu", "/Helpdesk/Index", "fa-circle-question"),
            new SearchResult("Asset Register", "Menu", "/AssetRegister/Index", "fa-laptop"),
            new SearchResult("Document Manager", "Menu", "/Documents/Index", "fa-file-pdf"),
            new SearchResult("Automation Center", "Menu", "/Automation/Index", "fa-bolt")
        };

        foreach (var item in navItems)
        {
            if (item.Title.ToLowerInvariant().Contains(q) || item.Category.ToLowerInvariant().Contains(q))
            {
                results.Add(item);
            }
        }

        // 2. Query Parties
        var matchedParties = await db.Parties
            .AsNoTracking()
            .Where(p => !p.IsDeleted && (p.PartyCode.Contains(q) || p.PartyName.Contains(q)))
            .Take(4)
            .Select(p => new SearchResult(
                $"{p.PartyCode} - {p.PartyName}",
                "Distributors",
                $"/Customer360/Index?partyCode={p.PartyCode}",
                "fa-user-tie"
            ))
            .ToListAsync(cancellationToken);

        results.AddRange(matchedParties);

        // 3. Query Branches
        var matchedBranches = await db.Branches
            .AsNoTracking()
            .Where(b => !b.IsDeleted && (b.Code.Contains(q) || b.Name.Contains(q)))
            .Take(3)
            .Select(b => new SearchResult(
                $"{b.Code} - {b.Name}",
                "Branches",
                $"/Branches/Index?search={b.Code}",
                "fa-building"
            ))
            .ToListAsync(cancellationToken);

        results.AddRange(matchedBranches);

        // 4. Query Assets
        var matchedAssets = await db.AssetItems
            .AsNoTracking()
            .Where(a => !a.IsDeleted && (a.Name.Contains(q) || a.AssetCode.Contains(q) || a.SerialNumber.Contains(q)))
            .Take(3)
            .Select(a => new SearchResult(
                $"{a.AssetCode} - {a.Name}",
                "Assets",
                $"/AssetRegister/Index?search={a.AssetCode}",
                "fa-laptop-file"
            ))
            .ToListAsync(cancellationToken);

        results.AddRange(matchedAssets);

        // 5. Query Helpdesk Tickets
        var matchedTickets = await db.HelpdeskTickets
            .AsNoTracking()
            .Where(t => !t.IsDeleted && (t.Title.Contains(q) || t.Description.Contains(q)))
            .Take(3)
            .Select(t => new SearchResult(
                $"#{t.Id} - {t.Title}",
                "Helpdesk",
                $"/Helpdesk/Index?search={t.Id}",
                "fa-ticket"
            ))
            .ToListAsync(cancellationToken);

        results.AddRange(matchedTickets);

        // 6. Query Documents
        var matchedDocs = await db.DocumentItems
            .AsNoTracking()
            .Where(d => !d.IsDeleted && d.FileName.Contains(q))
            .Take(3)
            .Select(d => new SearchResult(
                d.FileName,
                "Documents",
                $"/Documents/Index?search={d.FileName}",
                "fa-file-pdf"
            ))
            .ToListAsync(cancellationToken);

        results.AddRange(matchedDocs);

        // 7. Query Cash Transactions
        var matchedCashIn = await db.CashInTransactions
            .AsNoTracking()
            .Where(c => !c.IsDeleted && (c.TransactionNo.Contains(q) || c.CustomerName.Contains(q)))
            .Take(2)
            .Select(c => new SearchResult(
                $"{c.TransactionNo} - {c.CustomerName} (₹{c.Amount})",
                "Cash Transactions",
                $"/CashManagement/CashBook?search={c.TransactionNo}",
                "fa-wallet"
            ))
            .ToListAsync(cancellationToken);

        var matchedCashOut = await db.CashOutTransactions
            .AsNoTracking()
            .Where(c => !c.IsDeleted && (c.TransactionNo.Contains(q) || c.VendorName.Contains(q)))
            .Take(2)
            .Select(c => new SearchResult(
                $"{c.TransactionNo} - {c.VendorName} (₹{c.Amount})",
                "Cash Transactions",
                $"/CashManagement/CashBook?search={c.TransactionNo}",
                "fa-receipt"
            ))
            .ToListAsync(cancellationToken);

        results.AddRange(matchedCashIn);
        results.AddRange(matchedCashOut);

        // 8. Query Invoices / Payouts (SsIncentive UTR)
        var matchedPayouts = await db.SsIncentives
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.UTRNumber != null && s.UTRNumber.Contains(q))
            .Take(2)
            .Select(s => new SearchResult(
                $"UTR: {s.UTRNumber} - {s.PartyName} (₹{s.NetTransferAmount})",
                "Payout Invoices",
                $"/Transfers/Index?search={s.UTRNumber}",
                "fa-money-bill-wave"
            ))
            .ToListAsync(cancellationToken);

        results.AddRange(matchedPayouts);

        // 9. Query Users (Employees)
        var matchedUsers = await db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && (u.UserName.Contains(q) || u.Email.Contains(q)))
            .Take(2)
            .Select(u => new SearchResult(
                $"{u.UserName} ({u.Email})",
                "Employees",
                $"/Users/Index?search={u.UserName}",
                "fa-user-tie"
            ))
            .ToListAsync(cancellationToken);

        results.AddRange(matchedUsers);

        return Json(results);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetHelpText(string page, string fieldKey, [FromServices] IncentivePortal.Data.IncentiveDbContext db, CancellationToken cancellationToken)
    {
        var help = await db.HelpTexts
            .AsNoTracking()
            .FirstOrDefaultAsync(h => !h.IsDeleted && h.Page == page && h.FieldKey == fieldKey, cancellationToken);
            
        return Json(new { text = help?.Text ?? "" });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

}

public record SearchResult(string Title, string Category, string Url, string Icon);
