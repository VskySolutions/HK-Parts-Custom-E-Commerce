using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreBankTransferEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BankTransferEnabled",
                table: "Stores",
                type: "bit",
                nullable: false,
                // Backfill existing stores to Bank Transfer ON — it was always offered before this toggle
                // existed, so scaffolding's `false` would silently withdraw it at checkout on deploy.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankTransferEnabled",
                table: "Stores");
        }
    }
}
