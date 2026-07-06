using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using IncentivePortal.DTOs;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

/// <summary>
/// Service interface for uploading, previewing, and committing raw Excel monthly transactional sales.
/// </summary>
public interface ISalesImportService
{
    /// <summary>
    /// Performs a full dry-run validation of the Excel file, mapping headers, checking month locks, and scanning for errors.
    /// </summary>
    Task<IReadOnlyList<SalesImportRow>> PreviewAsync(IFormFile file, string? uploadMode = null, string? branchRulesJson = null, string? alternateCodesJson = null, CancellationToken cancellationToken = default, int? limit = null);

    /// <summary>
    /// Commits validated transactions to the database. Safely wipes prior calculations and versions the entries cleanly.
    /// </summary>
    Task<ImportSummary> CommitAsync(IReadOnlyList<SalesImportRow> rows, string fileName, string? uploadMode = null, string? changeReason = null, int? previousImportLogId = null, CancellationToken cancellationToken = default, IFormFile? file = null, bool rewriteSales = true);
}

/// <summary>
/// Orchestrated implementation of <see cref="ISalesImportService"/> delegating to sub-services.
/// </summary>
public sealed class SalesImportService(
    IImportPreviewService previewService,
    IImportCommitService commitService
) : ISalesImportService
{
    public Task<IReadOnlyList<SalesImportRow>> PreviewAsync(
        IFormFile file,
        string? uploadMode = null,
        string? branchRulesJson = null,
        string? alternateCodesJson = null,
        CancellationToken cancellationToken = default,
        int? limit = null)
    {
        return previewService.PreviewAsync(file, uploadMode, branchRulesJson, alternateCodesJson, cancellationToken, limit);
    }

    public Task<ImportSummary> CommitAsync(
        IReadOnlyList<SalesImportRow> rows,
        string fileName,
        string? uploadMode = null,
        string? changeReason = null,
        int? previousImportLogId = null,
        CancellationToken cancellationToken = default,
        IFormFile? file = null,
        bool rewriteSales = true)
    {
        return commitService.CommitAsync(rows, fileName, uploadMode, changeReason, previousImportLogId, cancellationToken, file, rewriteSales);
    }
}
