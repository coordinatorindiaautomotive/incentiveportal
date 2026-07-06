using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Services;

public interface IAuditEngineService
{
    Task LogActionAsync(string actionName, string entityName, string entityId, string oldValue, string newValue, string username, string? ipAddress);
    Task<List<AuditLog>> GetLogsAsync(string? entityName, string? username, DateTime? fromDate, DateTime? toDate);
}

public sealed class AuditEngineService(IncentiveDbContext db) : IAuditEngineService
{
    public async Task LogActionAsync(string actionName, string entityName, string entityId, string oldValue, string newValue, string username, string? ipAddress)
    {
        var log = new AuditLog
        {
            Action = actionName,
            EntityName = entityName,
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = username,
            ChangedAt = DateTime.UtcNow,
            IpAddress = ipAddress
        };

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetLogsAsync(string? entityName, string? username, DateTime? fromDate, DateTime? toDate)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(entityName))
        {
            query = query.Where(l => l.EntityName.Contains(entityName));
        }

        if (!string.IsNullOrEmpty(username))
        {
            query = query.Where(l => l.ChangedBy.Contains(username));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(l => l.ChangedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(l => l.ChangedAt <= toDate.Value);
        }

        return await query
            .OrderByDescending(l => l.ChangedAt)
            .Take(1000) // Safety limit for rendering
            .ToListAsync();
    }
}
