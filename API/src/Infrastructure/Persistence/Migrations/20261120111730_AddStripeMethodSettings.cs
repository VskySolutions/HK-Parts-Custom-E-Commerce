using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeMethodSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptedCardTypes",
                table: "Credentials_Stripe",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedCustomerGroupIds",
                table: "Credentials_Stripe",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CustomerProfilesEnabled",
                table: "Credentials_Stripe",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DefaultOrderStatus",
                table: "Credentials_Stripe",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxOrderAmount",
                table: "Credentials_Stripe",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinOrderAmount",
                table: "Credentials_Stripe",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Credentials_Stripe",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "Credentials_Stripe",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedCardTypes",
                table: "Credentials_Stripe");

            migrationBuilder.DropColumn(
                name: "AllowedCustomerGroupIds",
                table: "Credentials_Stripe");

            migrationBuilder.DropColumn(
                name: "CustomerProfilesEnabled",
                table: "Credentials_Stripe");

            migrationBuilder.DropColumn(
                name: "DefaultOrderStatus",
                table: "Credentials_Stripe");

            migrationBuilder.DropColumn(
                name: "MaxOrderAmount",
                table: "Credentials_Stripe");

            migrationBuilder.DropColumn(
                name: "MinOrderAmount",
                table: "Credentials_Stripe");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Credentials_Stripe");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "Credentials_Stripe");
        }
    }
}
