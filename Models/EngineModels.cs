using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IncentivePortal.Models;

public sealed class RuleMaster : AuditableEntity
{
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required, MaxLength(30)]
    public string RuleType { get; set; } = "Scheme"; // Scheme, Bonus, TDS

    public bool IsActive { get; set; } = true;

    public ICollection<RuleVersion> Versions { get; set; } = new List<RuleVersion>();
}

public sealed class RuleVersion : AuditableEntity
{
    public int RuleMasterId { get; set; }
    public RuleMaster RuleMaster { get; set; } = null!;

    public int VersionNo { get; set; }

    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }

    [Required]
    public string FormulaExpression { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<RuleCondition> Conditions { get; set; } = new List<RuleCondition>();
}

public sealed class RuleCondition
{
    public int Id { get; set; }
    public int RuleVersionId { get; set; }
    public RuleVersion RuleVersion { get; set; } = null!;

    [Required, MaxLength(100)]
    public string FieldName { get; set; } = string.Empty; // AchievementPercent, GrowthPercent, Branch, Category, DealerType

    [Required, MaxLength(20)]
    public string Operator { get; set; } = string.Empty; // >=, <=, ==, !=, IN

    [Required, MaxLength(200)]
    public string ValueExpression { get; set; } = string.Empty; // 100, 20, HO

    [Required, MaxLength(10)]
    public string LogicalOperator { get; set; } = "AND"; // AND, OR

    public int SortOrder { get; set; }
}

public sealed class ImportTemplate : AuditableEntity
{
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string TargetTable { get; set; } = string.Empty; // MonthlySales, OutstandingRules

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ImportColumn> Columns { get; set; } = new List<ImportColumn>();
    public ICollection<ImportMapping> Mappings { get; set; } = new List<ImportMapping>();
    public ICollection<ImportValidationRule> ValidationRules { get; set; } = new List<ImportValidationRule>();
}

public sealed class ImportColumn
{
    public int Id { get; set; }
    public int ImportTemplateId { get; set; }
    public ImportTemplate ImportTemplate { get; set; } = null!;

    [Required, MaxLength(100)]
    public string ColumnName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string DataType { get; set; } = string.Empty; // String, Decimal, Int, DateTime

    public bool IsRequired { get; set; }

    public int? MaxLength { get; set; }
}

public sealed class ImportMapping
{
    public int Id { get; set; }
    public int ImportTemplateId { get; set; }
    public ImportTemplate ImportTemplate { get; set; } = null!;

    [Required, MaxLength(150)]
    public string SourceHeader { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DestinationColumn { get; set; } = string.Empty;
}

public sealed class ImportValidationRule
{
    public int Id { get; set; }
    public int ImportTemplateId { get; set; }
    public ImportTemplate ImportTemplate { get; set; } = null!;

    [Required, MaxLength(100)]
    public string ColumnName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ValidationType { get; set; } = string.Empty; // Range, Regex, DbLookup

    [Required]
    public string ValidationConfig { get; set; } = string.Empty; // JSON
}

public sealed class WorkflowDefinition : AuditableEntity
{
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
}

public sealed class WorkflowStep
{
    public int Id { get; set; }
    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;

    public int StepNumber { get; set; }

    [Required, MaxLength(100)]
    public string StepName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string RoleAllowed { get; set; } = string.Empty; // Maker, Checker, Approver

    public int RequiredApprovalsCount { get; set; } = 1;

    public int SlaHours { get; set; } = 48;
}

public sealed class WorkflowAssignment : AuditableEntity
{
    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;

    [Required, MaxLength(100)]
    public string TargetEntityId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string TargetEntityType { get; set; } = string.Empty; // BankDetail, IncentivePeriod

    public int CurrentStepNumber { get; set; } = 1;

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

    public bool IsActive { get; set; } = true;

    public DateTime? DueDate { get; set; }

    [MaxLength(100)]
    public string? EscalatedTo { get; set; }

    public DateTime? EscalationDate { get; set; }

    public ICollection<WorkflowHistory> Histories { get; set; } = new List<WorkflowHistory>();
}

public sealed class WorkflowHistory
{
    public int Id { get; set; }
    public int WorkflowAssignmentId { get; set; }
    public WorkflowAssignment WorkflowAssignment { get; set; } = null!;

    public int StepNumber { get; set; }

    [Required, MaxLength(30)]
    public string Action { get; set; } = string.Empty; // Approved, Rejected, Created

    [Required, MaxLength(100)]
    public string PerformedBy { get; set; } = string.Empty;

    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Remarks { get; set; }
}
