using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationCredentialBaseUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "Credentials_StripeTax",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "Credentials_Square",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "Credentials_Razorpay",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "Credentials_PayPal",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "Credentials_AuthorizeNet",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "Credentials_StripeTax");

            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "Credentials_Square");

            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "Credentials_Razorpay");

            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "Credentials_PayPal");

            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "Credentials_AuthorizeNet");
        }
    }
}
