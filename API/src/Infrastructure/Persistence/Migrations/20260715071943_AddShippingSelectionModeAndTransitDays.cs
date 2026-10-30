using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingSelectionModeAndTransitDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defaults mirror the entity initialisers (7 days / balanced 50). Backfilling the scaffolded 0
            // would leave an existing configuration row assuming instant delivery for every option with an
            // unknown estimate — which is the failure AssumedTransitDays exists to prevent.
            migrationBuilder.AddColumn<int>(
                name: "AssumedTransitDays",
                table: "ShippingProviderConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "CostVsSpeedWeight",
                table: "ShippingProviderConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "SelectionMode",
                table: "ShippingProviderConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TransitDays",
                table: "ShippingMethods",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingMethodId",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShippingWasRecommended",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssumedTransitDays",
                table: "ShippingProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "CostVsSpeedWeight",
                table: "ShippingProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "SelectionMode",
                table: "ShippingProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "TransitDays",
                table: "ShippingMethods");

            migrationBuilder.DropColumn(
                name: "ShippingMethodId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingWasRecommended",
                table: "Orders");
        }
    }
}
