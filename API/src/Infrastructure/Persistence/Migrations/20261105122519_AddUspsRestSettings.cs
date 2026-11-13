using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUspsRestSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InternationalFallbackRate",
                table: "ShippingMethods",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedDomesticServices",
                table: "Credentials_USPS",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedInternationalServices",
                table: "Credentials_USPS",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultHeightIn",
                table: "Credentials_USPS",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultLengthIn",
                table: "Credentials_USPS",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultWidthIn",
                table: "Credentials_USPS",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Credentials_USPS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HandlingFee",
                table: "Credentials_USPS",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LoggingEnabled",
                table: "Credentials_USPS",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderAmount",
                table: "Credentials_USPS",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceType",
                table: "Credentials_USPS",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipToCountries",
                table: "Credentials_USPS",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            // Existing rows keep quoting: an empty allowed list offers nothing (the legacy defect US-17),
            // so seed the US-16 / US-24 defaults. Before this migration the adapter rated Ground Advantage
            // domestically and all three international classes.
            // EXEC defers compiling the UPDATE until it runs: in the idempotent script (db-scripts) it shares
            // one batch with the ALTER TABLEs above, and a direct reference to the new columns would fail
            // the whole batch with "Invalid column name".
            migrationBuilder.Sql(
                "EXEC(N'UPDATE [Credentials_USPS] SET " +
                "[DisplayName] = N''USPS'', [PriceType] = N''RETAIL'', " +
                "[AllowedDomesticServices] = N''USPS_GROUND_ADVANTAGE,PRIORITY_MAIL,PRIORITY_MAIL_EXPRESS'', " +
                "[AllowedInternationalServices] = N''PRIORITY_MAIL_INTERNATIONAL,PRIORITY_MAIL_EXPRESS_INTERNATIONAL,FIRST-CLASS_PACKAGE_INTERNATIONAL_SERVICE'' " +
                "WHERE [AllowedDomesticServices] IS NULL;');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InternationalFallbackRate",
                table: "ShippingMethods");

            migrationBuilder.DropColumn(
                name: "AllowedDomesticServices",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "AllowedInternationalServices",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "DefaultHeightIn",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "DefaultLengthIn",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "DefaultWidthIn",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "HandlingFee",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "LoggingEnabled",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "MinimumOrderAmount",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "PriceType",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "ShipToCountries",
                table: "Credentials_USPS");
        }
    }
}
