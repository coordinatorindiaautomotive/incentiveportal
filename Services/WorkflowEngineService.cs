using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Services;

public interface IWorkflowEngineService
{
    Task<WorkflowAssignment> SubmitToWorkflowAsync(string targetEntityId, string targetEntityType, string workflowCode, string username);
    Task<WorkflowAssignment> ApproveStepAsync(int assignmentId, string username, string roleName, string remarks);
    Task<WorkflowAssignment> RejectStepAsync(int assignmentId, string username, string roleName, string remarks);
}

public sealed class WorkflowEngineService(IncentiveDbContext db) : IWorkflowEngineService
{
    public async Task<WorkflowAssignment> SubmitToWorkflowAsync(string targetEntityId, string targetEntityType, string workflowCode, string username)
    {
        // 1. Get workflow definition
        var definition = await db.WorkflowDefinitions
            .Include(d => d.Steps)
            .FirstOrDefaultAsync(d => d.Code == workflowCode && d.IsActive && !d.IsDeleted);

        if (definition == null)
            throw new InvalidOperationException($"Active workflow definition with code '{workflowCode}' was not found.");

        if (definition.Steps == null || !definition.Steps.Any())
            throw new InvalidOperationException($"Workflow '{workflowCode}' has no steps defined.");

        // 2. Check if an assignment already exists for this target
        var existing = await db.WorkflowAssignments
            .FirstOrDefaultAsync(a => a.TargetEntityId == targetEntityId && a.TargetEntityType == targetEntityType && a.IsActive && !a.IsDeleted);

        if (existing != null)
        {
            // Reset to step 1
            existing.CurrentStepNumber = 1;
            existing.Status = "Pending";
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = username;

            var resetHistory = new WorkflowHistory
            {
                WorkflowAssignmentId = existing.Id,
                StepNumber = 0,
                Action = "Re-submitted",
                PerformedBy = username,
                PerformedAt = DateTime.UtcNow,
                Remarks = "Re-submitted to workflow."
            };
            db.WorkflowHistories.Add(resetHistory);
            await db.SaveChangesAsync();
            return existing;
        }

        // 3. Create new assignment
        var assignment = new WorkflowAssignment
        {
            WorkflowDefinitionId = definition.Id,
            TargetEntityId = targetEntityId,
            TargetEntityType = targetEntityType,
            CurrentStepNumber = 1,
            Status = "Pending",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username
        };
        db.WorkflowAssignments.Add(assignment);
        await db.SaveChangesAsync();

        var history = new WorkflowHistory
        {
            WorkflowAssignmentId = assignment.Id,
            StepNumber = 0,
            Action = "Submitted",
            PerformedBy = username,
            PerformedAt = DateTime.UtcNow,
            Remarks = "Submitted to workflow."
        };
        db.WorkflowHistories.Add(history);
        await db.SaveChangesAsync();

        return assignment;
    }

    public async Task<WorkflowAssignment> ApproveStepAsync(int assignmentId, string username, string roleName, string remarks)
    {
        var assignment = await db.WorkflowAssignments
            .Include(a => a.WorkflowDefinition)
            .ThenInclude(d => d.Steps)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.IsActive && !a.IsDeleted);

        if (assignment == null)
            throw new KeyNotFoundException("Workflow assignment not found.");

        if (assignment.Status != "Pending")
            throw new InvalidOperationException("This workflow assignment has already been finalized.");

        var steps = assignment.WorkflowDefinition.Steps.OrderBy(s => s.StepNumber).ToList();
        var currentStep = steps.FirstOrDefault(s => s.StepNumber == assignment.CurrentStepNumber);

        if (currentStep == null)
            throw new InvalidOperationException("Invalid workflow step state.");

        // Validate that the user has the allowed role for this step
        if (!currentStep.RoleAllowed.Equals(roleName, StringComparison.OrdinalIgnoreCase) && 
            !roleName.Equals("Super Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Role '{roleName}' is not authorized to approve step {currentStep.StepNumber} ({currentStep.StepName}). Required: '{currentStep.RoleAllowed}'");
        }

