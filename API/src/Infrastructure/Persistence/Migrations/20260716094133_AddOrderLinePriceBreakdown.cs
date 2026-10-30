using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLinePriceBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "OrderLineItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalUnitPrice",
                table: "OrderLineItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Backfill existing lines: their list price is the price that was charged (no group discount
            // was recorded), so OriginalUnitPrice = UnitPrice and DiscountAmount stays 0.
            migrationBuilder.Sql("UPDATE [OrderLineItems] SET [OriginalUnitPrice] = [UnitPrice];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "OriginalUnitPrice",
                table: "OrderLineItems");
        }
    }
}
