using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutSettingsLockBlacklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckoutActivityUtc",
                table: "Carts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CheckoutLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerCartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerBlacklist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBlacklist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerBlacklistHits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlacklistEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MatchedOn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBlacklistHits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerBlacklistHits_CustomerBlacklist_BlacklistEntryId",
                        column: x => x.BlacklistEntryId,
                        principalTable: "CustomerBlacklist",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutLocks_OwnerCartId",
                table: "CheckoutLocks",
                column: "OwnerCartId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutLocks_ProductId",
                table: "CheckoutLocks",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBlacklist_Email",
                table: "CustomerBlacklist",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBlacklist_LastName_FirstName",
                table: "CustomerBlacklist",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBlacklistHits_AtUtc",
                table: "CustomerBlacklistHits",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBlacklistHits_BlacklistEntryId",
                table: "CustomerBlacklistHits",
                column: "BlacklistEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutLocks");

            migrationBuilder.DropTable(
                name: "CustomerBlacklistHits");

            migrationBuilder.DropTable(
                name: "CustomerBlacklist");

            migrationBuilder.DropColumn(
                name: "LastCheckoutActivityUtc",
                table: "Carts");
        }
    }
}
