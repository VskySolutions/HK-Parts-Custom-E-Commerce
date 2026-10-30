using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateProductImagesToMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the new schema first so the data backfill can populate it before the drop.
            migrationBuilder.AddColumn<Guid>(
                name: "ProductVariantId",
                table: "ProductPictures",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "Media",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            // 2. One-time data migration: fold every legacy ProductImages row (images AND video embeds)
            //    into the central Media library, then re-map it onto the product via ProductPictures.
            //    The image/video URL becomes both the Media asset key and its resolved Url (going forward
            //    no physical URL is stored outside Media). MediaType ints already align (Image=0, Video=1).
            //    A temp map keeps one new MediaId per legacy image so both inserts stay correlated.
            migrationBuilder.Sql(@"
CREATE TABLE #ImgMap (ProductImageId uniqueidentifier PRIMARY KEY, MediaId uniqueidentifier NOT NULL);

INSERT INTO #ImgMap (ProductImageId, MediaId)
SELECT Id, NEWID() FROM ProductImages;

INSERT INTO Media
    (Id, OriginalFileName, SeoFileName, AssetKey, Url, MediaType, MimeType, FileSizeBytes,
     Width, Height, AltText, Title, Caption, Description, CreatedOnUtc, UpdatedOnUtc, Deleted)
SELECT
    map.MediaId,
    LEFT(pi.Url, 400),
    CONCAT('migrated-', LOWER(REPLACE(CONVERT(varchar(36), map.MediaId), '-', ''))),
    pi.Url,
    pi.Url,
    pi.MediaType,
    CASE WHEN pi.MediaType = 1 THEN 'text/html' ELSE 'image/*' END,
    0,
    NULL, NULL,
    pi.AltText, NULL, NULL, NULL,
    SYSUTCDATETIME(), SYSUTCDATETIME(), 0
FROM ProductImages pi
JOIN #ImgMap map ON map.ProductImageId = pi.Id;

INSERT INTO ProductPictures (Id, ProductId, ProductVariantId, MediaId, DisplayOrder)
SELECT NEWID(), pi.ProductId, pi.ProductVariantId, map.MediaId, pi.DisplayOrder
FROM ProductImages pi
JOIN #ImgMap map ON map.ProductImageId = pi.Id;

DROP TABLE #ImgMap;
");

            // 3. Now the legacy table is fully migrated — index/relate the new column and drop it.
            migrationBuilder.CreateIndex(
                name: "IX_ProductPictures_ProductVariantId",
                table: "ProductPictures",
                column: "ProductVariantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPictures_ProductVariants_ProductVariantId",
                table: "ProductPictures",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");

            migrationBuilder.DropTable(
                name: "ProductImages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductPictures_ProductVariants_ProductVariantId",
                table: "ProductPictures");

            migrationBuilder.DropIndex(
                name: "IX_ProductPictures_ProductVariantId",
                table: "ProductPictures");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "ProductPictures");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "Media");

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AltText = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    MediaType = table.Column<int>(type: "int", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductVariantId",
                table: "ProductImages",
                column: "ProductVariantId");
        }
    }
}
