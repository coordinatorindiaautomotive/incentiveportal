using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using IncentivePortal.Services;
using IncentivePortal.DTOs;
using IncentivePortal.Models;
using System.Collections.Generic;

namespace IncentivePortal.Helpers;

public interface IBackgroundJobExecutor
{
    Task RunImportJobAsync(
        string jobId,
        string filePath,
        string fileName,
        string uploadMode,
        string? branchRulesJson,
        string? alternateCodesJson,
        string? changeReason,
        int? previousImportLogId,
        bool rewriteSales,
        string username,
        string? previewToken = null);

    Task RunCalculateJobAsync(
        string jobId,
        int month,
        int year,
        bool forceRecalculate,
        string? branchRulesJson,
        string? partyMappingsJson,
        string? governorFiltersJson,
        string username);
}

public class BackgroundJobState
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // "Pending", "Processing", "Succeeded", "Failed"
    public string Message { get; set; } = string.Empty;
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public int DeletedRows { get; set; }
    public int TotalRows { get; set; }
    public string Error { get; set; } = string.Empty;
}

public class BackgroundJobExecutor(
    ISalesImportService importService,
    IIncentiveCalculationService calculationService,
    IMemoryCache cache
) : IBackgroundJobExecutor
{
    public async Task RunImportJobAsync(
        string jobId,
        string filePath,
        string fileName,
        string uploadMode,
        string? branchRulesJson,
        string? alternateCodesJson,
        string? changeReason,
        int? previousImportLogId,
        bool rewriteSales,
        string username,
        string? previewToken = null)
    {
        Console.WriteLine($"[HANGFIRE] RunImportJobAsync entered. JobId: {jobId}, FilePath: {filePath}, PreviewToken: {previewToken}");
        var state = new BackgroundJobState
        {
            JobId = jobId,
            Status = "Processing",
            Message = "Parsing and validating spreadsheet..."
        };
        cache.Set($"JobStatus_{jobId}", state, TimeSpan.FromHours(1));

        try
        {
            Console.WriteLine($"[HANGFIRE] JobId: {jobId} - checking if file exists at {filePath}");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Uploaded file was not found on the server.", filePath);
            }

            IReadOnlyList<IncentivePortal.DTOs.SalesImportRow> rows;

            // Fast path: use cached rows from the preview step (skips full re-parse + validation)
            if (!string.IsNullOrEmpty(previewToken) &&
                cache.TryGetValue($"PreviewRows_{previewToken}", out IReadOnlyList<IncentivePortal.DTOs.SalesImportRow>? cachedRows) &&
                cachedRows != null)
            {
                Console.WriteLine($"[HANGFIRE] JobId: {jobId} - found {cachedRows.Count} rows in preview cache.");
                rows = cachedRows;
                // Remove from cache to free memory
                cache.Remove($"PreviewRows_{previewToken}");
            }
            else
            {
                Console.WriteLine($"[HANGFIRE] JobId: {jobId} - Preview cache not found or expired. Re-parsing file.");
                // Fallback: re-parse file (cache expired or token not provided)
                state.Message = "Re-validating spreadsheet (preview cache expired)...";
                cache.Set($"JobStatus_{jobId}", state, TimeSpan.FromHours(1));

                var formFile = new FileSystemFormFile(filePath, fileName);
                rows = await importService.PreviewAsync(
                    formFile,
                    uploadMode,
                    branchRulesJson,
                    alternateCodesJson,
                    CancellationToken.None);
            }

            Console.WriteLine($"[HANGFIRE] JobId: {jobId} - Committing {rows.Count} rows to DB...");
            state.Message = "Committing transactions to database...";
            cache.Set($"JobStatus_{jobId}", state, TimeSpan.FromHours(1));

            var formFileForCommit = new FileSystemFormFile(filePath, fileName);

            // Step 2: Commit
            Console.WriteLine($"[HANGFIRE] JobId: {jobId} - Calling importService.CommitAsync...");
            var summary = await importService.CommitAsync(
                rows,
                fileName,
                uploadMode,
                changeReason,
                previousImportLogId,
                cancellationToken: CancellationToken.None,
                file: formFileForCommit,
                rewriteSales: rewriteSales);

            Console.WriteLine($"[HANGFIRE] JobId: {jobId} - importService.CommitAsync finished successfully. LogId: {summary.Log?.Id}");
            var log = summary.Log;

            state.Status = "Succeeded";
            state.Message = $"Import completed. Committed: {summary.Committed:N0}, Deleted: {summary.DeletedRecords:N0}, Skipped: {summary.Skipped:N0} of {summary.TotalRows:N0} total rows.";
            state.SuccessRows = summary.Committed;
            state.FailedRows = summary.Skipped;
            state.DeletedRows = summary.DeletedRecords;
            state.TotalRows = summary.TotalRows;
            cache.Set($"JobStatus_{jobId}", state, TimeSpan.FromHours(1));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HANGFIRE] JobId: {jobId} - Exception caught: {ex.Message}\n{ex.StackTrace}");
            state.Status = "Failed";
            state.Error = ex.Message;
            state.Message = "Import failed: " + ex.Message;
            cache.Set($"JobStatus_{jobId}", state, TimeSpan.FromHours(1));
            throw;
        }
        finally
        {
            // Clean up the temp file
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // Ignore file delete failure
                }
            }
        }
    }

    public async Task RunCalculateJobAsync(
        string jobId,
        int month,
        int year,
        bool forceRecalculate,
        string? branchRulesJson,
        string? partyMappingsJson,
        string? governorFiltersJson,
        string username)
    {
        var state = new BackgroundJobState
        {
            JobId = jobId,
            Status = "Processing",
            Message = "Running monthly incentive calculations..."
        };
        cache.Set($"JobStatus_{jobId}", state, TimeSpan.FromHours(1));

        try
        {
            IReadOnlyList<IncentivePortal.DTOs.BranchCalcRule>? branchRules = null;
            if (!string.IsNullOrWhiteSpace(branchRulesJson))
            {
                branchRules = System.Text.Json.JsonSerializer.Deserialize<List<IncentivePortal.DTOs.BranchCalcRule>>(branchRulesJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            
            IReadOnlyList<IncentivePortal.DTOs.PartyMappingRule>? customMappings = null;
            if (!string.IsNullOrWhiteSpace(partyMappingsJson))
            {
                customMappings = System.Text.Json.JsonSerializer.Deserialize<List<IncentivePortal.DTOs.PartyMappingRule>>(partyMappingsJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            var results = await calculationService.CalculateMonthAsync(
                month,
                year,
                forceRecalculate,
                branchRules,
                false,
                customMappings,
                governorFiltersJson,
                CancellationToken.None);

            state.Status = "Succeeded";
            state.Message = $"Calculations completed successfully. {results.Count} dealer party record(s) processed.";
            state.SuccessRows = results.Count;
            cache.Set($"JobStatus_{jobId}", state, TimeSpan.FromHours(1));
        }
        catch (Exception ex)
        {
            state.Status = "Failed";
            state.Error = ex.Message;
            state.Message = "Calculations failed: " + ex.Message;
            cache.Set($"JobStatus_{jobId}", state, TimeSpan.FromHours(1));
            throw;
        }
    }
}

public sealed class FileSystemFormFile : IFormFile
{
    private readonly string _filePath;

    public FileSystemFormFile(string filePath, string fileName)
    {
        _filePath = filePath;
        FileName = fileName;
        Name = "file";
        var fileInfo = new FileInfo(filePath);
        Length = fileInfo.Length;
    }

    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
    public IHeaderDictionary Headers => new HeaderDictionary();
    public long Length { get; }
    public string Name { get; }
    public string FileName { get; }

    public Stream OpenReadStream()
    {
        return new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public void CopyTo(Stream target)
    {
        using var stream = OpenReadStream();
        stream.CopyTo(target);
    }

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        using var stream = OpenReadStream();
        return stream.CopyToAsync(target, cancellationToken);
    }
}
