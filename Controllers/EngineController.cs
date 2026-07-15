using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IncentivePortal.Models;
using IncentivePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IncentivePortal.Helpers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Text;

namespace IncentivePortal.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class EngineController(
    IFormulaEngineService formulaEngine,
    IWorkflowEngineService workflowEngine,
    IUploadEngineService uploadEngine,
    IAuditEngineService auditEngine,
    IReportEngineService reportEngine
) : ControllerBase
{
    // =========================================================
    // DYNAMIC FORMULA ENGINE API
    // =========================================================
    [HttpPost("formula/evaluate")]
    public IActionResult EvaluateFormula([FromBody] EvaluateFormulaRequest request)
    {
        try
        {
            var result = formulaEngine.Evaluate(request.Formula, request.Variables);
            return Ok(new { success = true, result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("formula/validate")]
    public IActionResult ValidateFormula([FromBody] ValidateFormulaRequest request)
    {
        var isValid = formulaEngine.ValidateFormula(request.Formula, out var errMsg);
        return Ok(new { success = isValid, message = errMsg });
    }

    // =========================================================
    // WORKFLOW ENGINE API
    // =========================================================
    [HttpPost("workflow/submit")]
    public async Task<IActionResult> SubmitToWorkflow([FromBody] SubmitWorkflowRequest request)
    {
        try
        {
            var username = User.Identity?.Name ?? "system";
            var assignment = await workflowEngine.SubmitToWorkflowAsync(request.TargetEntityId, request.TargetEntityType, request.WorkflowCode, username);
            
            // Log action to Audit Engine
            await auditEngine.LogActionAsync(
                "SubmitToWorkflow",
                request.TargetEntityType,
                request.TargetEntityId,
                "{}",
                System.Text.Json.JsonSerializer.Serialize(assignment),
                username,
                HttpContext.Connection.RemoteIpAddress?.ToString()
            );

            return Ok(new { success = true, assignmentId = assignment.Id, status = assignment.Status });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("workflow/approve")]
    public async Task<IActionResult> ApproveStep([FromBody] ApproveWorkflowRequest request)
    {
        try
        {
            var username = User.Identity?.Name ?? "system";
            // Get user role - fallback to Auditor/Associate/BranchManager/HOFinance/SuperAdmin
            string roleName = GetUserRole();

            var assignment = await workflowEngine.ApproveStepAsync(request.AssignmentId, username, roleName, request.Remarks);
            
            await auditEngine.LogActionAsync(
                "ApproveWorkflowStep",
                assignment.TargetEntityType,
                assignment.TargetEntityId,
                "{}",
                System.Text.Json.JsonSerializer.Serialize(assignment),
                username,
                HttpContext.Connection.RemoteIpAddress?.ToString()
            );

            return Ok(new { success = true, status = assignment.Status, currentStep = assignment.CurrentStepNumber });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("workflow/reject")]
    public async Task<IActionResult> RejectStep([FromBody] ApproveWorkflowRequest request)
    {
        try
        {
            var username = User.Identity?.Name ?? "system";
            string roleName = GetUserRole();

            var assignment = await workflowEngine.RejectStepAsync(request.AssignmentId, username, roleName, request.Remarks);
            
            await auditEngine.LogActionAsync(
                "RejectWorkflowStep",
                assignment.TargetEntityType,
                assignment.TargetEntityId,
                "{}",
                System.Text.Json.JsonSerializer.Serialize(assignment),
                username,
                HttpContext.Connection.RemoteIpAddress?.ToString()
            );

            return Ok(new { success = true, status = assignment.Status });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // =========================================================
    // DYNAMIC UPLOAD ENGINE API
    // =========================================================
    [HttpPost("upload/preview")]
    [DisableFormValueModelBinding]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> PreviewUpload(CancellationToken cancellationToken)
    {
        try
        {
            if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
                return BadRequest(new { success = false, message = "Not a multipart request." });

            var boundary = MultipartRequestHelper.GetBoundary(MediaTypeHeaderValue.Parse(Request.ContentType), 100);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);
            
            string templateCode = string.Empty;
            string tempFilePath = Path.GetTempFileName();
            bool hasFile = false;

            var section = await reader.ReadNextSectionAsync(cancellationToken);
            while (section != null)
            {
                var hasContentDisposition = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);
                if (hasContentDisposition)
                {
                    if (MultipartRequestHelper.HasFormDataContentDisposition(contentDisposition!))
                    {
                        var key = HeaderUtilities.RemoveQuotes(contentDisposition!.Name).Value;
                        using var streamReader = new StreamReader(section.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                        var value = await streamReader.ReadToEndAsync(cancellationToken);
                        if (string.Equals(key, "templateCode", StringComparison.OrdinalIgnoreCase))
                        {
                            templateCode = value;
                        }
                    }
                    else if (MultipartRequestHelper.HasFileContentDisposition(contentDisposition!))
                    {
                        hasFile = true;
                        using (var targetStream = System.IO.File.Create(tempFilePath))
                        {
                            await section.Body.CopyToAsync(targetStream, cancellationToken);
                        }
                    }
                }
                section = await reader.ReadNextSectionAsync(cancellationToken);
            }

            if (!hasFile)
                return BadRequest(new { success = false, message = "Please select a valid Excel file." });

            try
            {
                using var stream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
                var preview = await uploadEngine.PreviewAsync(stream, templateCode, cancellationToken);
                return Ok(new { success = preview.IsValid, totalRows = preview.Rows.Count, errors = preview.Errors, rows = preview.Rows });
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("upload/commit")]
    [DisableFormValueModelBinding]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> CommitUpload(CancellationToken cancellationToken)
    {
        try
        {
            if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
                return BadRequest(new { success = false, message = "Not a multipart request." });

            var boundary = MultipartRequestHelper.GetBoundary(MediaTypeHeaderValue.Parse(Request.ContentType), 100);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);
            
            string templateCode = string.Empty;
            string tempFilePath = Path.GetTempFileName();
            bool hasFile = false;

            var section = await reader.ReadNextSectionAsync(cancellationToken);
            while (section != null)
            {
                var hasContentDisposition = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);
                if (hasContentDisposition)
                {
                    if (MultipartRequestHelper.HasFormDataContentDisposition(contentDisposition!))
                    {
                        var key = HeaderUtilities.RemoveQuotes(contentDisposition!.Name).Value;
                        using var streamReader = new StreamReader(section.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                        var value = await streamReader.ReadToEndAsync(cancellationToken);
                        if (string.Equals(key, "templateCode", StringComparison.OrdinalIgnoreCase))
                        {
                            templateCode = value;
                        }
                    }
                    else if (MultipartRequestHelper.HasFileContentDisposition(contentDisposition!))
                    {
                        hasFile = true;
                        using (var targetStream = System.IO.File.Create(tempFilePath))
                        {
                            await section.Body.CopyToAsync(targetStream, cancellationToken);
                        }
                    }
                }
                section = await reader.ReadNextSectionAsync(cancellationToken);
            }

            if (!hasFile)
                return BadRequest(new { success = false, message = "Please select a valid Excel file." });

            var username = User.Identity?.Name ?? "system";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            try
            {
                using var stream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
                var commit = await uploadEngine.CommitAsync(stream, templateCode, username, cancellationToken);
                
                await auditEngine.LogActionAsync(
                    "CommitUpload",
                    "ImportTemplate",
                    templateCode,
                    "{}",
                    System.Text.Json.JsonSerializer.Serialize(commit),
                    username,
                    ipAddress
                );

                return Ok(commit);
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath);
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // =========================================================
    // DYNAMIC REPORT ENGINE API
    // =========================================================
    [HttpPost("reports/query")]
    public async Task<IActionResult> QueryReport([FromBody] ReportQuery query)
    {
        try
        {
            var data = await reportEngine.GenerateReportDataAsync(query);
            return Ok(new { success = true, count = data.Count, data });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("reports/export")]
    public async Task<IActionResult> ExportReport([FromBody] ReportQuery query)
    {
        try
        {
            var excelBytes = await reportEngine.ExportToExcelAsync("Dynamic_Report", query);
            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Dynamic_Report_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
            );
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // =========================================================
    // AUDIT ENGINE API
    // =========================================================
    [HttpGet("audit/logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityName,
        [FromQuery] string? username,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        try
        {
            var logs = await auditEngine.GetLogsAsync(entityName, username, fromDate, toDate);
            return Ok(new { success = true, count = logs.Count, logs });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private string GetUserRole()
    {
        if (User.IsInRole("Super Admin")) return "Super Admin";
        if (User.IsInRole("HO Finance")) return "HO Finance";
        if (User.IsInRole("Branch Manager")) return "Branch Manager";
        if (User.IsInRole("Associate")) return "Associate";
        if (User.IsInRole("Auditor")) return "Auditor";
        if (User.IsInRole("Sales Executive")) return "Sales Executive";
        return "Associate";
    }
}

// ── REQUEST DTOS ───────────────────────────────────────────────────
public sealed class EvaluateFormulaRequest
{
    public string Formula { get; set; } = string.Empty;
    public Dictionary<string, decimal> Variables { get; set; } = new();
}

public sealed class ValidateFormulaRequest
{
    public string Formula { get; set; } = string.Empty;
}

public sealed class SubmitWorkflowRequest
{
    public string TargetEntityId { get; set; } = string.Empty;
    public string TargetEntityType { get; set; } = string.Empty;
    public string WorkflowCode { get; set; } = string.Empty;
}

public sealed class ApproveWorkflowRequest
{
    public int AssignmentId { get; set; }
    public string Remarks { get; set; } = string.Empty;
}
