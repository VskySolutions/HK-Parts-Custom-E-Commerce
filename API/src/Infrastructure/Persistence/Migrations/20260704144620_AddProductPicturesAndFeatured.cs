using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPicturesAndFeatured : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FeaturedDisplayOrder",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProductPictures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPictures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPictures_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductPictures_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsFeatured_FeaturedDisplayOrder",
                table: "Products",
                columns: new[] { "IsFeatured", "FeaturedDisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPictures_MediaId",
                table: "ProductPictures",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPictures_ProductId_DisplayOrder",
                table: "ProductPictures",
                columns: new[] { "ProductId", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductPictures");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsFeatured_FeaturedDisplayOrder",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FeaturedDisplayOrder",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Products");
        }
    }
}
