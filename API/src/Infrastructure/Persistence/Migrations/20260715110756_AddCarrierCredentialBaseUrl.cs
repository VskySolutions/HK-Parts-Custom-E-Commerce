using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarrierCredentialBaseUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "Credentials_USPS",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "Credentials_UPS",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "Credentials_FedEx",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "Credentials_DHLExpress",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "Credentials_DHLExpress");
        }
    }
}
