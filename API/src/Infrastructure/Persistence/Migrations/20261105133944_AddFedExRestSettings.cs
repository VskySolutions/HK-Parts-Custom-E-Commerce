using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFedExRestSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedServices",
                table: "Credentials_FedEx",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultHeightIn",
                table: "Credentials_FedEx",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultLengthIn",
                table: "Credentials_FedEx",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultWidthIn",
                table: "Credentials_FedEx",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Credentials_FedEx",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FflAllowedServices",
                table: "Credentials_FedEx",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HandlingFee",
                table: "Credentials_FedEx",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LoggingEnabled",
                table: "Credentials_FedEx",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderAmount",
                table: "Credentials_FedEx",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupType",
                table: "Credentials_FedEx",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateMode",
                table: "Credentials_FedEx",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SendPackageInfo",
                table: "Credentials_FedEx",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ShipToCountries",
                table: "Credentials_FedEx",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            // Existing rows get the fedex-shipping-requirements.md FX-19 / §3.2 parcel defaults (freight,
            // same-day, Europe and international off), ACCOUNT rates and the pickup type the adapter has always
            // sent. EXEC: see AddUspsRestSettings — keeps the idempotent script's single batch compilable.
            migrationBuilder.Sql(
                "EXEC(N'UPDATE [Credentials_FedEx] SET " +
                "[DisplayName] = N''FedEx'', [RateMode] = N''ACCOUNT'', [PickupType] = N''USE_SCHEDULED_PICKUP'', " +
                "[AllowedServices] = N''FEDEX_GROUND,GROUND_HOME_DELIVERY,FEDEX_EXPRESS_SAVER,FEDEX_2_DAY,STANDARD_OVERNIGHT,PRIORITY_OVERNIGHT'' " +
                "WHERE [AllowedServices] IS NULL;');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedServices",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "DefaultHeightIn",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "DefaultLengthIn",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "DefaultWidthIn",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "FflAllowedServices",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "HandlingFee",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "LoggingEnabled",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "MinimumOrderAmount",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "PickupType",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "RateMode",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "SendPackageInfo",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "ShipToCountries",
                table: "Credentials_FedEx");
        }
    }
}
