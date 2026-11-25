using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AccountRequired",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "GuestId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CustomerBlacklist",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GuestOrderAccessLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestOrderAccessLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailNormalized = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MarketingOptIn = table.Column<bool>(type: "bit", nullable: false),
                    OptInOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LinkedCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOrderOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Guests_Customers_LinkedCustomerId",
                        column: x => x.LinkedCustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_GuestId",
                table: "Orders",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestOrderAccessLogs_IpAddress_AtUtc",
                table: "GuestOrderAccessLogs",
                columns: new[] { "IpAddress", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestOrderAccessLogs_OrderId",
                table: "GuestOrderAccessLogs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_EmailNormalized",
                table: "Guests",
                column: "EmailNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guests_LastOrderOnUtc",
                table: "Guests",
                column: "LastOrderOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_LinkedCustomerId",
                table: "Guests",
                column: "LinkedCustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Guests_GuestId",
                table: "Orders",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id");

            // Back-fill (guest checkout §5.2, D6): one Guests row per trimmed, lower-case email of the existing guest
            // orders (no account, not eBay, not an inquiry), named from each email's latest order; then point those
            // orders at it and store their contact email normalized. EXEC(...) because the columns are new in this
            // migration and the idempotent script runs it as one batch (see db-scripts notes).
            migrationBuilder.Sql(@"EXEC(N'
;WITH guestOrders AS (
    SELECT o.Id, o.PlacedOnUtc, LOWER(LTRIM(RTRIM(a.Email))) AS Email, a.FirstName, a.LastName, a.PhoneNumber,
           ROW_NUMBER() OVER (PARTITION BY LOWER(LTRIM(RTRIM(a.Email))) ORDER BY o.PlacedOnUtc DESC) AS rn
    FROM Orders o
    JOIN Addresses a ON a.Id = o.ShippingAddressId
    WHERE o.CustomerId IS NULL AND o.EbayOrderId IS NULL AND o.IsInquiry = 0
      AND a.Email IS NOT NULL AND LTRIM(RTRIM(a.Email)) <> N''''
)
INSERT INTO Guests (Id, EmailNormalized, FirstName, LastName, PhoneNumber, MarketingOptIn, LastOrderOnUtc, CreatedOnUtc, UpdatedOnUtc)
SELECT NEWID(), g.Email, LEFT(g.FirstName, 200), LEFT(g.LastName, 200), LEFT(g.PhoneNumber, 50), 0, g.PlacedOnUtc, SYSUTCDATETIME(), SYSUTCDATETIME()
FROM guestOrders g
WHERE g.rn = 1 AND LEN(g.Email) <= 256
  AND NOT EXISTS (SELECT 1 FROM Guests x WHERE x.EmailNormalized = g.Email);

UPDATE o SET o.GuestId = g.Id
FROM Orders o
JOIN Addresses a ON a.Id = o.ShippingAddressId
JOIN Guests g ON g.EmailNormalized = LOWER(LTRIM(RTRIM(a.Email)))
WHERE o.CustomerId IS NULL AND o.EbayOrderId IS NULL AND o.IsInquiry = 0 AND o.GuestId IS NULL;

UPDATE a SET a.Email = LOWER(LTRIM(RTRIM(a.Email)))
FROM Addresses a
JOIN Orders o ON o.ShippingAddressId = a.Id
WHERE o.GuestId IS NOT NULL AND a.Email <> LOWER(LTRIM(RTRIM(a.Email))) COLLATE Latin1_General_BIN2;
')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Guests_GuestId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "GuestOrderAccessLogs");

            migrationBuilder.DropTable(
                name: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Orders_GuestId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AccountRequired",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "GuestId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CustomerBlacklist");
        }
    }
}
