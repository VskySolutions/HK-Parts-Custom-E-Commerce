using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBrandingMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FaviconMediaId",
                table: "TenantBrandings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoMediaId",
                table: "TenantBrandings",
                type: "uniqueidentifier",
                nullable: true);

            // One-time backfill: fold each legacy LogoUrl/FaviconUrl into a Media asset and point the new
            // FK at it. The legacy URL columns are intentionally retained as read fallbacks.
            migrationBuilder.Sql(@"
DECLARE @LogoMap TABLE (BrandingId uniqueidentifier PRIMARY KEY, MediaId uniqueidentifier NOT NULL);
INSERT INTO @LogoMap (BrandingId, MediaId)
SELECT Id, NEWID() FROM TenantBrandings
WHERE LogoUrl IS NOT NULL AND LTRIM(RTRIM(LogoUrl)) <> '' AND LogoMediaId IS NULL;

INSERT INTO Media (Id, OriginalFileName, SeoFileName, AssetKey, Url, MediaType, MimeType, FileSizeBytes, CreatedOnUtc, UpdatedOnUtc, Deleted)
SELECT map.MediaId, LEFT(t.LogoUrl, 400),
    CONCAT('migrated-brand-logo-', LOWER(REPLACE(CONVERT(varchar(36), map.MediaId), '-', ''))),
    t.LogoUrl, t.LogoUrl, 0, 'image/*', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
FROM TenantBrandings t JOIN @LogoMap map ON map.BrandingId = t.Id;

UPDATE t SET LogoMediaId = map.MediaId FROM TenantBrandings t JOIN @LogoMap map ON map.BrandingId = t.Id;

DECLARE @FavMap TABLE (BrandingId uniqueidentifier PRIMARY KEY, MediaId uniqueidentifier NOT NULL);
INSERT INTO @FavMap (BrandingId, MediaId)
SELECT Id, NEWID() FROM TenantBrandings
WHERE FaviconUrl IS NOT NULL AND LTRIM(RTRIM(FaviconUrl)) <> '' AND FaviconMediaId IS NULL;

INSERT INTO Media (Id, OriginalFileName, SeoFileName, AssetKey, Url, MediaType, MimeType, FileSizeBytes, CreatedOnUtc, UpdatedOnUtc, Deleted)
SELECT map.MediaId, LEFT(t.FaviconUrl, 400),
    CONCAT('migrated-brand-favicon-', LOWER(REPLACE(CONVERT(varchar(36), map.MediaId), '-', ''))),
    t.FaviconUrl, t.FaviconUrl, 0, 'image/*', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
FROM TenantBrandings t JOIN @FavMap map ON map.BrandingId = t.Id;

UPDATE t SET FaviconMediaId = map.MediaId FROM TenantBrandings t JOIN @FavMap map ON map.BrandingId = t.Id;
");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBrandings_FaviconMediaId",
                table: "TenantBrandings",
                column: "FaviconMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBrandings_LogoMediaId",
                table: "TenantBrandings",
                column: "LogoMediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantBrandings_Media_FaviconMediaId",
                table: "TenantBrandings",
                column: "FaviconMediaId",
                principalTable: "Media",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantBrandings_Media_LogoMediaId",
                table: "TenantBrandings",
                column: "LogoMediaId",
                principalTable: "Media",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenantBrandings_Media_FaviconMediaId",
                table: "TenantBrandings");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantBrandings_Media_LogoMediaId",
                table: "TenantBrandings");

            migrationBuilder.DropIndex(
                name: "IX_TenantBrandings_FaviconMediaId",
                table: "TenantBrandings");

            migrationBuilder.DropIndex(
                name: "IX_TenantBrandings_LogoMediaId",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "FaviconMediaId",
                table: "TenantBrandings");

            migrationBuilder.DropColumn(
                name: "LogoMediaId",
                table: "TenantBrandings");
        }
    }
}
