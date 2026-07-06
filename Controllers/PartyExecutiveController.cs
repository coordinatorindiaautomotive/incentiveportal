using ClosedXML.Excel;
using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Controllers;

[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance}")]
public sealed class PartyExecutiveController(IncentiveDbContext db) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var list = await db.PartyExecutiveMappings
            .OrderBy(x => x.ExecutiveName)
            .ThenBy(x => x.PartyName)
            .ToListAsync(cancellationToken);
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PartyExecutiveMapping request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PartyCode) || string.IsNullOrWhiteSpace(request.ExecutiveCode))
        {
            return Json(new { ok = false, message = "Party Code and Executive Code are required." });
        }

        if (request.Id > 0)
        {
            var existing = await db.PartyExecutiveMappings.FindAsync(new object[] { request.Id }, cancellationToken);
            if (existing is null) return Json(new { ok = false, message = "Mapping record not found." });

            existing.PartyCode = request.PartyCode.Trim().ToUpper();
            existing.PartyName = request.PartyName?.Trim() ?? string.Empty;
            existing.ExecutiveCode = request.ExecutiveCode.Trim().ToUpper();
            existing.ExecutiveName = request.ExecutiveName?.Trim() ?? string.Empty;
            existing.BranchCode = request.BranchCode?.Trim().ToUpper() ?? string.Empty;
        }
        else
        {
            var mapping = new PartyExecutiveMapping
            {
                PartyCode = request.PartyCode.Trim().ToUpper(),
                PartyName = request.PartyName?.Trim() ?? string.Empty,
                ExecutiveCode = request.ExecutiveCode.Trim().ToUpper(),
                ExecutiveName = request.ExecutiveName?.Trim() ?? string.Empty,
                BranchCode = request.BranchCode?.Trim().ToUpper() ?? string.Empty
            };
            db.PartyExecutiveMappings.Add(mapping);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "Mapping record saved successfully." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var record = await db.PartyExecutiveMappings.FindAsync(new object[] { id }, cancellationToken);
        if (record is null) return Json(new { ok = false, message = "Record not found." });

        db.PartyExecutiveMappings.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "Mapping record deleted successfully." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAll(CancellationToken cancellationToken)
    {
        var records = await db.PartyExecutiveMappings.ToListAsync(cancellationToken);
        db.PartyExecutiveMappings.RemoveRange(records);
        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "All mapping records cleared successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> UploadExcel(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Json(new { ok = false, message = "Please select a valid Excel workbook file." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.First();
            var rows = sheet.RowsUsed().Skip(1); // skip header row

            var newMappings = new List<PartyExecutiveMapping>();
            int processedCount = 0;

            foreach (var row in rows)
            {
                // Columns: PartyCode (1), PartyName (2), ExecutiveCode (3), ExecutiveName (4), BranchCode (5)
                var partyCode = row.Cell(1).GetString()?.Trim().ToUpper();
                var partyName = row.Cell(2).GetString()?.Trim();
                var execCode = row.Cell(3).GetString()?.Trim().ToUpper();
                var execName = row.Cell(4).GetString()?.Trim();
                var branchCode = row.Cell(5).GetString()?.Trim().ToUpper();

                if (string.IsNullOrWhiteSpace(partyCode) || string.IsNullOrWhiteSpace(execCode))
                    continue;

                newMappings.Add(new PartyExecutiveMapping
                {
                    PartyCode = partyCode,
                    PartyName = partyName ?? string.Empty,
                    ExecutiveCode = execCode,
                    ExecutiveName = execName ?? string.Empty,
                    BranchCode = branchCode ?? string.Empty
                });
                processedCount++;
            }

            if (newMappings.Count > 0)
            {
                db.PartyExecutiveMappings.AddRange(newMappings);
                await db.SaveChangesAsync(cancellationToken);
            }

            return Json(new { ok = true, message = $"Successfully imported {processedCount} executive-party mappings from Excel." });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, message = $"Failed to parse Excel workbook: {ex.Message}" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadCopyPaste(string pasteData, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pasteData))
        {
            return Json(new { ok = false, message = "Pasted tabular data is empty." });
        }

        try
        {
            var lines = pasteData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var newMappings = new List<PartyExecutiveMapping>();
            int processedCount = 0;

            foreach (var line in lines)
            {
                // Try tab separation first (Excel default copy)
                var parts = line.Split('\t');
                if (parts.Length < 3)
                {
                    // Fallback to comma separation
                    parts = line.Split(',');
                }

                if (parts.Length < 3) continue;

                var partyCode = parts[0]?.Trim().ToUpper();
                var partyName = parts.Length > 1 ? parts[1]?.Trim() : string.Empty;
                var execCode = parts.Length > 2 ? parts[2]?.Trim().ToUpper() : string.Empty;
                var execName = parts.Length > 3 ? parts[3]?.Trim() : string.Empty;
                var branchCode = parts.Length > 4 ? parts[4]?.Trim().ToUpper() : string.Empty;

                if (string.IsNullOrWhiteSpace(partyCode) || string.IsNullOrWhiteSpace(execCode))
                    continue;

                newMappings.Add(new PartyExecutiveMapping
                {
                    PartyCode = partyCode,
                    PartyName = partyName ?? string.Empty,
                    ExecutiveCode = execCode,
                    ExecutiveName = execName ?? string.Empty,
                    BranchCode = branchCode ?? string.Empty
                });
                processedCount++;
            }

            if (newMappings.Count > 0)
            {
                db.PartyExecutiveMappings.AddRange(newMappings);
                await db.SaveChangesAsync(cancellationToken);
            }

            return Json(new { ok = true, message = $"Successfully imported {processedCount} mappings from pasted tabular data." });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, message = $"Failed to parse pasted data: {ex.Message}" });
        }
    }
}
