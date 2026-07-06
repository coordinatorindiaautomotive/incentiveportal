using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class DocumentsController(IncentiveDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? category, string? search)
    {
        var query = db.DocumentItems.Where(d => !d.IsDeleted);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(d => d.Category == category);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(d => d.FileName.Contains(search) || (d.AssociatedPartyCode != null && d.AssociatedPartyCode.Contains(search)));

        var docs = await query
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        ViewBag.CategoryFilter = category;
        ViewBag.SearchFilter = search;

        // Fetch dealers for dropdown
        ViewBag.Parties = await db.Parties
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.PartyName)
            .Select(p => new { p.PartyCode, p.PartyName })
            .ToListAsync();

        // Expiring Soon (within 30 days)
        var limitDate = DateTime.UtcNow.AddDays(30);
        ViewBag.ExpiringSoon = await db.DocumentItems
            .Where(d => !d.IsDeleted && d.ExpiryDate != null && d.ExpiryDate <= limitDate && d.ExpiryDate >= DateTime.UtcNow)
            .OrderBy(d => d.ExpiryDate)
            .ToListAsync();

        return View(docs);
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, string category, string? associatedPartyCode, DateTime? expiryDate)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        try
        {
            // Create uploads directory if it does not exist
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            var originalFileName = file.FileName;
            var extension = Path.GetExtension(originalFileName);
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(uploadsDir, uniqueFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // check if there is an existing file for the same dealer and filename to increment version
            int version = 1;
            var existing = await db.DocumentItems
                .Where(d => d.AssociatedPartyCode == associatedPartyCode && d.FileName == originalFileName && !d.IsDeleted)
                .OrderByDescending(d => d.Version)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                version = existing.Version + 1;
            }

            var doc = new DocumentItem
            {
                FileName = originalFileName,
                FilePath = "/uploads/documents/" + uniqueFileName,
                Category = string.IsNullOrEmpty(category) ? "General" : category,
                Version = version,
                ExpiryDate = expiryDate,
                Owner = User.Identity?.Name ?? "system",
                SizeBytes = file.Length,
                AssociatedPartyCode = associatedPartyCode,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "system"
            };

            db.DocumentItems.Add(doc);
            await db.SaveChangesAsync();

            return Json(new { ok = true, message = "Document uploaded successfully.", fileName = originalFileName });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Upload failed: {ex.Message}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var doc = await db.DocumentItems.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
        if (doc == null)
        {
            return NotFound("Document not found.");
        }

        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", doc.FilePath.TrimStart('/'));
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound("File not found on disk.");
        }

        var contentType = "application/octet-stream";
        var ext = Path.GetExtension(doc.FileName).ToLowerInvariant();
        if (ext == ".pdf") contentType = "application/pdf";
        else if (ext == ".png") contentType = "image/png";
        else if (ext == ".jpg" || ext == ".jpeg") contentType = "image/jpeg";
        else if (ext == ".xlsx") contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        else if (ext == ".xls") contentType = "application/vnd.ms-excel";
        else if (ext == ".csv") contentType = "text/csv";

        return File(System.IO.File.OpenRead(fullPath), contentType, doc.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var doc = await db.DocumentItems.FindAsync(id);
        if (doc == null) return NotFound("Document not found.");

        doc.IsDeleted = true;
        doc.UpdatedAt = DateTime.UtcNow;
        doc.UpdatedBy = User.Identity?.Name ?? "system";
        await db.SaveChangesAsync();
        return Json(new { ok = true, message = "Document deleted successfully." });
    }
}
