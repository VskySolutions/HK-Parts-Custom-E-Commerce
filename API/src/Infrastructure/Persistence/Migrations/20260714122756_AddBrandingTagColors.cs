using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandingTagColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyBackgroundColor",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Heading1Color",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Heading2Color",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Heading3Color",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Heading4Color",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Heading5Color",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Heading6Color",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeadingColor",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkColor",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParagraphColor",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpanColor",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextColor",
                table: "TenantBrandings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyBackgroundColor",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "Heading1Color",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "Heading2Color",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "Heading3Color",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "Heading4Color",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "Heading5Color",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "Heading6Color",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "HeadingColor",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "LinkColor",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "ParagraphColor",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "SpanColor",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "TextColor",
                table: "TenantBrandings");
        }
    }
}
