using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    StopFurtherRules = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DiscountType = table.Column<byte>(type: "tinyint", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LastAppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceRules", x => x.Id);
                    table.CheckConstraint("CK_PriceRules_DateRange", "[StartDate] IS NULL OR [EndDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_PriceRules_DiscountAmount", "[DiscountAmount] > 0");
                    table.CheckConstraint("CK_PriceRules_PercentMax", "[DiscountType] <> 1 OR [DiscountAmount] <= 100");
                    table.ForeignKey(
                        name: "FK_PriceRules_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PriceRuleCategories",
                columns: table => new
                {
                    PriceRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceRuleCategories", x => new { x.PriceRuleId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_PriceRuleCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PriceRuleCategories_PriceRules_PriceRuleId",
                        column: x => x.PriceRuleId,
                        principalTable: "PriceRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceRuleCustomerGroups",
                columns: table => new
                {
                    PriceRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceRuleCustomerGroups", x => new { x.PriceRuleId, x.CustomerGroupId });
                    table.ForeignKey(
                        name: "FK_PriceRuleCustomerGroups_PriceRules_PriceRuleId",
                        column: x => x.PriceRuleId,
                        principalTable: "PriceRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceRuleProductPrices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PriceRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RulePrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    StopFurtherRules = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceRuleProductPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceRuleProductPrices_PriceRules_PriceRuleId",
                        column: x => x.PriceRuleId,
                        principalTable: "PriceRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceRuleCategories_CategoryId",
                table: "PriceRuleCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceRuleProductPrices_Lookup",
                table: "PriceRuleProductPrices",
                columns: new[] { "ProductId", "CustomerGroupId", "StoreId" })
                .Annotation("SqlServer:Include", new[] { "ProductVariantId", "PriceRuleId", "RulePrice", "StartDate", "EndDate", "Priority", "StopFurtherRules" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceRuleProductPrices_PriceRuleId_ProductId_ProductVariantId_CustomerGroupId",
                table: "PriceRuleProductPrices",
                columns: new[] { "PriceRuleId", "ProductId", "ProductVariantId", "CustomerGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceRules_Name",
                table: "PriceRules",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PriceRules_StoreId",
                table: "PriceRules",
                column: "StoreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceRuleCategories");

            migrationBuilder.DropTable(
                name: "PriceRuleCustomerGroups");

            migrationBuilder.DropTable(
                name: "PriceRuleProductPrices");

            migrationBuilder.DropTable(
                name: "PriceRules");
        }
    }
}
