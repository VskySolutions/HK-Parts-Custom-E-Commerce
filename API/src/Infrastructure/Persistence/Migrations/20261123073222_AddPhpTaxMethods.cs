using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhpTaxMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AvalaraCommitDocuments",
                table: "TaxProviderConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AvalaraLoggingEnabled",
                table: "TaxProviderConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "AvalaraShippingTaxCode",
                table: "TaxProviderConfigurations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AvalaraTimeoutSeconds",
                table: "TaxProviderConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<bool>(
                name: "TaxShipping",
                table: "TaxProviderConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxDocumentCode",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TaxDocumentVoidedOnUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxMethod",
                table: "CustomerGroups",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TaxLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HttpStatus = table.Column<int>(type: "int", nullable: true),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxStateRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    StateCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    StateName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RatePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxStateRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxZipRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RatePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxZipRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxLogs_CreatedOnUtc",
                table: "TaxLogs",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TaxStateRates_CountryCode_StateCode",
                table: "TaxStateRates",
                columns: new[] { "CountryCode", "StateCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxZipRates_PostalCode",
                table: "TaxZipRates",
                column: "PostalCode",
                unique: true);

            // The existing settings row gets Avalara's common-carrier shipping code (new rows get it from the entity).
            // EXEC so the idempotent script, which runs the migration as one batch, can see the new column.
            migrationBuilder.Sql(
                "EXEC(N'UPDATE [TaxProviderConfigurations] SET [AvalaraShippingTaxCode] = N''FR020100'' WHERE [AvalaraShippingTaxCode] IS NULL');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxLogs");

            migrationBuilder.DropTable(
                name: "TaxStateRates");

            migrationBuilder.DropTable(
                name: "TaxZipRates");

            migrationBuilder.DropColumn(
                name: "AvalaraCommitDocuments",
                table: "TaxProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "AvalaraLoggingEnabled",
                table: "TaxProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "AvalaraShippingTaxCode",
                table: "TaxProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "AvalaraTimeoutSeconds",
                table: "TaxProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "TaxShipping",
                table: "TaxProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "TaxDocumentCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TaxDocumentVoidedOnUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TaxMethod",
                table: "CustomerGroups");
        }
    }
}
