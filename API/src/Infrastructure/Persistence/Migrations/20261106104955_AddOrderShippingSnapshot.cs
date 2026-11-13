using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderShippingSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsShippingRestrictionExempt",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlacementLockUntilUtc",
                table: "Carts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderShippings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShipmentType = table.Column<int>(type: "int", nullable: false),
                    ShipToType = table.Column<int>(type: "int", nullable: false),
                    MethodId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MethodName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Carrier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MethodPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HandlingFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SignatureRequired = table.Column<bool>(type: "bit", nullable: false),
                    SignatureFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceSelected = table.Column<bool>(type: "bit", nullable: false),
                    InsuranceFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeclaredValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EstimatedDeliveryDays = table.Column<int>(type: "int", nullable: true),
                    WasRecommended = table.Column<bool>(type: "bit", nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    WeightLb = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ContainsFflItems = table.Column<bool>(type: "bit", nullable: false),
                    FflDealerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FflNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RulesVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderShippings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderShippings_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShippingRuleSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignatureEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SignatureFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceEnabled = table.Column<bool>(type: "bit", nullable: false),
                    InsuranceBandsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InsuranceExcessFeePer100 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StateRestrictionEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RestrictedStates = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    InternationalRestrictionEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AllowedCountryCodes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RefusePoBoxes = table.Column<bool>(type: "bit", nullable: false),
                    PoBoxMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InvalidDestinationMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingRuleSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderShippings_OrderId_ShipmentType",
                table: "OrderShippings",
                columns: new[] { "OrderId", "ShipmentType" });

            // OP-61: give every existing order (inquiries excluded) one shipping row from the columns it already
            // carries. Add-ons did not exist before, so none are back-filled. EXEC so the idempotent db-script,
            // which runs the whole migration in one batch, compiles before OrderShippings exists.
            migrationBuilder.Sql(@"EXEC(N'
INSERT INTO [OrderShippings] ([Id], [OrderId], [ShipmentType], [ShipToType], [MethodId], [MethodName], [DisplayName], [Carrier],
    [MethodPrice], [HandlingFee], [SignatureRequired], [SignatureFee], [InsuranceSelected], [InsuranceFee], [DeclaredValue],
    [ShippingTotal], [EstimatedDeliveryDays], [WasRecommended], [ItemCount], [WeightLb], [ContainsFflItems], [FflDealerName],
    [FflNumber], [RulesVersion])
SELECT NEWID(), o.[Id], 0,
    CASE WHEN o.[IsPickup] = 1 THEN 2 ELSE 0 END,
    COALESCE(o.[ShippingMethodId], CASE WHEN o.[IsPickup] = 1 THEN N''pickup'' WHEN o.[ShippingTotal] = 0 AND o.[ShippingMethodName] IS NULL THEN N''no_shipping'' END),
    COALESCE(o.[ShippingMethodId], CASE WHEN o.[IsPickup] = 1 THEN N''pickup'' WHEN o.[ShippingTotal] = 0 AND o.[ShippingMethodName] IS NULL THEN N''no_shipping'' END),
    COALESCE(o.[ShippingMethodName], CASE WHEN o.[IsPickup] = 1 THEN N''Pickup in store'' WHEN o.[ShippingTotal] = 0 THEN N''No Shipment Required'' END),
    o.[ShippingCarrier],
    o.[ShippingTotal], 0, 0, 0, 0, 0, o.[Subtotal],
    o.[ShippingTotal], NULL, o.[ShippingWasRecommended],
    ISNULL((SELECT SUM(l.[Quantity]) FROM [OrderLineItems] l WHERE l.[OrderId] = o.[Id]), 0),
    NULL, 0, NULL, NULL, 1
FROM [Orders] o
WHERE o.[IsInquiry] = 0
  AND NOT EXISTS (SELECT 1 FROM [OrderShippings] s WHERE s.[OrderId] = o.[Id]);
')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderShippings");

            migrationBuilder.DropTable(
                name: "ShippingRuleSettings");

            migrationBuilder.DropColumn(
                name: "IsShippingRestrictionExempt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PlacementLockUntilUtc",
                table: "Carts");
        }
    }
}
