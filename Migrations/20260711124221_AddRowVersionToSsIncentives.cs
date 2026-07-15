using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncentivePortal.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersionToSsIncentives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ssincentives",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ssincentives");

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(8079));

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(8582));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 570, DateTimeKind.Utc).AddTicks(389));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 570, DateTimeKind.Utc).AddTicks(1453));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 570, DateTimeKind.Utc).AddTicks(1486));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 570, DateTimeKind.Utc).AddTicks(1488));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemeDetails",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 570, DateTimeKind.Utc).AddTicks(1490));

            migrationBuilder.UpdateData(
                table: "IncentiveSchemes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(9190));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(2805));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(3667));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(3669));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(3670));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(3670));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 7, 11, 8, 52, 14, 569, DateTimeKind.Utc).AddTicks(3671));
        }
    }
}
