using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase5Cms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CMSBanners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CtaLabel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DisplayLocation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartsOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CMSBanners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CMSBanners_Media_ImageMediaId",
                        column: x => x.ImageMediaId,
                        principalTable: "Media",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CMSBlogPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FeaturedImageMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublishedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MetaTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CMSBlogPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CMSBlogPosts_Media_FeaturedImageMediaId",
                        column: x => x.FeaturedImageMediaId,
                        principalTable: "Media",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CMSHomePageSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionType = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Configuration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CMSHomePageSections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CMSNewsletterSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ConfirmedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnsubscribedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CMSNewsletterSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CMSPageGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: true),
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
                    table.PrimaryKey("PK_CMSPageGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CMSProductCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CMSProductCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CMSPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MetaKeywords = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CanonicalUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PageGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsSystemPage = table.Column<bool>(type: "bit", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CMSPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CMSPages_CMSPageGroups_PageGroupId",
                        column: x => x.PageGroupId,
                        principalTable: "CMSPageGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CMSCategoryPageConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BannerMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PromotionalDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    YmalCollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CMSCategoryPageConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CMSCategoryPageConfigs_CMSProductCollections_YmalCollectionId",
                        column: x => x.YmalCollectionId,
                        principalTable: "CMSProductCollections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CMSCategoryPageConfigs_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CMSCategoryPageConfigs_Media_BannerMediaId",
                        column: x => x.BannerMediaId,
                        principalTable: "Media",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CMSProductCollectionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CMSProductCollectionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CMSProductCollectionItems_CMSProductCollections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "CMSProductCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CMSProductCollectionItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CMSSearchPageContent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Heading = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PlaceholderText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ResultsCountLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NoResultsMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoResultsBannerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NoResultsCollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CMSSearchPageContent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CMSSearchPageContent_CMSBanners_NoResultsBannerId",
                        column: x => x.NoResultsBannerId,
                        principalTable: "CMSBanners",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CMSSearchPageContent_CMSProductCollections_NoResultsCollectionId",
                        column: x => x.NoResultsCollectionId,
                        principalTable: "CMSProductCollections",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CMSCategoryPinnedProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryPageConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CMSCategoryPinnedProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CMSCategoryPinnedProducts_CMSCategoryPageConfigs_CategoryPageConfigId",
                        column: x => x.CategoryPageConfigId,
                        principalTable: "CMSCategoryPageConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CMSCategoryPinnedProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsFeatured",
                table: "Categories",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_CMSBanners_DisplayLocation_DisplayOrder",
                table: "CMSBanners",
                columns: new[] { "DisplayLocation", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CMSBanners_ImageMediaId",
                table: "CMSBanners",
                column: "ImageMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_CMSBlogPosts_FeaturedImageMediaId",
                table: "CMSBlogPosts",
                column: "FeaturedImageMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_CMSBlogPosts_Slug",
                table: "CMSBlogPosts",
                column: "Slug",
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CMSBlogPosts_Status_PublishedOnUtc",
                table: "CMSBlogPosts",
                columns: new[] { "Status", "PublishedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CMSCategoryPageConfigs_BannerMediaId",
                table: "CMSCategoryPageConfigs",
                column: "BannerMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_CMSCategoryPageConfigs_CategoryId",
                table: "CMSCategoryPageConfigs",
                column: "CategoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CMSCategoryPageConfigs_YmalCollectionId",
                table: "CMSCategoryPageConfigs",
                column: "YmalCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CMSCategoryPinnedProducts_CategoryPageConfigId_DisplayOrder",
                table: "CMSCategoryPinnedProducts",
                columns: new[] { "CategoryPageConfigId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CMSCategoryPinnedProducts_CategoryPageConfigId_ProductId",
                table: "CMSCategoryPinnedProducts",
                columns: new[] { "CategoryPageConfigId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CMSCategoryPinnedProducts_ProductId",
                table: "CMSCategoryPinnedProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CMSHomePageSections_DisplayOrder",
                table: "CMSHomePageSections",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_CMSNewsletterSubscriptions_Email",
                table: "CMSNewsletterSubscriptions",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CMSPageGroups_Slug",
                table: "CMSPageGroups",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL AND [Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CMSPages_PageGroupId_DisplayOrder",
                table: "CMSPages",
                columns: new[] { "PageGroupId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CMSPages_Slug",
                table: "CMSPages",
                column: "Slug",
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CMSProductCollectionItems_CollectionId_DisplayOrder",
                table: "CMSProductCollectionItems",
                columns: new[] { "CollectionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CMSProductCollectionItems_CollectionId_ProductId",
                table: "CMSProductCollectionItems",
                columns: new[] { "CollectionId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CMSProductCollectionItems_ProductId",
                table: "CMSProductCollectionItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CMSProductCollections_Slug",
                table: "CMSProductCollections",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL AND [Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CMSSearchPageContent_NoResultsBannerId",
                table: "CMSSearchPageContent",
                column: "NoResultsBannerId");

            migrationBuilder.CreateIndex(
                name: "IX_CMSSearchPageContent_NoResultsCollectionId",
                table: "CMSSearchPageContent",
                column: "NoResultsCollectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CMSBlogPosts");

            migrationBuilder.DropTable(
                name: "CMSCategoryPinnedProducts");

            migrationBuilder.DropTable(
                name: "CMSHomePageSections");

            migrationBuilder.DropTable(
                name: "CMSNewsletterSubscriptions");

            migrationBuilder.DropTable(
                name: "CMSPages");

            migrationBuilder.DropTable(
                name: "CMSProductCollectionItems");

            migrationBuilder.DropTable(
                name: "CMSSearchPageContent");

            migrationBuilder.DropTable(
                name: "CMSCategoryPageConfigs");

            migrationBuilder.DropTable(
                name: "CMSPageGroups");

            migrationBuilder.DropTable(
                name: "CMSBanners");

            migrationBuilder.DropTable(
                name: "CMSProductCollections");

            migrationBuilder.DropIndex(
                name: "IX_Categories_IsFeatured",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Categories");
        }
    }
}
