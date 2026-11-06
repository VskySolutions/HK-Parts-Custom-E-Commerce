using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEbayChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EbayFulfillmentId",
                table: "Shipments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChannelEbay",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EbayBuyerMessage",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EbayOrderId",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EbayLineItemId",
                table: "OrderLineItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EbayAccounts",
                columns: table => new
                {
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Environment = table.Column<byte>(type: "tinyint", nullable: false),
                    MarketplaceId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "EBAY_US"),
                    CategoryTreeId = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    AccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccessTokenExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefreshTokenExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConnectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConnectedEbayUser = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LoggingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DefaultCategoryId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DefaultPaymentPolicyId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DefaultFulfillmentPolicyId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DefaultReturnPolicyId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MerchantLocationKey = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    FitmentEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SyncPriceFromStore = table.Column<bool>(type: "bit", nullable: false),
                    OrdersLastSyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EbayAccounts", x => x.StoreId);
                    table.ForeignKey(
                        name: "FK_EbayAccounts_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EbayApiLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseStatus = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EbayApiLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EbayCategories",
                columns: table => new
                {
                    CategoryId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ParentCategoryId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    IsLeaf = table.Column<bool>(type: "bit", nullable: false),
                    TreeVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EbayCategories", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "EbayListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoryId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(55)", maxLength: 55, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    QuantitySold = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ConditionId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PaymentPolicyId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    FulfillmentPolicyId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReturnPolicyId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    OfferId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    EbayListingIdExternal = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EbayListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EbayListings_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VehicleFitments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Make = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Trim = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Engine = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleFitments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleFitments_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EbayListingAspects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EbayListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EbayListingAspects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EbayListingAspects_EbayListings_EbayListingId",
                        column: x => x.EbayListingId,
                        principalTable: "EbayListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_EbayOrderId",
                table: "Orders",
                column: "EbayOrderId",
                unique: true,
                filter: "[EbayOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EbayApiLogs_StoreId_CreatedOnUtc",
                table: "EbayApiLogs",
                columns: new[] { "StoreId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EbayCategories_Name",
                table: "EbayCategories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_EbayCategories_ParentCategoryId",
                table: "EbayCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EbayListingAspects_EbayListingId",
                table: "EbayListingAspects",
                column: "EbayListingId");

            migrationBuilder.CreateIndex(
                name: "IX_EbayListings_EbayListingIdExternal",
                table: "EbayListings",
                column: "EbayListingIdExternal");

            migrationBuilder.CreateIndex(
                name: "IX_EbayListings_Sku",
                table: "EbayListings",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_EbayListings_StoreId_Status",
                table: "EbayListings",
                columns: new[] { "StoreId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_EbayListings_Product_NotEnded",
                table: "EbayListings",
                column: "ProductId",
                unique: true,
                filter: "[Status] <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleFitments_ProductId_Year",
                table: "VehicleFitments",
                columns: new[] { "ProductId", "Year" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EbayAccounts");

            migrationBuilder.DropTable(
                name: "EbayApiLogs");

            migrationBuilder.DropTable(
                name: "EbayCategories");

            migrationBuilder.DropTable(
                name: "EbayListingAspects");

            migrationBuilder.DropTable(
                name: "VehicleFitments");

            migrationBuilder.DropTable(
                name: "EbayListings");

            migrationBuilder.DropIndex(
                name: "IX_Orders_EbayOrderId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EbayFulfillmentId",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ChannelEbay",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "EbayBuyerMessage",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EbayOrderId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EbayLineItemId",
                table: "OrderLineItems");
        }
    }
}
