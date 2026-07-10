using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IncentivePortal.Models;

[Table("ItMasterDatas")]
public sealed class ItMasterData : AuditableEntity
{
    [Required, MaxLength(100)]
    public string Type { get; set; } = string.Empty; // e.g., "Location", "Department", "Employee", "AssetCategory", etc.

    [Required, MaxLength(100)]
    public string Code { get; set; } = string.Empty; // Unique code within Type

    [Required, MaxLength(250)]
    public string Name { get; set; } = string.Empty;

    public int? ParentId { get; set; }
    
    [ForeignKey("ParentId")]
    public ItMasterData? Parent { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    [MaxLength(500)]
    public string? Description { get; set; }
}

[Table("ItAssets")]
public sealed class ItAsset : AuditableEntity
{
    [Required, MaxLength(40)]
    public string AssetCode { get; set; } = string.Empty; // Auto generated

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int CategoryId { get; set; } // FK to ItMasterData (Category)
    
    public int TypeId { get; set; } // FK to ItMasterData (Type)

    public int BrandId { get; set; } // FK to ItMasterData (Brand)

    public int ModelId { get; set; } // FK to ItMasterData (Model)

    [MaxLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string AssetTag { get; set; } = string.Empty;

    public DateTime PurchaseDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchaseCost { get; set; }

    public int VendorId { get; set; } // FK to ItMasterData (Vendor)

    [MaxLength(100)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime? WarrantyStart { get; set; }
    public DateTime? WarrantyEnd { get; set; }
    public DateTime? AmcStart { get; set; }
    public DateTime? AmcEnd { get; set; }

    public int? AmcProviderId { get; set; } // FK to ItMasterData (AMC Provider)
    public int? WarrantyProviderId { get; set; } // FK to ItMasterData (Warranty Provider)

    public int BranchId { get; set; } // FK to Branches
    
    [ForeignKey("BranchId")]
    public Branch Branch { get; set; } = default!;

    public int LocationId { get; set; } // FK to ItMasterData (Location)
    public int DepartmentId { get; set; } // FK to ItMasterData (Department)
    
    public int? AssignedEmployeeId { get; set; } // FK to ItMasterData (Employee)
    
    public int AssetStatusId { get; set; } // FK to ItMasterData (Status)

    [MaxLength(50)]
    public string Condition { get; set; } = "Good"; // New, Good, Fair, Poor, Scrapped

    public int? CurrentUserId { get; set; } // FK to ItMasterData (Employee)

    [MaxLength(100)]
    public string PurchaseOrder { get; set; } = string.Empty;

    [Column(TypeName = "decimal(9,4)")]
    public decimal DepreciationRatePercent { get; set; }

    [MaxLength(500)]
    public string InsuranceDetails { get; set; } = string.Empty;

    public DateTime? DisposalDate { get; set; }
    public int? DisposalReasonId { get; set; } // FK to ItMasterData (Disposal Reason)

    public int CostCenterId { get; set; } // FK to ItMasterData (Cost Center)

    [MaxLength(1000)]
    public string Remarks { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? AttachmentPath { get; set; }
}

[Table("ItAssetHistories")]
public sealed class ItAssetHistory : AuditableEntity
{
    public int AssetId { get; set; }

    [ForeignKey("AssetId")]
    public ItAsset Asset { get; set; } = default!;

    [Required, MaxLength(50)]
    public string ActionType { get; set; } = string.Empty; // Purchase, Allocate, Transfer, Repair, Disposal, etc.

    public int? FromBranchId { get; set; }
    public int? ToBranchId { get; set; }

    public int? FromUserId { get; set; } // Employee ID
    public int? ToUserId { get; set; } // Employee ID

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(30)]
    public string ApprovalStatus { get; set; } = "Approved"; // Pending, Approved, Rejected

    [MaxLength(100)]
    public string ApprovedBy { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? AttachmentPath { get; set; }

    [MaxLength(1000)]
    public string Details { get; set; } = string.Empty;
}

[Table("ItSoftwareLicenses")]
public sealed class ItSoftwareLicense : AuditableEntity
{
    [Required, MaxLength(150)]
    public string SoftwareName { get; set; } = string.Empty;

    public int VendorId { get; set; } // FK to ItMasterData (Software Vendor)

    [Required, MaxLength(200)]
    public string LicenseKey { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Version { get; set; } = string.Empty;

    public DateTime InstallationDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public int? AssetId { get; set; } // Device on which it is installed

    [ForeignKey("AssetId")]
    public ItAsset? Asset { get; set; }

    public int TotalLicenses { get; set; } = 1;
    public int LicenseTypeId { get; set; } // FK to ItMasterData (License Type)
    public bool IsActive { get; set; } = true;
}

[Table("ItTickets")]
public sealed class ItTicket : AuditableEntity
{
    [Required, MaxLength(40)]
    public string TicketNumber { get; set; } = string.Empty; // Auto generated

    [Required, MaxLength(100)]
    public string Requester { get; set; } = string.Empty; // UserName

    public int BranchId { get; set; }
    
    [ForeignKey("BranchId")]
    public Branch Branch { get; set; } = default!;

    public int DepartmentId { get; set; } // FK to ItMasterData

    public int CategoryId { get; set; } // FK to ItMasterData
    public int SubCategoryId { get; set; } // FK to ItMasterData

    public int PriorityId { get; set; } // FK to ItMasterData
    public int SeverityId { get; set; } // FK to ItMasterData
    public int ImpactId { get; set; } // FK to ItMasterData

    [Required, MaxLength(250)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? AttachmentPath { get; set; }

    [MaxLength(100)]
    public string AssignedEngineer { get; set; } = string.Empty; // UserName

    [Required, MaxLength(30)]
    public string Status { get; set; } = "New"; // New, Assigned, InProgress, WaitingForUser, WaitingForVendor, Resolved, Closed

    public string ResolutionText { get; set; } = string.Empty;

    public int? RootCauseId { get; set; } // FK to ItMasterData
    public int? ResolutionTypeId { get; set; } // FK to ItMasterData

    public DateTime? ClosureDate { get; set; }

    public int? UserFeedbackScore { get; set; } // 1 to 5 stars

    [MaxLength(500)]
    public string? UserFeedbackText { get; set; }

    public DateTime SlaDeadline { get; set; }
    public bool SlaBreached { get; set; } = false;

    public ICollection<ItTicketComment> Comments { get; set; } = new List<ItTicketComment>();
}

[Table("ItTicketComments")]
public sealed class ItTicketComment : AuditableEntity
{
    public int TicketId { get; set; }

    [ForeignKey("TicketId")]
    public ItTicket Ticket { get; set; } = default!;

    [Required]
    public string CommentText { get; set; } = string.Empty;

    public bool IsInternal { get; set; }
}

[Table("ItSlaPolicies")]
public sealed class ItSlaPolicy : AuditableEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int PriorityId { get; set; } // FK to ItMasterData

    public int ResponseTimeHours { get; set; }
    public int ResolutionTimeHours { get; set; }

    public bool IsActive { get; set; } = true;
}

[Table("ItMaintenanceSchedules")]
public sealed class ItMaintenanceSchedule : AuditableEntity
{
    public int AssetId { get; set; }

    [ForeignKey("AssetId")]
    public ItAsset Asset { get; set; } = default!;

    [Required, MaxLength(30)]
    public string Frequency { get; set; } = "Quarterly"; // Monthly, Quarterly, Annually

    public DateTime LastDoneDate { get; set; }
    public DateTime NextDueDate { get; set; }

    [MaxLength(100)]
    public string AssignedEngineer { get; set; } = string.Empty; // UserName

    public string ChecklistJson { get; set; } = "[]";

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Pending"; // Pending, Completed
}

[Table("ItKbArticles")]
public sealed class ItKbArticle : AuditableEntity
{
    [Required, MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public int CategoryId { get; set; } // FK to ItMasterData

    [MaxLength(200)]
    public string Tags { get; set; } = string.Empty;

    public int ViewsCount { get; set; } = 0;
}
