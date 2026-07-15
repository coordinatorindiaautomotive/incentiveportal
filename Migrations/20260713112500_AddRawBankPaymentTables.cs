using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncentivePortal.Migrations
{
    /// <inheritdoc />
    public partial class AddRawBankPaymentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankPaymentImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchRef = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalRecords = table.Column<int>(type: "int", nullable: false),
                    ImportedRecords = table.Column<int>(type: "int", nullable: false),
                    DuplicateRecords = table.Column<int>(type: "int", nullable: false),
                    FailedRecords = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImportRemarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsForcedReimport = table.Column<bool>(type: "bit", nullable: false),
                    ForcedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailedRowsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankPaymentImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RawBankPaymentRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    FileSequenceNum = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PymtProdTypeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PymtMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DebitAcctNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BeneficiaryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BeneficiaryAccountNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BeneIfscCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DebitNarration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreditNarration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EmailId = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PymtDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReferenceNo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AddlInfo1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddlInfo2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddlInfo3 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddlInfo4 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddlInfo5 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BeneficiaryLei = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    BankStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CurrentStep = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    BankFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    RejectedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AcctDebitDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerRefNo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    UtrNo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawBankPaymentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawBankPaymentRecords_BankPaymentImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "BankPaymentImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 262, DateTimeKind.Utc).AddTicks(9707));

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 263, DateTimeKind.Utc).AddTicks(123));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 263, DateTimeKind.Utc).AddTicks(1838));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 263, DateTimeKind.Utc).AddTicks(2731));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 263, DateTimeKind.Utc).AddTicks(2752));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 263, DateTimeKind.Utc).AddTicks(2753));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 263, DateTimeKind.Utc).AddTicks(2764));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 263, DateTimeKind.Utc).AddTicks(749));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 262, DateTimeKind.Utc).AddTicks(4862));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 262, DateTimeKind.Utc).AddTicks(5193));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 262, DateTimeKind.Utc).AddTicks(5194));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 262, DateTimeKind.Utc).AddTicks(5195));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 262, DateTimeKind.Utc).AddTicks(5195));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 13, 11, 24, 58, 262, DateTimeKind.Utc).AddTicks(5196));

            migrationBuilder.CreateIndex(
                name: "IX_SsIncentives_Ledger_Covering",
                table: "ssincentives",
                columns: new[] { "Status", "Year", "Month", "SourceLocation" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BankPaymentBatch_BatchRef",
                table: "BankPaymentImportBatches",
                column: "BatchRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankPaymentBatch_FileName",
                table: "BankPaymentImportBatches",
                column: "OriginalFileName",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RawBankPayment_BatchId",
                table: "RawBankPaymentRecords",
                column: "BatchId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RawBankPayment_SeqNum_Utr",
                table: "RawBankPaymentRecords",
                columns: new[] { "FileSequenceNum", "UtrNo" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawBankPaymentRecords");

            migrationBuilder.DropTable(
                name: "BankPaymentImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_SsIncentives_Ledger_Covering",
                table: "ssincentives");

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 112, DateTimeKind.Utc).AddTicks(2415));

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 112, DateTimeKind.Utc).AddTicks(2911));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 112, DateTimeKind.Utc).AddTicks(4755));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 112, DateTimeKind.Utc).AddTicks(5863));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 112, DateTimeKind.Utc).AddTicks(5887));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 112, DateTimeKind.Utc).AddTicks(5888));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 112, DateTimeKind.Utc).AddTicks(5890));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 112, DateTimeKind.Utc).AddTicks(3523));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 111, DateTimeKind.Utc).AddTicks(7968));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 111, DateTimeKind.Utc).AddTicks(8345));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 111, DateTimeKind.Utc).AddTicks(8346));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 111, DateTimeKind.Utc).AddTicks(8347));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 111, DateTimeKind.Utc).AddTicks(8347));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 12, 42, 20, 111, DateTimeKind.Utc).AddTicks(8348));
        }
    }
}
