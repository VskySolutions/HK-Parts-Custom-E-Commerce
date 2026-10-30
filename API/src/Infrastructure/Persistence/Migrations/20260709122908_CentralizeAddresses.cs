using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CentralizeAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_Customers_CustomerId",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_CustomerId",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_CustomerId_AddressType",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "StateProvince",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StateProvince",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AddressType",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Addresses");

            migrationBuilder.AddColumn<Guid>(
                name: "AddressId",
                table: "Stores",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShippingAddressId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Addresses",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Addresses",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Addresses",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Landmark",
                table: "Addresses",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Addresses",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Addresses",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressType = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stores_AddressId",
                table: "Stores",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ShippingAddressId",
                table: "Orders",
                column: "ShippingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_AddressId",
                table: "CustomerAddresses",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_CustomerId",
                table: "CustomerAddresses",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_CustomerId_AddressType",
                table: "CustomerAddresses",
                columns: new[] { "CustomerId", "AddressType" },
                unique: true,
                filter: "[IsDefault] = 1 AND [Deleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Addresses_ShippingAddressId",
                table: "Orders",
                column: "ShippingAddressId",
                principalTable: "Addresses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_Addresses_AddressId",
                table: "Stores",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Addresses_ShippingAddressId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Addresses_AddressId",
                table: "Stores");

            migrationBuilder.DropTable(
                name: "CustomerAddresses");

            migrationBuilder.DropIndex(
                name: "IX_Stores_AddressId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ShippingAddressId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "ShippingAddressId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Landmark",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Addresses");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "Stores",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "Stores",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Stores",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Stores",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Stores",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Stores",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Stores",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateProvince",
                table: "Stores",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "Orders",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "Orders",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Orders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Orders",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Orders",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Orders",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Orders",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Orders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateProvince",
                table: "Orders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "Addresses",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "Addresses",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AddressType",
                table: "Addresses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Addresses",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Addresses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_CustomerId",
                table: "Addresses",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_CustomerId_AddressType",
                table: "Addresses",
                columns: new[] { "CustomerId", "AddressType" },
                unique: true,
                filter: "[IsDefault] = 1 AND [Deleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_Customers_CustomerId",
                table: "Addresses",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
