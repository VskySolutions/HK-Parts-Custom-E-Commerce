using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingProviderConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShippingProviderConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingProviderConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShippingCarrierSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShippingProviderConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Carrier = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingCarrierSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShippingCarrierSettings_ShippingProviderConfigurations_ShippingProviderConfigurationId",
                        column: x => x.ShippingProviderConfigurationId,
                        principalTable: "ShippingProviderConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShippingCarrierSettings_ShippingProviderConfigurationId_Carrier",
                table: "ShippingCarrierSettings",
                columns: new[] { "ShippingProviderConfigurationId", "Carrier" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShippingCarrierSettings");

            migrationBuilder.DropTable(
                name: "ShippingProviderConfigurations");
        }
    }
}
