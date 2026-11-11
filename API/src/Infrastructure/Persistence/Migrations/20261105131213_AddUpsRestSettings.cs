using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUpsRestSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedServices",
                table: "Credentials_UPS",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiVersion",
                table: "Credentials_UPS",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Credentials_UPS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FflAllowedServices",
                table: "Credentials_UPS",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HandlingFee",
                table: "Credentials_UPS",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LoggingEnabled",
                table: "Credentials_UPS",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderAmount",
                table: "Credentials_UPS",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageType",
                table: "Credentials_UPS",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupType",
                table: "Credentials_UPS",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SendPackageInfo",
                table: "Credentials_UPS",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShipToCountries",
                table: "Credentials_UPS",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseNegotiatedRates",
                table: "Credentials_UPS",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing rows get the ups-shipping-requirements.md §3.2 / §4 defaults: domestic services on,
            // international off, FFL carts Next Day only, customer package, daily pickup.
            // EXEC: see AddUspsRestSettings — keeps the idempotent script's single batch compilable.
            migrationBuilder.Sql(
                "EXEC(N'UPDATE [Credentials_UPS] SET " +
                "[DisplayName] = N''UPS'', [ApiVersion] = N''v2409'', " +
                "[AllowedServices] = N''03,12,02,13,01'', [FflAllowedServices] = N''01,13,14'', " +
                "[PackageType] = N''02'', [PickupType] = N''01'' " +
                "WHERE [AllowedServices] IS NULL;');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedServices",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "ApiVersion",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "FflAllowedServices",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "HandlingFee",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "LoggingEnabled",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "MinimumOrderAmount",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "PackageType",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "PickupType",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "SendPackageInfo",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "ShipToCountries",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "UseNegotiatedRates",
                table: "Credentials_UPS");
        }
    }
}
