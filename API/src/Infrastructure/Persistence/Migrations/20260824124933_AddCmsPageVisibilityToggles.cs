using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCmsPageVisibilityToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowInFooter",
                table: "CMSPages",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInTopBar",
                table: "CMSPages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_CMSPages_Status_DisplayOrder",
                table: "CMSPages",
                columns: new[] { "Status", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CMSPages_Status_DisplayOrder",
                table: "CMSPages");

            migrationBuilder.DropColumn(
                name: "ShowInFooter",
                table: "CMSPages");

            migrationBuilder.DropColumn(
                name: "ShowInTopBar",
                table: "CMSPages");
        }
    }
}
