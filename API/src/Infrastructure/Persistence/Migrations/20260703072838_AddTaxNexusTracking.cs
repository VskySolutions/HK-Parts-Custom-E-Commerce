using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxNexusTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StateNexusAccumulators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StateCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrossSales = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ThresholdTransactions = table.Column<int>(type: "int", nullable: true),
                    WarningPercent = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    LastAlertedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateNexusAccumulators", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StateNexusAccumulators_StateCode_PeriodStartUtc",
                table: "StateNexusAccumulators",
                columns: new[] { "StateCode", "PeriodStartUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StateNexusAccumulators");
        }
    }
}
