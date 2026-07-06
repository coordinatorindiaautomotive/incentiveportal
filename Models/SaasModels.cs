using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IncentivePortal.Models;

[Table("HelpdeskTickets")]
public sealed class HelpdeskTicket : AuditableEntity
{
    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Urgent

    [MaxLength(20)]
    public string Status { get; set; } = "New"; // New, Assigned, InProgress, Resolved, Closed

    [MaxLength(100)]
    public string AssignedTo { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Category { get; set; } = "General"; // Schemes, Billing, Systems, Logistics, General

    [MaxLength(300)]
    public string? AttachmentPath { get; set; }

    public DateTime? SlaExpiry { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }

    [MaxLength(40)]
    public string? AssociatedPartyCode { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
}

[Table("TicketComments")]
public sealed class TicketComment : AuditableEntity
{
    public int TicketId { get; set; }
    public HelpdeskTicket Ticket { get; set; } = default!;

    [Required]
    public string CommentText { get; set; } = string.Empty;

    public bool IsInternal { get; set; }
}

[Table("DocumentItems")]
public sealed class DocumentItem : AuditableEntity
{
    [Required, MaxLength(250)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Category { get; set; } = "General"; // GST, Agreement, Invoice, PAN, Outstanding, General

    public int Version { get; set; } = 1;

    public DateTime? ExpiryDate { get; set; }

    [MaxLength(100)]
    public string Owner { get; set; } = "system";

    public long SizeBytes { get; set; }

    [MaxLength(40)]
    public string? AssociatedPartyCode { get; set; }
}

[Table("CustomerTasks")]
public sealed class CustomerTask : AuditableEntity
{
    [Required, MaxLength(40)]
    public string PartyCode { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Completed, Cancelled

    [MaxLength(20)]
    public string Priority { get; set; } = "Medium"; // Low, Medium, High

    public DateTime? DueDate { get; set; }

    [MaxLength(100)]
    public string AssignedTo { get; set; } = string.Empty;

    [MaxLength(20)]
    public string TaskType { get; set; } = "Call"; // Call, WhatsApp, Visit, Email, General
}

[Table("SystemNotifications")]
public sealed class SystemNotification : AuditableEntity
{
    [Required, MaxLength(100)]
    public string TargetUser { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    [MaxLength(30)]
    public string NotificationType { get; set; } = "System"; // System, Workflow, Task, Escalation
}

[Table("AutomationRules")]
public sealed class AutomationRule : AuditableEntity
{
    [Required, MaxLength(100)]
    public string RuleName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string TriggerType { get; set; } = string.Empty; // OutstandingLimit, ClosingMismatch, SlaWarning

    [Required, MaxLength(50)]
    public string ActionType { get; set; } = string.Empty; // Email, WhatsApp, Notification

    [Required]
    public string ConditionsJson { get; set; } = "{}";

    public bool IsActive { get; set; } = true;
}

[Table("KnowledgeBaseArticles")]
public sealed class KnowledgeBaseArticle : AuditableEntity
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Category { get; set; } = "General";

    public int ViewsCount { get; set; }
}

[Table("CashVerifications")]
public sealed class CashVerification : AuditableEntity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = default!;

    public DateTime VerificationDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningCash { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedClosingCash { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PhysicalClosingCash { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Difference { get; set; }

    [MaxLength(500)]
    public string Remarks { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = "Verified"; // Verified, Mismatch, PendingApproval
}
