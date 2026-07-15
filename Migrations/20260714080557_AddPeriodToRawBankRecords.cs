using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncentivePortal.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodToRawBankRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReconMonth",
                table: "RawBankPaymentRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReconYear",
                table: "RawBankPaymentRecords",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(4329));

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(4741));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(6736));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(7694));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(7716));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(7717));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(7719));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(5668));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(106));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(430));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(431));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(431));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(432));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 14, 8, 5, 56, 572, DateTimeKind.Utc).AddTicks(433));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReconMonth",
                table: "RawBankPaymentRecords");

            migrationBuilder.DropColumn(
                name: "ReconYear",
                table: "RawBankPaymentRecords");

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
        }
    }
}
