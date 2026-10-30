using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManufacturerLogoMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LogoMediaId",
                table: "Manufacturers",
                type: "uniqueidentifier",
                nullable: true);

            // One-time backfill: fold each legacy LogoUrl into a Media asset and point LogoMediaId at it.
            // The legacy LogoUrl column is intentionally retained as a read fallback.
            migrationBuilder.Sql(@"
DECLARE @Map TABLE (ManufacturerId uniqueidentifier PRIMARY KEY, MediaId uniqueidentifier NOT NULL);

INSERT INTO @Map (ManufacturerId, MediaId)
SELECT Id, NEWID()
FROM Manufacturers
WHERE LogoUrl IS NOT NULL AND LTRIM(RTRIM(LogoUrl)) <> '' AND LogoMediaId IS NULL;

INSERT INTO Media
    (Id, OriginalFileName, SeoFileName, AssetKey, Url, MediaType, MimeType, FileSizeBytes,
     AltText, CreatedOnUtc, UpdatedOnUtc, Deleted)
SELECT
    map.MediaId,
    LEFT(m.LogoUrl, 400),
    CONCAT('migrated-logo-', LOWER(REPLACE(CONVERT(varchar(36), map.MediaId), '-', ''))),
    m.LogoUrl,
    m.LogoUrl,
    0,
    'image/*',
    0,
    m.Name,
    SYSUTCDATETIME(), SYSUTCDATETIME(), 0
FROM Manufacturers m
JOIN @Map map ON map.ManufacturerId = m.Id;

UPDATE m SET LogoMediaId = map.MediaId
FROM Manufacturers m
JOIN @Map map ON map.ManufacturerId = m.Id;
");

            migrationBuilder.CreateIndex(
                name: "IX_Manufacturers_LogoMediaId",
                table: "Manufacturers",
                column: "LogoMediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Manufacturers_Media_LogoMediaId",
                table: "Manufacturers",
                column: "LogoMediaId",
                principalTable: "Media",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Manufacturers_Media_LogoMediaId",
                table: "Manufacturers");

            migrationBuilder.DropIndex(
                name: "IX_Manufacturers_LogoMediaId",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "LogoMediaId",
                table: "Manufacturers");
        }
    }
}
