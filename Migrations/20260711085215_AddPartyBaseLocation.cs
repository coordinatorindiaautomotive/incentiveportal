using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IncentivePortal.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyBaseLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConditionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BranchType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Consignee = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Incharge = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MobileNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmailID = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OpeningYear = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Longitude = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Latitude = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AllowedCategories = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AllowedPartyTypes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TallyOutletCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashMasterItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMasterItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashPeriodControls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ControlYear = table.Column<int>(type: "int", nullable: false),
                    ControlMonth = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnlockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashPeriodControls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategorySalesAggregates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    MonthYear = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Quarter = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PartyType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PartCategoryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Loc = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DealerSubType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NetSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetDdl = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Transactions = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorySalesAggregates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ColumnMappingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExcelHeader = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PortalField = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UploadContext = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColumnMappingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CostCenterCashes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    CostCenterName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCenterCashes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedTo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TaskType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomReportLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SelectedFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PivotRowsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PivotColumnsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PivotValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FiltersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GroupsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomReportLayouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DealerOutstandings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    MonthLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PartyName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Outstanding = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingLess7Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding7To14Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding14To21Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding21To28Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding28To35Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding35To50Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding50To80Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OutstandingMore80Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerOutstandings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DealerTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    PartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SystemSuggestedTarget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdminDefinedTarget = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FinalTarget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AssociatedPartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalIncentiveUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    MonthLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIncentiveUploads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpdeskTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AttachmentPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SlaExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AssociatedPartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpdeskTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HelpTexts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Page = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpTexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    SuccessRows = table.Column<int>(type: "int", nullable: false),
                    FailedRows = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsHistorical = table.Column<bool>(type: "bit", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PreviousImportLogId = table.Column<int>(type: "int", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportLogs_ImportLogs_PreviousImportLogId",
                        column: x => x.PreviousImportLogId,
                        principalTable: "ImportLogs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ImportTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TargetTable = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncentivePeriodLocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PartCategoryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IncentiveSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LockStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LockedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnlockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UnlockRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncentivePeriodLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncentivePeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LockedFlag = table.Column<bool>(type: "bit", nullable: false),
                    LockedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LockedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncentivePeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncentiveSchemes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SchemeMonth = table.Column<int>(type: "int", nullable: false),
                    SchemeYear = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncentiveSchemes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItKbArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ViewsCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItKbArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItMasterDatas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItMasterDatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItMasterDatas_ItMasterDatas_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ItMasterDatas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ItSlaPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PriorityId = table.Column<int>(type: "int", nullable: false),
                    ResponseTimeHours = table.Column<int>(type: "int", nullable: false),
                    ResolutionTimeHours = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItSlaPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ViewsCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonthLocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LockYear = table.Column<int>(type: "int", nullable: false),
                    LockMonth = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnlockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SmtpHost = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SmtpPort = table.Column<int>(type: "int", nullable: false),
                    SmtpUseSsl = table.Column<bool>(type: "bit", nullable: false),
                    SmtpUser = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SmtpPassEncrypted = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FromEmail = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    FromName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SmsApiKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SmsSenderId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmailEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SmsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutstandingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DeductionRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutstandingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartyCodeMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AlternativeCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyCodeMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartyExecutiveMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PartyName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    ExecutiveCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExecutiveName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyExecutiveMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartyPrimaryBranch",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PrimaryBranchCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    TotalSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastRefreshedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyPrimaryBranch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortalSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductCodeMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AlternativeCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCodeMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportColumnConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ColumnKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportColumnConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RuleType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TallyIntegrationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PasswordEncrypted = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TallyIntegrationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TdsRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnnualThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RateWithPan = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    RateNoPan = table.Column<decimal>(type: "decimal(6,4)", nullable: false),
                    Section = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TdsRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssetItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AssetCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ModelNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurchaseCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Vendor = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DepreciationRatePercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssetLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WarrantyExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetItems_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExceptionRef = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ExceptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashExceptions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashInTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiptType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    DealerCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Narration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AttachmentPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TallyVoucherNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TallyVoucherType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TallyLedgerName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TallyGuid = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TallySyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TallySyncStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ApprovalRemarks = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashInTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashInTransactions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashOutTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpenseCategory = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    CostCenter = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    GlAccount = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PaymentInstrument = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReferenceNo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Narration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AttachmentPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TallyVoucherNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TallyGuid = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TallySyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TallySyncStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ApprovalRemarks = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashOutTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashOutTransactions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashVerifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    VerificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpeningCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedClosingCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PhysicalClosingCash = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashVerifications_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    TypeId = table.Column<int>(type: "int", nullable: false),
                    BrandId = table.Column<int>(type: "int", nullable: false),
                    ModelId = table.Column<int>(type: "int", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssetTag = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PurchaseCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WarrantyStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WarrantyEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AmcStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AmcEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AmcProviderId = table.Column<int>(type: "int", nullable: true),
                    WarrantyProviderId = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    AssignedEmployeeId = table.Column<int>(type: "int", nullable: true),
                    AssetStatusId = table.Column<int>(type: "int", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentUserId = table.Column<int>(type: "int", nullable: true),
                    PurchaseOrder = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DepreciationRatePercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    InsuranceDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisposalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisposalReasonId = table.Column<int>(type: "int", nullable: true),
                    CostCenterId = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AttachmentPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItAssets_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Requester = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    SubCategoryId = table.Column<int>(type: "int", nullable: false),
                    PriorityId = table.Column<int>(type: "int", nullable: false),
                    SeverityId = table.Column<int>(type: "int", nullable: false),
                    ImpactId = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AssignedEngineer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResolutionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RootCauseId = table.Column<int>(type: "int", nullable: true),
                    ResolutionTypeId = table.Column<int>(type: "int", nullable: true),
                    ClosureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserFeedbackScore = table.Column<int>(type: "int", nullable: true),
                    UserFeedbackText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SlaDeadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlaBreached = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItTickets_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PartyName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    GST = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DealerType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FixedIncentivePercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    OriginalPartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AutoBaseBranchId = table.Column<int>(type: "int", nullable: true),
                    IsManuallyMapped = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parties_Branches_AutoBaseBranchId",
                        column: x => x.AutoBaseBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Parties_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordSalt = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedLoginAttempts = table.Column<int>(type: "int", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReportSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LayoutId = table.Column<int>(type: "int", nullable: false),
                    RecipientEmails = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastRunJobId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportSchedules_CustomReportLayouts_LayoutId",
                        column: x => x.LayoutId,
                        principalTable: "CustomReportLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalIncentiveRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    MonthLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConsPartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ConsPartyName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    NetRetailSelling = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Slab = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Incentive = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIncentiveRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalIncentiveRecords_ExternalIncentiveUploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "ExternalIncentiveUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    CommentText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketComments_HelpdeskTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "HelpdeskTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IFSC = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UTR = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PartyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsReconciled = table.Column<bool>(type: "bit", nullable: false),
                    RawRowJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImportLogId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementRecords_ImportLogs_ImportLogId",
                        column: x => x.ImportLogId,
                        principalTable: "ImportLogs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Raw",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DealerSubType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Consignee = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    DealerCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Loc = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PartCategoryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FiscalYear = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Quarter = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Month = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MonthYear = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ConsPartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ConsPartyName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    PartyType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DocumentNum = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NetRetailSelling = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetRetailDdl = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OriginalCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ImportLogId = table.Column<int>(type: "int", nullable: true),
                    RowNumber = table.Column<int>(type: "int", nullable: true),
                    MonthNumber = table.Column<int>(type: "int", nullable: true),
                    YearNumber = table.Column<int>(type: "int", nullable: true),
                    AchievementPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PartNum = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RootPartNum = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Day = table.Column<int>(type: "int", nullable: true),
                    NetRetailQty = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Raw", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Raw_ImportLogs_ImportLogId",
                        column: x => x.ImportLogId,
                        principalTable: "ImportLogs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ssincentives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    MonthLabel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PartyCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PartyName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    SaleValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SlabPercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    OnBillDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AchievementPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossIncentive = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TdsAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetTransferAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransferredAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Outstanding = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingLess7Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding7To14Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding14To21Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding21To28Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding28To35Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding35To50Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Outstanding50To80Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OutstandingMore80Days = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ProcessingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UTRNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PartCategoryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceLocation = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IFSC = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImportLogId = table.Column<int>(type: "int", nullable: true),
                    TdsNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IncentiveType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ApplicableSlab = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ssincentives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ssincentives_ImportLogs_ImportLogId",
                        column: x => x.ImportLogId,
                        principalTable: "ImportLogs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ImportColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportTemplateId = table.Column<int>(type: "int", nullable: false),
                    ColumnName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    MaxLength = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportColumns_ImportTemplates_ImportTemplateId",
                        column: x => x.ImportTemplateId,
                        principalTable: "ImportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportTemplateId = table.Column<int>(type: "int", nullable: false),
                    SourceHeader = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DestinationColumn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportMappings_ImportTemplates_ImportTemplateId",
                        column: x => x.ImportTemplateId,
                        principalTable: "ImportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportValidationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportTemplateId = table.Column<int>(type: "int", nullable: false),
                    ColumnName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValidationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ValidationConfig = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportValidationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportValidationRules_ImportTemplates_ImportTemplateId",
                        column: x => x.ImportTemplateId,
                        principalTable: "ImportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncentiveSchemeDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncentiveSchemeId = table.Column<int>(type: "int", nullable: false),
                    MinAchievementPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxAchievementPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FixedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Percentage = table.Column<decimal>(type: "decimal(9,4)", nullable: true),
                    RuleName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncentiveSchemeDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncentiveSchemeDetails_IncentiveSchemes_IncentiveSchemeId",
                        column: x => x.IncentiveSchemeId,
                        principalTable: "IncentiveSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuleVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleMasterId = table.Column<int>(type: "int", nullable: false),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FormulaExpression = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleVersions_RuleMasters_RuleMasterId",
                        column: x => x.RuleMasterId,
                        principalTable: "RuleMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: false),
                    TargetEntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrentStepNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalatedTo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EscalationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowAssignments_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: false),
                    StepNumber = table.Column<int>(type: "int", nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoleAllowed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiredApprovalsCount = table.Column<int>(type: "int", nullable: false),
                    SlaHours = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowSteps_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CashReconRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReconRef = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ReconDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CashInId = table.Column<int>(type: "int", nullable: true),
                    CashOutId = table.Column<int>(type: "int", nullable: true),
                    PortalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TallyVoucherNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    TallyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Variance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReconStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashReconRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashReconRecords_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashReconRecords_CashInTransactions_CashInId",
                        column: x => x.CashInId,
                        principalTable: "CashInTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashReconRecords_CashOutTransactions_CashOutId",
                        column: x => x.CashOutId,
                        principalTable: "CashOutTransactions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ItAssetHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FromBranchId = table.Column<int>(type: "int", nullable: true),
                    ToBranchId = table.Column<int>(type: "int", nullable: true),
                    FromUserId = table.Column<int>(type: "int", nullable: true),
                    ToUserId = table.Column<int>(type: "int", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AttachmentPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItAssetHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItAssetHistories_ItAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "ItAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItMaintenanceSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LastDoneDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextDueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedEngineer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChecklistJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItMaintenanceSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItMaintenanceSchedules_ItAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "ItAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItSoftwareLicenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SoftwareName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    LicenseKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InstallationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssetId = table.Column<int>(type: "int", nullable: true),
                    TotalLicenses = table.Column<int>(type: "int", nullable: false),
                    LicenseTypeId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItSoftwareLicenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItSoftwareLicenses_ItAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "ItAssets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ItTicketComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    CommentText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItTicketComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItTicketComments_ItTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "ItTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyId = table.Column<int>(type: "int", nullable: false),
                    AccountHolder = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IFSC = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    PAN = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankDetails_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealerGrowthAnalytics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    SalesCurrent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalesPriorMonth = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalesPriorYearSamePeriod = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IncentiveCurrent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IncentivePriorMonth = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IncentivePriorYearSamePeriod = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalesGrowthMoM = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    SalesGrowthYoY = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    IncentiveGrowthMoM = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    IncentiveGrowthYoY = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerGrowthAnalytics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealerGrowthAnalytics_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealerMonthlyPerformances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    TotalSales = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SlabPercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    IncentiveEarned = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Outstanding = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrowthTrend = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerMonthlyPerformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealerMonthlyPerformances_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DealerSlabProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    CurrentSale = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NextSlabPercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    NextSlabTarget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProgressPercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    RemainingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealerSlabProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DealerSlabProgresses_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncentiveSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    CurrentSale = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentSlabPercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    CurrentIncentive = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NextSlabTarget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdditionalPurchaseRequired = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NextIncentive = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ForecastedIncentive = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncentiveSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncentiveSummaries_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RuleConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleVersionId = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValueExpression = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LogicalOperator = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuleConditions_RuleVersions_RuleVersionId",
                        column: x => x.RuleVersionId,
                        principalTable: "RuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowAssignmentId = table.Column<int>(type: "int", nullable: false),
                    StepNumber = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowHistories_WorkflowAssignments_WorkflowAssignmentId",
                        column: x => x.WorkflowAssignmentId,
                        principalTable: "WorkflowAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartyId = table.Column<int>(type: "int", nullable: false),
                    BankDetailId = table.Column<int>(type: "int", nullable: true),
                    RequestType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OldJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankApprovalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankApprovalRequests_BankDetails_BankDetailId",
                        column: x => x.BankDetailId,
                        principalTable: "BankDetails",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BankApprovalRequests_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "Address", "AllowedCategories", "AllowedPartyTypes", "Area", "BranchType", "Code", "Consignee", "CreatedAt", "CreatedBy", "EmailID", "Incharge", "IsDeleted", "Latitude", "Longitude", "MobileNo", "Name", "OpeningYear", "Region", "Status", "TallyOutletCode", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "", "AA,M", "INDEPENDENT WORKSHOP", "", "", "HO", "", new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(8079), "system", "", "", false, "", "", "", "Head Office", "", "Corporate", "Active", "", null, null },
                    { 2, "", "AA,M", "INDEPENDENT WORKSHOP", "", "", "BR-N", "", new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(8582), "system", "", "", false, "", "", "", "North Branch", "", "North", "Active", "", null, null }
                });

            migrationBuilder.InsertData(
                table: "IncentiveSchemes",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "EffectiveFrom", "EffectiveTo", "IsDeleted", "IsLocked", "Name", "SchemeMonth", "SchemeYear", "UpdatedAt", "UpdatedBy", "Version" },
                values: new object[] { 1, new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(9190), "system", new DateTime(2026, 5, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 31, 23, 59, 59, 0, DateTimeKind.Utc), false, false, "Standard Sales Value Scheme", 5, 2026, null, null, 1 });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "IsDeleted", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(2805), "system", false, "Super Admin", null, null },
                    { 2, new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(3667), "system", false, "HO Finance", null, null },
                    { 3, new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(3669), "system", false, "Branch Manager", null, null },
                    { 4, new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(3670), "system", false, "Associate", null, null },
                    { 5, new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(3670), "system", false, "Auditor", null, null },
                    { 6, new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(3671), "system", false, "Sales Executive", null, null }
                });

            migrationBuilder.InsertData(
                table: "IncentiveSchemeDetails",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "FixedAmount", "IncentiveSchemeId", "IsDeleted", "MaxAchievementPercent", "MinAchievementPercent", "Percentage", "RuleName", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 7, 11, 8, 52, 14, 570, DateTimeKind.Utc).AddTicks(389), "system", 0m, 1, false, 29999m, 0m, 0m, "Slab 1", null, null },
                    { 2, new DateTime(2026, 7, 11, 8, 52, 14, 570, DateTimeKind.Utc).AddTicks(1453), "system", 0m, 1, false, 49999m, 30000m, 3m, "Slab 2", null, null },
                    { 3, new DateTime(2026, 7, 11, 8, 52, 14, 570, DateTimeKind.Utc).AddTicks(1486), "system", 0m, 1, false, 74999m, 50000m, 4m, "Slab 3", null, null },
                    { 4, new DateTime(2026, 7, 11, 8, 52, 14, 570, DateTimeKind.Utc).AddTicks(1488), "system", 0m, 1, false, 119999m, 75000m, 5m, "Slab 4", null, null },
                    { 5, new DateTime(2026, 7, 11, 8, 52, 14, 570, DateTimeKind.Utc).AddTicks(1490), "system", 0m, 1, false, 999999999m, 120000m, 6m, "Slab 5", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetItems_BranchId_AssetCode",
                table: "AssetItems",
                columns: new[] { "BranchId", "AssetCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BankApprovalRequests_BankDetailId",
                table: "BankApprovalRequests",
                column: "BankDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_BankApprovalRequests_PartyId",
                table: "BankApprovalRequests",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_BankDetails_AccountNumber",
                table: "BankDetails",
                column: "AccountNumber",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BankDetails_PartyId",
                table: "BankDetails",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementRecords_ImportLogId",
                table: "BankStatementRecords",
                column: "ImportLogId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Code",
                table: "Branches",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashExceptions_BranchId",
                table: "CashExceptions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashInTransactions_BranchId",
                table: "CashInTransactions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashOutTransactions_BranchId",
                table: "CashOutTransactions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashReconRecords_BranchId",
                table: "CashReconRecords",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashReconRecords_CashInId",
                table: "CashReconRecords",
                column: "CashInId");

            migrationBuilder.CreateIndex(
                name: "IX_CashReconRecords_CashOutId",
                table: "CashReconRecords",
                column: "CashOutId");

            migrationBuilder.CreateIndex(
                name: "IX_CashVerifications_BranchId",
                table: "CashVerifications",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ColumnMappingRules_ExcelHeader_UploadContext",
                table: "ColumnMappingRules",
                columns: new[] { "ExcelHeader", "UploadContext" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenterCashes_Year_Month_CostCenterName",
                table: "CostCenterCashes",
                columns: new[] { "Year", "Month", "CostCenterName" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DealerGrowthAnalytics_PartyId",
                table: "DealerGrowthAnalytics",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_DealerGrowthAnalytics_Year_Month_PartyId",
                table: "DealerGrowthAnalytics",
                columns: new[] { "Year", "Month", "PartyId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DealerMonthlyPerformances_PartyId",
                table: "DealerMonthlyPerformances",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_DealerMonthlyPerformances_Year_Month_PartyId",
                table: "DealerMonthlyPerformances",
                columns: new[] { "Year", "Month", "PartyId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DealerOutstandings_Year_Month_PartyCode",
                table: "DealerOutstandings",
                columns: new[] { "Year", "Month", "PartyCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DealerSlabProgresses_PartyId",
                table: "DealerSlabProgresses",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_DealerSlabProgresses_Year_Month_PartyId",
                table: "DealerSlabProgresses",
                columns: new[] { "Year", "Month", "PartyId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIncentiveRecords_UploadId",
                table: "ExternalIncentiveRecords",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIncentiveRecords_Year_Month_ConsPartyCode_UploadId",
                table: "ExternalIncentiveRecords",
                columns: new[] { "Year", "Month", "ConsPartyCode", "UploadId" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ImportColumns_ImportTemplateId",
                table: "ImportColumns",
                column: "ImportTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLogs_PreviousImportLogId",
                table: "ImportLogs",
                column: "PreviousImportLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportMappings_ImportTemplateId",
                table: "ImportMappings",
                column: "ImportTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportTemplates_Code",
                table: "ImportTemplates",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ImportValidationRules_ImportTemplateId",
                table: "ImportValidationRules",
                column: "ImportTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_IncentivePeriods_Year_Month",
                table: "IncentivePeriods",
                columns: new[] { "Year", "Month" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IncentiveSchemeDetails_IncentiveSchemeId",
                table: "IncentiveSchemeDetails",
                column: "IncentiveSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_IncentiveSchemes_SchemeYear_SchemeMonth_Version",
                table: "IncentiveSchemes",
                columns: new[] { "SchemeYear", "SchemeMonth", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IncentiveSummaries_PartyId",
                table: "IncentiveSummaries",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_IncentiveSummaries_Year_Month_PartyId",
                table: "IncentiveSummaries",
                columns: new[] { "Year", "Month", "PartyId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ItAssetHistories_AssetId",
                table: "ItAssetHistories",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ItAssets_BranchId",
                table: "ItAssets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ItMaintenanceSchedules_AssetId",
                table: "ItMaintenanceSchedules",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ItMasterDatas_ParentId",
                table: "ItMasterDatas",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ItSoftwareLicenses_AssetId",
                table: "ItSoftwareLicenses",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ItTicketComments_TicketId",
                table: "ItTicketComments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_ItTickets_BranchId",
                table: "ItTickets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthLocks_LockYear_LockMonth",
                table: "MonthLocks",
                columns: new[] { "LockYear", "LockMonth" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_AutoBaseBranchId",
                table: "Parties",
                column: "AutoBaseBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_BranchId",
                table: "Parties",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Parties_PartyCode",
                table: "Parties",
                column: "PartyCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyCodeMappings_AlternativeCode",
                table: "PartyCodeMappings",
                column: "AlternativeCode",
                unique: true,
                filter: "[IsDeleted] = 0 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PartyCodeMappings_OriginalCode",
                table: "PartyCodeMappings",
                column: "OriginalCode",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PartyPrimaryBranch_PartyCode",
                table: "PartyPrimaryBranch",
                column: "PartyCode",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Raw_ImportLogId",
                table: "Raw",
                column: "ImportLogId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Raw_Loc",
                table: "Raw",
                column: "Loc",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Raw_OriginalCode",
                table: "Raw",
                column: "OriginalCode",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Raw_Year_Month",
                table: "Raw",
                columns: new[] { "YearNumber", "MonthNumber" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Raw_YearNumber_MonthNumber_ConsPartyCode",
                table: "Raw",
                columns: new[] { "YearNumber", "MonthNumber", "ConsPartyCode" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ReportColumnConfigs_ReportName_ColumnKey",
                table: "ReportColumnConfigs",
                columns: new[] { "ReportName", "ColumnKey" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_LayoutId",
                table: "ReportSchedules",
                column: "LayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleName_Module_Action",
                table: "RolePermissions",
                columns: new[] { "RoleName", "Module", "Action" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleConditions_RuleVersionId",
                table: "RuleConditions",
                column: "RuleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RuleMasters_Code",
                table: "RuleMasters",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RuleVersions_RuleMasterId_VersionNo",
                table: "RuleVersions",
                columns: new[] { "RuleMasterId", "VersionNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ssincentives_ImportLogId",
                table: "ssincentives",
                column: "ImportLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ssincentives_Status",
                table: "ssincentives",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ssincentives_Year_Month_PartyCode_PartCategoryCode",
                table: "ssincentives",
                columns: new[] { "Year", "Month", "PartyCode", "PartCategoryCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TdsRules_EffectiveFrom_EffectiveTo",
                table: "TdsRules",
                columns: new[] { "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketComments_TicketId",
                table: "TicketComments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_BranchId",
                table: "Users",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAssignments_TargetEntityId_TargetEntityType_WorkflowDefinitionId",
                table: "WorkflowAssignments",
                columns: new[] { "TargetEntityId", "TargetEntityType", "WorkflowDefinitionId" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAssignments_WorkflowDefinitionId",
                table: "WorkflowAssignments",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_Code",
                table: "WorkflowDefinitions",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowHistories_WorkflowAssignmentId",
                table: "WorkflowHistories",
                column: "WorkflowAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_WorkflowDefinitionId",
                table: "WorkflowSteps",
                column: "WorkflowDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "AssetItems");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "AutomationRules");

            migrationBuilder.DropTable(
                name: "BankApprovalRequests");

            migrationBuilder.DropTable(
                name: "BankStatementRecords");

            migrationBuilder.DropTable(
                name: "CashExceptions");

            migrationBuilder.DropTable(
                name: "CashMasterItems");

            migrationBuilder.DropTable(
                name: "CashPeriodControls");

            migrationBuilder.DropTable(
                name: "CashReconRecords");

            migrationBuilder.DropTable(
                name: "CashVerifications");

            migrationBuilder.DropTable(
                name: "CategorySalesAggregates");

            migrationBuilder.DropTable(
                name: "ColumnMappingRules");

            migrationBuilder.DropTable(
                name: "CostCenterCashes");

            migrationBuilder.DropTable(
                name: "CustomerTasks");

            migrationBuilder.DropTable(
                name: "DealerGrowthAnalytics");

            migrationBuilder.DropTable(
                name: "DealerMonthlyPerformances");

            migrationBuilder.DropTable(
                name: "DealerOutstandings");

            migrationBuilder.DropTable(
                name: "DealerSlabProgresses");

            migrationBuilder.DropTable(
                name: "DealerTargets");

            migrationBuilder.DropTable(
                name: "DocumentItems");

            migrationBuilder.DropTable(
                name: "ExternalIncentiveRecords");

            migrationBuilder.DropTable(
                name: "HelpTexts");

            migrationBuilder.DropTable(
                name: "ImportColumns");

            migrationBuilder.DropTable(
                name: "ImportMappings");

            migrationBuilder.DropTable(
                name: "ImportValidationRules");

            migrationBuilder.DropTable(
                name: "IncentivePeriodLocks");

            migrationBuilder.DropTable(
                name: "IncentivePeriods");

            migrationBuilder.DropTable(
                name: "IncentiveSchemeDetails");

            migrationBuilder.DropTable(
                name: "IncentiveSummaries");

            migrationBuilder.DropTable(
                name: "ItAssetHistories");

            migrationBuilder.DropTable(
                name: "ItKbArticles");

            migrationBuilder.DropTable(
                name: "ItMaintenanceSchedules");

            migrationBuilder.DropTable(
                name: "ItMasterDatas");

            migrationBuilder.DropTable(
                name: "ItSlaPolicies");

            migrationBuilder.DropTable(
                name: "ItSoftwareLicenses");

            migrationBuilder.DropTable(
                name: "ItTicketComments");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseArticles");

            migrationBuilder.DropTable(
                name: "MonthLocks");

            migrationBuilder.DropTable(
                name: "NotificationSettings");

            migrationBuilder.DropTable(
                name: "OutstandingRules");

            migrationBuilder.DropTable(
                name: "PartyCodeMappings");

            migrationBuilder.DropTable(
                name: "PartyExecutiveMappings");

            migrationBuilder.DropTable(
                name: "PartyPrimaryBranch");

            migrationBuilder.DropTable(
                name: "PortalSettings");

            migrationBuilder.DropTable(
                name: "ProductCodeMappings");

            migrationBuilder.DropTable(
                name: "Raw");

            migrationBuilder.DropTable(
                name: "ReportColumnConfigs");

            migrationBuilder.DropTable(
                name: "ReportSchedules");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "RuleConditions");

            migrationBuilder.DropTable(
                name: "ssincentives");

            migrationBuilder.DropTable(
                name: "SystemNotifications");

            migrationBuilder.DropTable(
                name: "TallyIntegrationSettings");

            migrationBuilder.DropTable(
                name: "TdsRules");

            migrationBuilder.DropTable(
                name: "TicketComments");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "WorkflowHistories");

            migrationBuilder.DropTable(
                name: "WorkflowSteps");

            migrationBuilder.DropTable(
                name: "BankDetails");

            migrationBuilder.DropTable(
                name: "CashInTransactions");

            migrationBuilder.DropTable(
                name: "CashOutTransactions");

            migrationBuilder.DropTable(
                name: "ExternalIncentiveUploads");

            migrationBuilder.DropTable(
                name: "ImportTemplates");

            migrationBuilder.DropTable(
                name: "IncentiveSchemes");

            migrationBuilder.DropTable(
                name: "ItAssets");

            migrationBuilder.DropTable(
                name: "ItTickets");

            migrationBuilder.DropTable(
                name: "CustomReportLayouts");

            migrationBuilder.DropTable(
                name: "RuleVersions");

            migrationBuilder.DropTable(
                name: "ImportLogs");

            migrationBuilder.DropTable(
                name: "HelpdeskTickets");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "WorkflowAssignments");

            migrationBuilder.DropTable(
                name: "Parties");

            migrationBuilder.DropTable(
                name: "RuleMasters");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "Branches");
        }
    }
}
