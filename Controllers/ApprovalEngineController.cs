using IncentivePortal.Data;
using IncentivePortal.Models;
using IncentivePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class ApprovalEngineController(IncentiveDbContext db, IWorkflowEngineService workflowService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var workflows = await db.WorkflowDefinitions
            .Include(d => d.Steps)
            .Where(d => !d.IsDeleted)
            .ToListAsync();

        var pendingAssignments = await db.WorkflowAssignments
            .Include(a => a.WorkflowDefinition)
            .Where(a => a.Status == "Pending" && !a.IsDeleted)
            .ToListAsync();

        // Seed basic workflows if none exist
        if (!workflows.Any())
        {
            var bankWorkflow = new WorkflowDefinition
            {
                Code = "BANK_APPROVAL",
                Name = "Bank Details Modification Workflow",
                Description = "Two-level workflow for approving dealer bank account number changes.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Steps = new List<WorkflowStep>
                {
                    new() { StepNumber = 1, StepName = "Branch Manager Verification", RoleAllowed = AppRoles.BranchManager, RequiredApprovalsCount = 1, SlaHours = 24 },
                    new() { StepNumber = 2, StepName = "HO Finance Final Approval", RoleAllowed = AppRoles.HOFinance, RequiredApprovalsCount = 1, SlaHours = 48 }
                }
            };

            var cashWorkflow = new WorkflowDefinition
            {
                Code = "CASH_TRANSACTION",
                Name = "High Value Cash Out Transaction Approval",
                Description = "Approval workflow for branch expense transactions above ₹25,000.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system",
                Steps = new List<WorkflowStep>
                {
                    new() { StepNumber = 1, StepName = "HO Finance Audit", RoleAllowed = AppRoles.HOFinance, RequiredApprovalsCount = 1, SlaHours = 24 },
                    new() { StepNumber = 2, StepName = "Super Admin Sign-off", RoleAllowed = AppRoles.SuperAdmin, RequiredApprovalsCount = 1, SlaHours = 48 }
                }
            };

            db.WorkflowDefinitions.Add(bankWorkflow);
            db.WorkflowDefinitions.Add(cashWorkflow);
            await db.SaveChangesAsync();

            workflows = new List<WorkflowDefinition> { bankWorkflow, cashWorkflow };
        }

        ViewBag.PendingAssignments = pendingAssignments;
        ViewBag.RolesList = new[] { AppRoles.SuperAdmin, AppRoles.HOFinance, AppRoles.BranchManager, AppRoles.Associate, AppRoles.Auditor };

        return View(workflows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Save(WorkflowDefinition definition, List<WorkflowStep> steps)
    {
        if (string.IsNullOrWhiteSpace(definition.Code) || string.IsNullOrWhiteSpace(definition.Name))
            return BadRequest("Workflow Code and Name are required.");

        if (definition.Id == 0)
        {
            definition.CreatedAt = DateTime.UtcNow;
            definition.CreatedBy = User.Identity?.Name ?? "system";
            definition.IsActive = true;
            
            // Add steps
            int stepNum = 1;
            foreach (var step in steps)
            {
                if (string.IsNullOrWhiteSpace(step.StepName) || string.IsNullOrWhiteSpace(step.RoleAllowed)) continue;
                step.StepNumber = stepNum++;
                definition.Steps.Add(step);
            }

            db.WorkflowDefinitions.Add(definition);
        }
        else
        {
            var existing = await db.WorkflowDefinitions
                .Include(d => d.Steps)
                .FirstOrDefaultAsync(d => d.Id == definition.Id);

            if (existing == null || existing.IsDeleted)
                return NotFound("Workflow not found.");

            existing.Name = definition.Name;
            existing.Description = definition.Description;
            existing.IsActive = definition.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name ?? "system";

            // Remove existing steps and re-add
            db.RemoveRange(existing.Steps);
            int stepNum = 1;
            foreach (var step in steps)
            {
                if (string.IsNullOrWhiteSpace(step.StepName) || string.IsNullOrWhiteSpace(step.RoleAllowed)) continue;
                step.StepNumber = stepNum++;
                step.WorkflowDefinitionId = existing.Id;
                db.Entry(step).State = EntityState.Added;
            }
        }

        await db.SaveChangesAsync();
        return Json(new { ok = true, message = "Workflow definition saved successfully." });
    }

    [HttpGet]
    public async Task<IActionResult> GetAssignmentHistory(int assignmentId)
    {
        var assignment = await db.WorkflowAssignments
            .Include(a => a.WorkflowDefinition)
            .Include(a => a.Histories)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && !a.IsDeleted);

        if (assignment == null) return NotFound("Assignment not found.");

        var currentSteps = await db.WorkflowSteps
            .Where(s => s.WorkflowDefinitionId == assignment.WorkflowDefinitionId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync();

        var historyList = assignment.Histories
            .OrderBy(h => h.PerformedAt)
            .Select(h => new {
                h.StepNumber,
                h.Action,
                h.PerformedBy,
                performedAt = h.PerformedAt.ToString("dd MMM yyyy HH:mm"),
                h.Remarks
            }).ToList();

        var workflowStepsList = currentSteps.Select(s => new {
            s.StepNumber,
            s.StepName,
            s.RoleAllowed,
            isCurrent = s.StepNumber == assignment.CurrentStepNumber && assignment.Status == "Pending",
            isCompleted = s.StepNumber < assignment.CurrentStepNumber || assignment.Status == "Approved"
        }).ToList();

        return Json(new {
            assignment.Id,
            workflowName = assignment.WorkflowDefinition.Name,
            assignment.TargetEntityId,
            assignment.TargetEntityType,
            assignment.Status,
            currentStep = assignment.CurrentStepNumber,
            steps = workflowStepsList,
            history = historyList
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int assignmentId, string remarks)
    {
        var username = User.Identity?.Name ?? "system";
        
        // Determine role of current user
        string role = AppRoles.Associate;
        if (User.IsInRole(AppRoles.SuperAdmin)) role = AppRoles.SuperAdmin;
        else if (User.IsInRole(AppRoles.HOFinance)) role = AppRoles.HOFinance;
        else if (User.IsInRole(AppRoles.BranchManager)) role = AppRoles.BranchManager;
        else if (User.IsInRole(AppRoles.Auditor)) role = AppRoles.Auditor;
        else if (User.IsInRole(AppRoles.SalesExecutive)) role = AppRoles.SalesExecutive;

        try
        {
            await workflowService.ApproveStepAsync(assignmentId, username, role, remarks);
            
            // Add a notification for progress
            var assignment = await db.WorkflowAssignments.FindAsync(assignmentId);
            if (assignment != null)
            {
                db.SystemNotifications.Add(new SystemNotification
                {
                    TargetUser = assignment.CreatedBy,
                    Title = "Workflow Approved",
                    Message = $"Your request for {assignment.TargetEntityType} ({assignment.TargetEntityId}) was approved at Step {assignment.CurrentStepNumber}.",
                    NotificationType = "Workflow",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                });
                await db.SaveChangesAsync();
            }

            return Json(new { ok = true, message = "Workflow step approved successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int assignmentId, string remarks)
    {
        var username = User.Identity?.Name ?? "system";
        
        string role = AppRoles.Associate;
        if (User.IsInRole(AppRoles.SuperAdmin)) role = AppRoles.SuperAdmin;
        else if (User.IsInRole(AppRoles.HOFinance)) role = AppRoles.HOFinance;
        else if (User.IsInRole(AppRoles.BranchManager)) role = AppRoles.BranchManager;
        else if (User.IsInRole(AppRoles.Auditor)) role = AppRoles.Auditor;
        else if (User.IsInRole(AppRoles.SalesExecutive)) role = AppRoles.SalesExecutive;

        try
        {
            await workflowService.RejectStepAsync(assignmentId, username, role, remarks);

            // Add notification
            var assignment = await db.WorkflowAssignments.FindAsync(assignmentId);
            if (assignment != null)
            {
                db.SystemNotifications.Add(new SystemNotification
                {
                    TargetUser = assignment.CreatedBy,
                    Title = "Workflow Rejected",
                    Message = $"Your request for {assignment.TargetEntityType} ({assignment.TargetEntityId}) was rejected: {remarks}",
                    NotificationType = "Workflow",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                });
                await db.SaveChangesAsync();
            }

            return Json(new { ok = true, message = "Workflow step rejected successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
