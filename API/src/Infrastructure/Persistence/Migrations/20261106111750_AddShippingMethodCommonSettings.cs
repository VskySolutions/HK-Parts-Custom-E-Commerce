using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingMethodCommonSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HandlingFee",
                table: "ShippingMethods",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderAmount",
                table: "ShippingMethods",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipToCountries",
                table: "ShippingMethods",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Credentials_Freightview",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HandlingFee",
                table: "Credentials_Freightview",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderAmount",
                table: "Credentials_Freightview",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipToCountries",
                table: "Credentials_Freightview",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HandlingFee",
                table: "ShippingMethods");

            migrationBuilder.DropColumn(
                name: "MinimumOrderAmount",
                table: "ShippingMethods");

            migrationBuilder.DropColumn(
                name: "ShipToCountries",
                table: "ShippingMethods");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Credentials_Freightview");

            migrationBuilder.DropColumn(
                name: "HandlingFee",
                table: "Credentials_Freightview");

            migrationBuilder.DropColumn(
                name: "MinimumOrderAmount",
                table: "Credentials_Freightview");

            migrationBuilder.DropColumn(
                name: "ShipToCountries",
                table: "Credentials_Freightview");
        }
    }
}
