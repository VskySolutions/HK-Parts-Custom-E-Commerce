using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkBlacklistEntryToCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "CustomerBlacklist",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBlacklist_CustomerId",
                table: "CustomerBlacklist",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerBlacklist_Customers_CustomerId",
                table: "CustomerBlacklist",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            // Backfill: customers already blocked from the Customers list get their Blocked Customers entry
            // (Status 0 = Blocked), so they show on that page and are refused at checkout.
            migrationBuilder.Sql(@"
INSERT INTO CustomerBlacklist (Id, CustomerId, FirstName, LastName, Email, Reason, Status, CreatedById, CreatedOnUtc, UpdatedById, UpdatedOnUtc)
SELECT NEWID(), b.CustomerId,
       NULLIF(LTRIM(RTRIM(LEFT(c.FirstName, 100))), ''),
       NULLIF(LTRIM(RTRIM(LEFT(c.LastName, 100))), ''),
       NULLIF(LOWER(LTRIM(RTRIM(u.Email))), ''),
       LEFT(b.Reason, 1000), 0, b.CreatedById, b.CreatedOnUtc, b.CreatedById, SYSUTCDATETIME()
FROM BlacklistedCustomers b
JOIN Customers c ON c.Id = b.CustomerId
LEFT JOIN Users u ON u.Id = c.UserId
WHERE b.Deleted = 0
  AND NOT EXISTS (SELECT 1 FROM CustomerBlacklist e WHERE e.CustomerId = b.CustomerId);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerBlacklist_Customers_CustomerId",
                table: "CustomerBlacklist");

            migrationBuilder.DropIndex(
                name: "IX_CustomerBlacklist_CustomerId",
                table: "CustomerBlacklist");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "CustomerBlacklist");
        }
    }
}
