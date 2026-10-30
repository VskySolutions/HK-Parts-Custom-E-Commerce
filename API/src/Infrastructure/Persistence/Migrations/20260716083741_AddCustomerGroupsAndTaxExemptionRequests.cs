using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerGroupsAndTaxExemptionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Customer Roles are being replaced by Customer Groups. Existing group-price rows reference a
            // CustomerRole that is about to be dropped; the column is reused for the new CustomerGroup FK, so
            // any residual row would point at a non-existent group and fail the new FK below. There is no
            // meaningful remap (groups start empty), so clear the table first. (No-op on a fresh database.)
            migrationBuilder.Sql("DELETE FROM [CustomerGroupPrices];");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerGroupPrices_CustomerRoles_CustomerRoleId",
                table: "CustomerGroupPrices");

            migrationBuilder.DropTable(
                name: "CategoryRoleRestrictions");

            migrationBuilder.DropTable(
                name: "CustomerRoleAssignments");

            migrationBuilder.DropTable(
                name: "ProductRoleRestrictions");

            migrationBuilder.DropTable(
                name: "CustomerRoles");

            migrationBuilder.DropIndex(
                name: "IX_CustomerGroupPrices_CustomerRoleId",
                table: "CustomerGroupPrices");

            migrationBuilder.DropIndex(
                name: "IX_CustomerGroupPrices_ProductId_ProductVariantId",
                table: "CustomerGroupPrices");

            migrationBuilder.RenameColumn(
                name: "CustomerRoleId",
                table: "CustomerGroupPrices",
                newName: "CustomerGroupId");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerGroupId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PricingRuleType = table.Column<int>(type: "int", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxExemptionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CertificateNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VatId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SubmittedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxExemptionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxExemptionRequestDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxExemptionRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxExemptionRequestDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxExemptionRequestDocuments_TaxExemptionRequests_TaxExemptionRequestId",
                        column: x => x.TaxExemptionRequestId,
                        principalTable: "TaxExemptionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerGroupId",
                table: "Customers",
                column: "CustomerGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroupPrices_CustomerGroupId_ProductId_ProductVariantId",
                table: "CustomerGroupPrices",
                columns: new[] { "CustomerGroupId", "ProductId", "ProductVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxExemptionRequestDocuments_TaxExemptionRequestId",
                table: "TaxExemptionRequestDocuments",
                column: "TaxExemptionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxExemptionRequests_CustomerId_SubmittedOnUtc",
                table: "TaxExemptionRequests",
                columns: new[] { "CustomerId", "SubmittedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxExemptionRequests_Status",
                table: "TaxExemptionRequests",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerGroupPrices_CustomerGroups_CustomerGroupId",
                table: "CustomerGroupPrices",
                column: "CustomerGroupId",
                principalTable: "CustomerGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_CustomerGroups_CustomerGroupId",
                table: "Customers",
                column: "CustomerGroupId",
                principalTable: "CustomerGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerGroupPrices_CustomerGroups_CustomerGroupId",
                table: "CustomerGroupPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_CustomerGroups_CustomerGroupId",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "CustomerGroups");

            migrationBuilder.DropTable(
                name: "TaxExemptionRequestDocuments");

            migrationBuilder.DropTable(
                name: "TaxExemptionRequests");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CustomerGroupId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_CustomerGroupPrices_CustomerGroupId_ProductId_ProductVariantId",
                table: "CustomerGroupPrices");

            migrationBuilder.DropColumn(
                name: "CustomerGroupId",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "CustomerGroupId",
                table: "CustomerGroupPrices",
                newName: "CustomerRoleId");

            migrationBuilder.CreateTable(
                name: "CustomerRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PricingRuleType = table.Column<int>(type: "int", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryRoleRestrictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryRoleRestrictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryRoleRestrictions_CustomerRoles_CustomerRoleId",
                        column: x => x.CustomerRoleId,
                        principalTable: "CustomerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerRoleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerRoleAssignments_CustomerRoles_CustomerRoleId",
                        column: x => x.CustomerRoleId,
                        principalTable: "CustomerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductRoleRestrictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRoleRestrictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductRoleRestrictions_CustomerRoles_CustomerRoleId",
                        column: x => x.CustomerRoleId,
                        principalTable: "CustomerRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroupPrices_CustomerRoleId",
                table: "CustomerGroupPrices",
                column: "CustomerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroupPrices_ProductId_ProductVariantId",
                table: "CustomerGroupPrices",
                columns: new[] { "ProductId", "ProductVariantId" });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRoleRestrictions_CategoryId",
                table: "CategoryRoleRestrictions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRoleRestrictions_CategoryId_CustomerRoleId",
                table: "CategoryRoleRestrictions",
                columns: new[] { "CategoryId", "CustomerRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRoleRestrictions_CustomerRoleId",
                table: "CategoryRoleRestrictions",
                column: "CustomerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRoleAssignments_CustomerId",
                table: "CustomerRoleAssignments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRoleAssignments_CustomerId_CustomerRoleId",
                table: "CustomerRoleAssignments",
                columns: new[] { "CustomerId", "CustomerRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRoleAssignments_CustomerRoleId",
                table: "CustomerRoleAssignments",
                column: "CustomerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRoleRestrictions_CustomerRoleId",
                table: "ProductRoleRestrictions",
                column: "CustomerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRoleRestrictions_ProductId",
                table: "ProductRoleRestrictions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRoleRestrictions_ProductId_CustomerRoleId",
                table: "ProductRoleRestrictions",
                columns: new[] { "ProductId", "CustomerRoleId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerGroupPrices_CustomerRoles_CustomerRoleId",
                table: "CustomerGroupPrices",
                column: "CustomerRoleId",
                principalTable: "CustomerRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
