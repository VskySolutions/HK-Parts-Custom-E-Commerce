using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAuditScanDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientIp",
                table: "InventoryAuditLogs",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchedBy",
                table: "InventoryAuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "InventoryAuditLogs",
                type: "int",
                nullable: false,
                defaultValue: 1); // InventoryScanSource.Camera for rows logged before this column existed

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "InventoryAuditLogs",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryAuditLogs_NonNegative",
                table: "InventoryAuditLogs",
                sql: "([NewQty] IS NULL OR [NewQty] >= 0) AND ([NewPrice] IS NULL OR [NewPrice] >= 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryAuditLogs_NonNegative",
                table: "InventoryAuditLogs");

            migrationBuilder.DropColumn(
                name: "ClientIp",
                table: "InventoryAuditLogs");

            migrationBuilder.DropColumn(
                name: "MatchedBy",
                table: "InventoryAuditLogs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "InventoryAuditLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "InventoryAuditLogs");
        }
    }
}
