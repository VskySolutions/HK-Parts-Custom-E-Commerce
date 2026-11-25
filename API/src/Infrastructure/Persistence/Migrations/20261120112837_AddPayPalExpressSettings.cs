using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayPalExpressSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedCustomerGroupIds",
                table: "Credentials_PayPal",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiPassword",
                table: "Credentials_PayPal",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiSignature",
                table: "Credentials_PayPal",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiUsername",
                table: "Credentials_PayPal",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrandName",
                table: "Credentials_PayPal",
                type: "nvarchar(127)",
                maxLength: 127,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultOrderStatus",
                table: "Credentials_PayPal",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxOrderAmount",
                table: "Credentials_PayPal",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinOrderAmount",
                table: "Credentials_PayPal",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Credentials_PayPal",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedCustomerGroupIds",
                table: "Credentials_PayPal");

            migrationBuilder.DropColumn(
                name: "ApiPassword",
                table: "Credentials_PayPal");

            migrationBuilder.DropColumn(
                name: "ApiSignature",
                table: "Credentials_PayPal");

            migrationBuilder.DropColumn(
                name: "ApiUsername",
                table: "Credentials_PayPal");

            migrationBuilder.DropColumn(
                name: "BrandName",
                table: "Credentials_PayPal");

            migrationBuilder.DropColumn(
                name: "DefaultOrderStatus",
                table: "Credentials_PayPal");

            migrationBuilder.DropColumn(
                name: "MaxOrderAmount",
                table: "Credentials_PayPal");

            migrationBuilder.DropColumn(
                name: "MinOrderAmount",
                table: "Credentials_PayPal");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Credentials_PayPal");
        }
    }
}