        // Add history entry
        var history = new WorkflowHistory
        {
            WorkflowAssignmentId = assignment.Id,
            StepNumber = currentStep.StepNumber,
            Action = "Approved",
            PerformedBy = username,
            PerformedAt = DateTime.UtcNow,
            Remarks = remarks
        };
        db.WorkflowHistories.Add(history);

        // Advance step or complete workflow
        var nextStep = steps.FirstOrDefault(s => s.StepNumber > currentStep.StepNumber);
        if (nextStep != null)
        {
            assignment.CurrentStepNumber = nextStep.StepNumber;
            assignment.Status = "Pending";
        }
        else
        {
            assignment.Status = "Approved";
            // Callback: update target entity status
            await UpdateTargetEntityStatusAsync(assignment.TargetEntityId, assignment.TargetEntityType, "Approved", username);
        }

        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.UpdatedBy = username;

        await db.SaveChangesAsync();
        return assignment;
    }

    public async Task<WorkflowAssignment> RejectStepAsync(int assignmentId, string username, string roleName, string remarks)
    {
        var assignment = await db.WorkflowAssignments
            .Include(a => a.WorkflowDefinition)
            .ThenInclude(d => d.Steps)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.IsActive && !a.IsDeleted);

        if (assignment == null)
            throw new KeyNotFoundException("Workflow assignment not found.");

        if (assignment.Status != "Pending")
            throw new InvalidOperationException("This workflow assignment has already been finalized.");

        var steps = assignment.WorkflowDefinition.Steps.OrderBy(s => s.StepNumber).ToList();
        var currentStep = steps.FirstOrDefault(s => s.StepNumber == assignment.CurrentStepNumber);

        if (currentStep == null)
            throw new InvalidOperationException("Invalid workflow step state.");

        // Validate role
        if (!currentStep.RoleAllowed.Equals(roleName, StringComparison.OrdinalIgnoreCase) && 
            !roleName.Equals("Super Admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Role '{roleName}' is not authorized to reject step {currentStep.StepNumber} ({currentStep.StepName}). Required: '{currentStep.RoleAllowed}'");
        }

        // Add history
        var history = new WorkflowHistory
        {
            WorkflowAssignmentId = assignment.Id,
            StepNumber = currentStep.StepNumber,
            Action = "Rejected",
            PerformedBy = username,
            PerformedAt = DateTime.UtcNow,
            Remarks = remarks
        };
        db.WorkflowHistories.Add(history);

        assignment.Status = "Rejected";
        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.UpdatedBy = username;

        // Callback: update target entity status
        await UpdateTargetEntityStatusAsync(assignment.TargetEntityId, assignment.TargetEntityType, "Rejected", username);

        await db.SaveChangesAsync();
        return assignment;
    }

    private async Task UpdateTargetEntityStatusAsync(string targetEntityId, string targetEntityType, string status, string username)
    {
        if (targetEntityType.Equals("BankDetail", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(targetEntityId, out int detailId))
            {
                var detail = await db.BankDetails.FindAsync(detailId);
                if (detail != null)
                {
                    detail.ApprovalStatus = status;
                    detail.UpdatedAt = DateTime.UtcNow;
                    detail.UpdatedBy = username;
                }
            }
        }
        else if (targetEntityType.Equals("IncentivePeriod", StringComparison.OrdinalIgnoreCase))
        {
            // E.g. lock/unlock a month upon workflow completion
            // targetEntityId format: "2026_05" (year_month)
            var parts = targetEntityId.Split('_');
            if (parts.Length == 2 && int.TryParse(parts[0], out int year) && int.TryParse(parts[1], out int month))
            {
                var lockRecord = await db.MonthLocks.FirstOrDefaultAsync(l => l.LockYear == year && l.LockMonth == month);
                if (lockRecord != null)
                {
                    lockRecord.IsLocked = (status == "Approved");
                    lockRecord.LockedAt = (status == "Approved") ? DateTime.UtcNow : null;
                    lockRecord.LockedBy = (status == "Approved") ? username : null;
                    lockRecord.UpdatedAt = DateTime.UtcNow;
                    lockRecord.UpdatedBy = username;
                }
            }
        }
    }
}
