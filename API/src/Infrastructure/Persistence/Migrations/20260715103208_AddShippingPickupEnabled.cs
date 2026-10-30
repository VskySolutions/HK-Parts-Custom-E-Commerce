using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingPickupEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PickupEnabled",
                table: "ShippingProviderConfigurations",
                type: "bit",
                nullable: false,
                // The platform switch defaults ON, matching the entity and the unconfigured-install path: it
                // only decides whether collection may be offered, and each store still has to opt in via
                // Stores.PickupEnabled (which defaults OFF). Scaffolding's `false` would instead leave a
                // kill-switch silently engaged, so a store that opted in would never reach checkout.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickupEnabled",
                table: "ShippingProviderConfigurations");
        }
    }
}
