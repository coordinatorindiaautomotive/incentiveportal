using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using IncentivePortal.Models;

namespace IncentivePortal.Data
{
    public class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private static readonly HashSet<string> ExcludedFromAudit = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(AuditLog),
            nameof(DealerMonthlyPerformance),
            nameof(IncentiveSummary),
            nameof(DealerSlabProgress),
            nameof(DealerGrowthAnalytics),
            nameof(CategorySalesAggregate)
        };

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            ProcessAudits(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ProcessAudits(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void ProcessAudits(DbContext? context)
        {
            if (context == null) return;

            // We must cast to IncentiveDbContext to check DisableAuditLogs if necessary, 
            // but the cleaner way is just tracking. If we really need DisableAuditLogs, we can check it.
            if (context is IncentiveDbContext incentiveContext && incentiveContext.DisableAuditLogs)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var user = _httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
            var ip = _httpContextAccessor?.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            // Handle AuditableEntity Creation/Updates
            foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = user;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = user;
                }
            }

            // Prepare AuditLogs
            var auditEntries = new List<AuditLog>();
            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => !ExcludedFromAudit.Contains(e.Metadata.ClrType.Name))
                .ToList();

            foreach (var entry in entries)
            {
                var oldValues = new Dictionary<string, object?>();
                var newValues = new Dictionary<string, object?>();

                foreach (var property in entry.Properties)
                {
                    string propertyName = property.Metadata.Name;

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            newValues[propertyName] = property.CurrentValue;
                            break;

                        case EntityState.Deleted:
                            oldValues[propertyName] = property.OriginalValue;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                oldValues[propertyName] = property.OriginalValue;
                                newValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }

                if (oldValues.Count == 0 && newValues.Count == 0) continue;

                var primaryKey = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                var entityId = primaryKey?.CurrentValue?.ToString() ?? "0";

                auditEntries.Add(new AuditLog
                {
                    EntityName = entry.Metadata.ClrType.Name,
                    Action = entry.State.ToString(),
                    ChangedBy = user,
                    ChangedAt = now,
                    IpAddress = ip,
                    OldValue = JsonSerializer.Serialize(oldValues),
                    NewValue = JsonSerializer.Serialize(newValues),
                    EntityId = entityId
                });
            }

            if (auditEntries.Count > 0)
            {
                context.Set<AuditLog>().AddRange(auditEntries);
            }
        }
    }
}
