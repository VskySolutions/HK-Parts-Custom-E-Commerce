using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnsRma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rmas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RmaNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Resolution = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RequestedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rmas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rmas_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RmaLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RmaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderLineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    LineReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RmaLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RmaLineItems_Rmas_RmaId",
                        column: x => x.RmaId,
                        principalTable: "Rmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RmaLineItems_RmaId",
                table: "RmaLineItems",
                column: "RmaId");

            migrationBuilder.CreateIndex(
                name: "IX_Rmas_CustomerId",
                table: "Rmas",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Rmas_OrderId",
                table: "Rmas",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Rmas_RmaNumber",
                table: "Rmas",
                column: "RmaNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rmas_Status",
                table: "Rmas",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RmaLineItems");

            migrationBuilder.DropTable(
                name: "Rmas");
        }
    }
}
