using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreCashOnDeliveryEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CashOnDeliveryEnabled",
                table: "Stores",
                type: "bit",
                nullable: false,
                // Backfill existing stores to COD ON — it was always available before this toggle existed.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashOnDeliveryEnabled",
                table: "Stores");
        }
    }
}
