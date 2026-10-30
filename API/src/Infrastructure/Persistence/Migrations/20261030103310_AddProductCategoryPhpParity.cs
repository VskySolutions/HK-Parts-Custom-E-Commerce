using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCategoryPhpParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "ProductVariants",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gtin",
                table: "ProductVariants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Msrp",
                table: "ProductVariants",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "ProductVariants",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackorderMessage",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FreightClass",
                table: "Products",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gtin",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "H1",
                table: "Products",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Height",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFflRequired",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHazardous",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPurchasable",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true); // backfill: existing products stay purchasable

            migrationBuilder.AddColumn<decimal>(
                name: "Length",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxOrderQuantity",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaCustomTags",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaFooterTags",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MetaImageMediaId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaOpenGraph",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinOrderQuantity",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Msrp",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PackagePieces",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 1); // backfill: must satisfy CK_Products_PackagePieces

            migrationBuilder.AddColumn<string>(
                name: "PackageType",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SaleEndDateUtc",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SaleStartDateUtc",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockStatusOverride",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Weight",
                table: "Products",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Width",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "H1",
                table: "Categories",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Image2AltText",
                table: "Categories",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Image2MediaId",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageAltText",
                table: "Categories",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageMediaId",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaCustomTags",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaFooterTags",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MetaImageMediaId",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaOpenGraph",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowBrandFilter",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInMenu",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: true); // backfill: existing categories stay in the menu

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Categories",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategoryFilterAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecificationAttributeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryFilterAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryFilterAttributes_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryFilterAttributes_SpecificationAttributes_SpecificationAttributeId",
                        column: x => x.SpecificationAttributeId,
                        principalTable: "SpecificationAttributes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductCustomFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FieldType = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ProductCustomFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductStoreMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductStoreMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductStoreMappings_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductStoreMappings_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductCustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductCustomFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCustomFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductCustomFieldValues_ProductCustomFields_ProductCustomFieldId",
                        column: x => x.ProductCustomFieldId,
                        principalTable: "ProductCustomFields",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductCustomFieldValues_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_Gtin",
                table: "ProductVariants",
                column: "Gtin",
                filter: "[Gtin] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Gtin",
                table: "Products",
                column: "Gtin",
                filter: "[Gtin] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_MetaImageMediaId",
                table: "Products",
                column: "MetaImageMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                filter: "[Sku] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_OrderQuantityRange",
                table: "Products",
                sql: "([MinOrderQuantity] IS NULL OR [MinOrderQuantity] >= 1) AND ([MaxOrderQuantity] IS NULL OR [MaxOrderQuantity] >= 1) AND ([MinOrderQuantity] IS NULL OR [MaxOrderQuantity] IS NULL OR [MaxOrderQuantity] >= [MinOrderQuantity])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_PackagePieces",
                table: "Products",
                sql: "[PackagePieces] >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_SaleWindow",
                table: "Products",
                sql: "[SaleStartDateUtc] IS NULL OR [SaleEndDateUtc] IS NULL OR [SaleEndDateUtc] >= [SaleStartDateUtc]");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Image2MediaId",
                table: "Categories",
                column: "Image2MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ImageMediaId",
                table: "Categories",
                column: "ImageMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_MetaImageMediaId",
                table: "Categories",
                column: "MetaImageMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryFilterAttributes_CategoryId_SpecificationAttributeId",
                table: "CategoryFilterAttributes",
                columns: new[] { "CategoryId", "SpecificationAttributeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryFilterAttributes_SpecificationAttributeId",
                table: "CategoryFilterAttributes",
                column: "SpecificationAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCustomFields_Code",
                table: "ProductCustomFields",
                column: "Code",
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCustomFieldValues_ProductCustomFieldId",
                table: "ProductCustomFieldValues",
                column: "ProductCustomFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCustomFieldValues_ProductId_ProductCustomFieldId",
                table: "ProductCustomFieldValues",
                columns: new[] { "ProductId", "ProductCustomFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductStoreMappings_ProductId_StoreId",
                table: "ProductStoreMappings",
                columns: new[] { "ProductId", "StoreId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductStoreMappings_StoreId",
                table: "ProductStoreMappings",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Media_Image2MediaId",
                table: "Categories",
                column: "Image2MediaId",
                principalTable: "Media",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Media_ImageMediaId",
                table: "Categories",
                column: "ImageMediaId",
                principalTable: "Media",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Media_MetaImageMediaId",
                table: "Categories",
                column: "MetaImageMediaId",
                principalTable: "Media",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Media_MetaImageMediaId",
                table: "Products",
                column: "MetaImageMediaId",
                principalTable: "Media",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Media_Image2MediaId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Media_ImageMediaId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Media_MetaImageMediaId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Media_MetaImageMediaId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "CategoryFilterAttributes");

            migrationBuilder.DropTable(
                name: "ProductCustomFieldValues");

            migrationBuilder.DropTable(
                name: "ProductStoreMappings");

            migrationBuilder.DropTable(
                name: "ProductCustomFields");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_Gtin",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Products_Gtin",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_MetaImageMediaId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_OrderQuantityRange",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_PackagePieces",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_SaleWindow",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Image2MediaId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ImageMediaId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_MetaImageMediaId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Gtin",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Msrp",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "BackorderMessage",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FreightClass",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Gtin",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "H1",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsFflRequired",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsHazardous",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsPurchasable",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Length",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MaxOrderQuantity",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaCustomTags",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaFooterTags",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaImageMediaId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaOpenGraph",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MinOrderQuantity",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Msrp",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PackagePieces",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PackageType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SaleEndDateUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SalePrice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SaleStartDateUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StockStatusOverride",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Weight",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "H1",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Image2AltText",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Image2MediaId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ImageAltText",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ImageMediaId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaCustomTags",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaFooterTags",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaImageMediaId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaOpenGraph",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ShowBrandFilter",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ShowInMenu",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Categories");
        }
    }
}
