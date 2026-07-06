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
    [DisableRequestSizeLimit]
    public async Task<IActionResult> PreviewUpload(IFormFile file, [FromForm] string templateCode, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Please select a valid Excel file." });

            using var stream = file.OpenReadStream();
            var preview = await uploadEngine.PreviewAsync(stream, templateCode, cancellationToken);
            return Ok(new { success = preview.IsValid, totalRows = preview.Rows.Count, errors = preview.Errors, rows = preview.Rows });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("upload/commit")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> CommitUpload(IFormFile file, [FromForm] string templateCode, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Please select a valid Excel file." });

            var username = User.Identity?.Name ?? "system";
            using var stream = file.OpenReadStream();
            var commit = await uploadEngine.CommitAsync(stream, templateCode, username, cancellationToken);

            await auditEngine.LogActionAsync(
                "CommitUpload",
                "ImportTemplate",
                templateCode,
                "{}",
                System.Text.Json.JsonSerializer.Serialize(commit),
                username,
                HttpContext.Connection.RemoteIpAddress?.ToString()
            );

            return Ok(commit);
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
