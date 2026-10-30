using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerRolesAndGroupPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerRoles",
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
                    table.PrimaryKey("PK_CustomerRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryRoleRestrictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                name: "CustomerGroupPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerGroupPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerGroupPrices_CustomerRoles_CustomerRoleId",
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
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                name: "IX_CustomerGroupPrices_CustomerRoleId",
                table: "CustomerGroupPrices",
                column: "CustomerRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerGroupPrices_ProductId_ProductVariantId",
                table: "CustomerGroupPrices",
                columns: new[] { "ProductId", "ProductVariantId" });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryRoleRestrictions");

            migrationBuilder.DropTable(
                name: "CustomerGroupPrices");

            migrationBuilder.DropTable(
                name: "CustomerRoleAssignments");

            migrationBuilder.DropTable(
                name: "ProductRoleRestrictions");

            migrationBuilder.DropTable(
                name: "CustomerRoles");
        }
    }
}
