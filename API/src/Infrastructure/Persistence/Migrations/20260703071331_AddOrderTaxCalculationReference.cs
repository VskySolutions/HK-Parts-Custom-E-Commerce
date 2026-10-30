using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTaxCalculationReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxProviderCalculationRef",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxProviderCalculationRef",
                table: "Orders");
        }
    }
}
