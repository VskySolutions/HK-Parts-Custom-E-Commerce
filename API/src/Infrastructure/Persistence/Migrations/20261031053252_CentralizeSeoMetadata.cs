using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CentralizeSeoMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Create the central table first so the existing per-entity SEO data can be copied into it
            //    before the old columns are dropped (hand-reordered from the scaffold).
            migrationBuilder.CreateTable(
                name: "SeoMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetaTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MetaKeywords = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CanonicalUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    H1 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MetaImageMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MetaOpenGraph = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaCustomTags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaFooterTags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoIndex = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeoMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeoMetadata_Media_MetaImageMediaId",
                        column: x => x.MetaImageMediaId,
                        principalTable: "Media",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeoMetadata_EntityType_EntityId",
                table: "SeoMetadata",
                columns: new[] { "EntityType", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeoMetadata_MetaImageMediaId",
                table: "SeoMetadata",
                column: "MetaImageMediaId");

            // 2) Copy existing values (SeoEntityType: Product=1, Category=2, Manufacturer=3, CmsPage=4,
            //    BlogPost=5). Only rows that actually carry SEO data get a SeoMetadata row.
            const string insertCols = "INSERT INTO [SeoMetadata] ([Id],[EntityType],[EntityId],[MetaTitle],[MetaDescription],"
                + "[MetaKeywords],[CanonicalUrl],[H1],[MetaImageMediaId],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags],"
                + "[NoIndex],[CreatedOnUtc],[UpdatedOnUtc]) ";

            migrationBuilder.Sql(Exec(insertCols +
                "SELECT NEWID(), 1, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], NULL, [H1], [MetaImageMediaId], "
                + "[MetaOpenGraph], [MetaCustomTags], [MetaFooterTags], 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [Products] "
                + "WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords],[H1],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags]) IS NOT NULL "
                + "OR [MetaImageMediaId] IS NOT NULL;"));

            migrationBuilder.Sql(Exec(insertCols +
                "SELECT NEWID(), 2, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], [CanonicalUrl], [H1], [MetaImageMediaId], "
                + "[MetaOpenGraph], [MetaCustomTags], [MetaFooterTags], 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [Categories] "
                + "WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags]) IS NOT NULL "
                + "OR [MetaImageMediaId] IS NOT NULL;"));

            migrationBuilder.Sql(Exec(insertCols +
                "SELECT NEWID(), 3, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], NULL, NULL, NULL, NULL, NULL, NULL, "
                + "0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [Manufacturers] "
                + "WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords]) IS NOT NULL;"));

            migrationBuilder.Sql(Exec(insertCols +
                "SELECT NEWID(), 4, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], [CanonicalUrl], NULL, NULL, NULL, NULL, NULL, "
                + "0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [CMSPages] "
                + "WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl]) IS NOT NULL;"));

            migrationBuilder.Sql(Exec(insertCols +
                "SELECT NEWID(), 5, [Id], [MetaTitle], [MetaDescription], NULL, NULL, NULL, NULL, NULL, NULL, NULL, "
                + "0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [CMSBlogPosts] "
                + "WHERE COALESCE([MetaTitle],[MetaDescription]) IS NOT NULL;"));

            // 3) Drop the old per-entity columns.
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Media_MetaImageMediaId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Media_MetaImageMediaId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_MetaImageMediaId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_MetaImageMediaId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "H1",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaCustomTags",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaFooterTags",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaImageMediaId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaKeywords",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaOpenGraph",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "MetaKeywords",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "CanonicalUrl",
                table: "CMSPages");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "CMSPages");

            migrationBuilder.DropColumn(
                name: "MetaKeywords",
                table: "CMSPages");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                table: "CMSPages");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "CMSBlogPosts");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                table: "CMSBlogPosts");

            migrationBuilder.DropColumn(
                name: "CanonicalUrl",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "H1",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaCustomTags",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaFooterTags",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaImageMediaId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaKeywords",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaOpenGraph",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                table: "Categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "H1",
                table: "Products",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaCustomTags",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
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
                name: "MetaKeywords",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaOpenGraph",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                table: "Products",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "Manufacturers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaKeywords",
                table: "Manufacturers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                table: "Manufacturers",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanonicalUrl",
                table: "CMSPages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "CMSPages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaKeywords",
                table: "CMSPages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                table: "CMSPages",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "CMSBlogPosts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                table: "CMSBlogPosts",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanonicalUrl",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "H1",
                table: "Categories",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaCustomTags",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
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
                name: "MetaKeywords",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaOpenGraph",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                table: "Categories",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_MetaImageMediaId",
                table: "Products",
                column: "MetaImageMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_MetaImageMediaId",
                table: "Categories",
                column: "MetaImageMediaId");

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

            // Copy the central rows back into the restored per-entity columns, then drop the table.
            // (NoIndex and fields a given entity never had are lost on rollback.)
            migrationBuilder.Sql(Exec(
                "UPDATE t SET t.[MetaTitle]=s.[MetaTitle], t.[MetaDescription]=s.[MetaDescription], t.[MetaKeywords]=s.[MetaKeywords], "
                + "t.[H1]=s.[H1], t.[MetaImageMediaId]=s.[MetaImageMediaId], t.[MetaOpenGraph]=s.[MetaOpenGraph], "
                + "t.[MetaCustomTags]=s.[MetaCustomTags], t.[MetaFooterTags]=s.[MetaFooterTags] "
                + "FROM [Products] t JOIN [SeoMetadata] s ON s.[EntityType]=1 AND s.[EntityId]=t.[Id];"));

            migrationBuilder.Sql(Exec(
                "UPDATE t SET t.[MetaTitle]=s.[MetaTitle], t.[MetaDescription]=s.[MetaDescription], t.[MetaKeywords]=s.[MetaKeywords], "
                + "t.[CanonicalUrl]=s.[CanonicalUrl], t.[H1]=s.[H1], t.[MetaImageMediaId]=s.[MetaImageMediaId], "
                + "t.[MetaOpenGraph]=s.[MetaOpenGraph], t.[MetaCustomTags]=s.[MetaCustomTags], t.[MetaFooterTags]=s.[MetaFooterTags] "
                + "FROM [Categories] t JOIN [SeoMetadata] s ON s.[EntityType]=2 AND s.[EntityId]=t.[Id];"));

            migrationBuilder.Sql(Exec(
                "UPDATE t SET t.[MetaTitle]=s.[MetaTitle], t.[MetaDescription]=s.[MetaDescription], t.[MetaKeywords]=s.[MetaKeywords] "
                + "FROM [Manufacturers] t JOIN [SeoMetadata] s ON s.[EntityType]=3 AND s.[EntityId]=t.[Id];"));

            migrationBuilder.Sql(Exec(
                "UPDATE t SET t.[MetaTitle]=s.[MetaTitle], t.[MetaDescription]=s.[MetaDescription], t.[MetaKeywords]=s.[MetaKeywords], "
                + "t.[CanonicalUrl]=s.[CanonicalUrl] "
                + "FROM [CMSPages] t JOIN [SeoMetadata] s ON s.[EntityType]=4 AND s.[EntityId]=t.[Id];"));

            migrationBuilder.Sql(Exec(
                "UPDATE t SET t.[MetaTitle]=s.[MetaTitle], t.[MetaDescription]=s.[MetaDescription] "
                + "FROM [CMSBlogPosts] t JOIN [SeoMetadata] s ON s.[EntityType]=5 AND s.[EntityId]=t.[Id];"));

            migrationBuilder.DropTable(
                name: "SeoMetadata");
        }

        // Wrap hand-written SQL in EXEC so SQL Server only compiles it when it runs. In an idempotent
        // script (`migrations script --idempotent`) the statements sit inside IF NOT EXISTS guards, but a
        // batch is compiled as a whole — without EXEC, re-running the script after the columns are gone
        // fails with "Invalid column name" even though the guarded block would be skipped.
        private static string Exec(string sql) => $"EXEC(N'{sql.Replace("'", "''")}');";
    }
}
