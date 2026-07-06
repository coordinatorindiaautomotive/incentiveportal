using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

public interface IImportRollbackService
{
    Task<bool> RollbackBatchAsync(int importLogId, string username, string? reason, CancellationToken cancellationToken);
    Task<bool> RollbackPartialAsync(int importLogId, List<string> partyCodes, string username, string? reason, CancellationToken cancellationToken);
}

public sealed class ImportRollbackService(IncentiveDbContext db) : IImportRollbackService
{
    public async Task<bool> RollbackBatchAsync(int importLogId, string username, string? reason, CancellationToken cancellationToken)
    {
        var log = await db.ImportLogs.FindAsync(new object[] { importLogId }, cancellationToken);
        if (log == null || log.Status == "RolledBack")
            return false;

        // Period lock check
        var isLocked = await db.MonthLocks.AnyAsync(x => x.LockYear == log.Year && (x.LockMonth == log.Month || x.LockMonth == 0) && x.IsLocked, cancellationToken);
        if (isLocked && !log.IsHistorical)
            throw new InvalidOperationException($"The period {log.Month}/{log.Year} is locked. Unlock it first to perform a rollback.");

        // Check if any payout has been Paid or Approved
        var incentives = await db.SsIncentives
            .Where(x => x.ImportLogId == importLogId)
            .ToListAsync(cancellationToken);

        var hasPaidOrApproved = incentives.Any(l => l.PaymentStatus == "Paid" || l.PaymentStatus == "Approved");
        if (hasPaidOrApproved && !log.IsHistorical)
            throw new InvalidOperationException("Cannot roll back batch: some incentive transactions are already approved or paid.");

        // Wrap in execution strategy to support connection resiliency + explicit transaction
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. Delete SsIncentives records
                if (incentives.Count > 0)
                {
                    var ids = incentives.Select(l => l.Id).ToList();
                    await db.SsIncentives.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
                }

                // 2. Delete Raw records
                await db.Raws.Where(x => x.ImportLogId == importLogId).ExecuteDeleteAsync(cancellationToken);

                // 3. Update ImportLog Status
                log.Status = "RolledBack";
                log.ChangeReason = $"Rolled back by {username} on {DateTime.UtcNow:g}. Reason: {reason}";
                log.UpdatedAt = DateTime.UtcNow;
                log.UpdatedBy = username;

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        return true;
    }

    public async Task<bool> RollbackPartialAsync(int importLogId, List<string> partyCodes, string username, string? reason, CancellationToken cancellationToken)
    {
        if (partyCodes == null || partyCodes.Count == 0)
            return false;

        var log = await db.ImportLogs.FindAsync(new object[] { importLogId }, cancellationToken);
        if (log == null || log.Status == "RolledBack")
            return false;

        var isLocked = await db.MonthLocks.AnyAsync(x => x.LockYear == log.Year && (x.LockMonth == log.Month || x.LockMonth == 0) && x.IsLocked, cancellationToken);
        if (isLocked && !log.IsHistorical)
            throw new InvalidOperationException($"The period {log.Month}/{log.Year} is locked. Unlock it first to perform a rollback.");

        var incentives = await db.SsIncentives
            .Where(x => x.ImportLogId == importLogId && partyCodes.Contains(x.PartyCode))
            .ToListAsync(cancellationToken);

        var hasPaidOrApproved = incentives.Any(l => l.PaymentStatus == "Paid" || l.PaymentStatus == "Approved");
        if (hasPaidOrApproved && !log.IsHistorical)
            throw new InvalidOperationException("Cannot roll back: some selected incentive payouts are already approved or paid.");

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. Delete selected SsIncentives records
                if (incentives.Count > 0)
                {
                    var ids = incentives.Select(l => l.Id).ToList();
                    await db.SsIncentives.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
                }

                // 2. Delete selected Raw records
                await db.Raws.Where(x => x.ImportLogId == importLogId && x.ConsPartyCode != null && partyCodes.Contains(x.ConsPartyCode)).ExecuteDeleteAsync(cancellationToken);

                // 3. Update ImportLog numbers
                log.SuccessRows = Math.Max(0, log.SuccessRows - incentives.Count);
                log.ChangeReason = $"Partially rolled back {partyCodes.Count} dealers by {username} on {DateTime.UtcNow:g}. Reason: {reason}";
                log.UpdatedAt = DateTime.UtcNow;
                log.UpdatedBy = username;

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        return true;
    }
}
