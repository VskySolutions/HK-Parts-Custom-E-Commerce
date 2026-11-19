using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipStationAndAddressVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShippingCost",
                table: "Shipments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Shipments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AddressOverridden",
                table: "OrderShippings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AddressVerifyStatus",
                table: "OrderShippings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipToAddressHash",
                table: "Carts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShipToAddressOverridden",
                table: "Carts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ShipToVerifyStatus",
                table: "Carts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AddressVerificationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsPoBox = table.Column<bool>(type: "bit", nullable: false),
                    IsResidential = table.Column<bool>(type: "bit", nullable: true),
                    SuggestedJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Messages = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressVerificationResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AddressVerificationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    FailedMessage = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Countries = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OfferSuggestion = table.Column<bool>(type: "bit", nullable: false),
                    OnUnavailable = table.Column<int>(type: "int", nullable: false),
                    LoggingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressVerificationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Credentials_Avalara",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LicenseKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    IsProduction = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TransactionFeePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Credentials_Avalara", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipStationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Page = table.Column<int>(type: "int", nullable: true),
                    OrderCount = table.Column<int>(type: "int", nullable: true),
                    OrderNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HttpStatus = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipStationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipStationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IncludeEbay = table.Column<bool>(type: "bit", nullable: false),
                    SplitFfl = table.Column<bool>(type: "bit", nullable: false),
                    NotifyCustomer = table.Column<bool>(type: "bit", nullable: false),
                    ExportStatuses = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AllowQueryCredentials = table.Column<bool>(type: "bit", nullable: false),
                    DebugUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipStationSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddressVerificationResults_AddressHash_Provider_CreatedOnUtc",
                table: "AddressVerificationResults",
                columns: new[] { "AddressHash", "Provider", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AddressVerificationResults_CreatedOnUtc",
                table: "AddressVerificationResults",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ShipStationLogs_Action_CreatedOnUtc",
                table: "ShipStationLogs",
                columns: new[] { "Action", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipStationLogs_CreatedOnUtc",
                table: "ShipStationLogs",
                column: "CreatedOnUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddressVerificationResults");

            migrationBuilder.DropTable(
                name: "AddressVerificationSettings");

            migrationBuilder.DropTable(
                name: "Credentials_Avalara");

            migrationBuilder.DropTable(
                name: "ShipStationLogs");

            migrationBuilder.DropTable(
                name: "ShipStationSettings");

            migrationBuilder.DropColumn(
                name: "ShippingCost",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "AddressOverridden",
                table: "OrderShippings");

            migrationBuilder.DropColumn(
                name: "AddressVerifyStatus",
                table: "OrderShippings");

            migrationBuilder.DropColumn(
                name: "ShipToAddressHash",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "ShipToAddressOverridden",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "ShipToVerifyStatus",
                table: "Carts");
        }
    }
}
