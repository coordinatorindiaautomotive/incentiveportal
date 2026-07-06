using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

/// <summary>
/// Service interface managing the secure creation and approval pipeline for dealer bank details.
/// </summary>
public interface IBankService
{
    /// <summary>
    /// Files a bank detail change request for review by HO Finance/Super Admin.
    /// </summary>
    Task<BankApprovalRequest> RequestChangeAsync(BankDetailRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a bank details change request and updates the primary master bank records.
    /// </summary>
    Task ApproveAsync(int requestId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sealed implementation of <see cref="IBankService"/> checking for duplicate account details.
/// </summary>
public sealed class BankService(IncentiveDbContext db) : IBankService
{
    public async Task<BankApprovalRequest> RequestChangeAsync(BankDetailRequest request, CancellationToken cancellationToken = default)
    {
        if (await db.BankDetails.AnyAsync(x => x.AccountNumber == request.AccountNumber, cancellationToken))
            throw new InvalidOperationException("Duplicate account number is not allowed.");

        if (await db.BankDetails.AnyAsync(x => x.PartyId == request.PartyId && x.ApprovalStatus == "Approved" && !x.IsDeleted, cancellationToken))
            throw new InvalidOperationException("Approved bank details already exist for this party.");

        var approval = new BankApprovalRequest
        {
            PartyId = request.PartyId,
            RequestType = "Create",
            NewJson = JsonSerializer.Serialize(request)
        };
        db.BankApprovalRequests.Add(approval);
        await db.SaveChangesAsync(cancellationToken);
        return approval;
    }

    public async Task ApproveAsync(int requestId, CancellationToken cancellationToken = default)
    {
        var approval = await db.BankApprovalRequests.FirstAsync(x => x.Id == requestId, cancellationToken);
        var payload = JsonSerializer.Deserialize<BankDetailRequest>(approval.NewJson)!;
        db.BankDetails.Add(new BankDetail
        {
            PartyId = payload.PartyId,
            AccountHolder = payload.AccountHolder,
            AccountNumber = payload.AccountNumber,
            IFSC = payload.IFSC.ToUpperInvariant(),
            BankName = payload.BankName,
            BranchName = payload.BranchName,
            ApprovalStatus = "Approved",
            PAN = payload.PAN,
            Mobile = payload.Mobile
        });
        approval.Status = "Approved";
        approval.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
