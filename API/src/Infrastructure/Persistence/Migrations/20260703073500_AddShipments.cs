using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuantityShipped",
                table: "OrderLineItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShipmentNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ServiceName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LabelAssetKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LabelPdfUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    LabelGeneratedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPolledOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shipments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderLineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentLineItems_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentTrackingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawStatus = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NormalizedStatus = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CheckpointOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentTrackingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentTrackingEvents_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLineItems_ShipmentId",
                table: "ShipmentLineItems",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OrderId",
                table: "Shipments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShipmentNumber",
                table: "Shipments",
                column: "ShipmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Status",
                table: "Shipments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TrackingNumber",
                table: "Shipments",
                column: "TrackingNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentTrackingEvents_ShipmentId",
                table: "ShipmentTrackingEvents",
                column: "ShipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentLineItems");

            migrationBuilder.DropTable(
                name: "ShipmentTrackingEvents");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropColumn(
                name: "QuantityShipped",
                table: "OrderLineItems");
        }
    }
}
