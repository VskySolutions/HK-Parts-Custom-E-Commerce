using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSquareMethodSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccessTokenExpiresAtUtc",
                table: "Credentials_Square",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedCustomerGroupIds",
                table: "Credentials_Square",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultOrderStatus",
                table: "Credentials_Square",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxOrderAmount",
                table: "Credentials_Square",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinOrderAmount",
                table: "Credentials_Square",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "Credentials_Square",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Credentials_Square",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "Credentials_Square",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessTokenExpiresAtUtc",
                table: "Credentials_Square");

            migrationBuilder.DropColumn(
                name: "AllowedCustomerGroupIds",
                table: "Credentials_Square");

            migrationBuilder.DropColumn(
                name: "DefaultOrderStatus",
                table: "Credentials_Square");

            migrationBuilder.DropColumn(
                name: "MaxOrderAmount",
                table: "Credentials_Square");

            migrationBuilder.DropColumn(
                name: "MinOrderAmount",
                table: "Credentials_Square");

            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "Credentials_Square");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Credentials_Square");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "Credentials_Square");
        }
    }
}
