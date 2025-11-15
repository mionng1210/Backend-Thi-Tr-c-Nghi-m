using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionRequestBankPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankAccountName",
                table: "PermissionRequests",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "PermissionRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "PermissionRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaymentAmount",
                table: "PermissionRequests",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "PermissionRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "PermissionRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "PermissionRequests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 6, 4, 3, 38, 196, DateTimeKind.Utc).AddTicks(5232));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 6, 4, 3, 38, 196, DateTimeKind.Utc).AddTicks(5233));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 6, 4, 3, 38, 196, DateTimeKind.Utc).AddTicks(5235));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankAccountName",
                table: "PermissionRequests");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "PermissionRequests");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "PermissionRequests");

            migrationBuilder.DropColumn(
                name: "PaymentAmount",
                table: "PermissionRequests");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "PermissionRequests");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "PermissionRequests");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "PermissionRequests");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 6, 3, 36, 1, 355, DateTimeKind.Utc).AddTicks(6722));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 6, 3, 36, 1, 355, DateTimeKind.Utc).AddTicks(6724));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 11, 6, 3, 36, 1, 355, DateTimeKind.Utc).AddTicks(6725));
        }
    }
}
