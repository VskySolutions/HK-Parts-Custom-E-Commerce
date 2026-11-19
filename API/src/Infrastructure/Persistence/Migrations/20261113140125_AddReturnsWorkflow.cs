using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnsWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "Rmas",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<decimal>(
                name: "Adjustment",
                table: "Rmas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentReason",
                table: "Rmas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedOnUtc",
                table: "Rmas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedOnUtc",
                table: "Rmas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByAdminId",
                table: "Rmas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EligibilityOverrideReason",
                table: "Rmas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryWarningSentOnUtc",
                table: "Rmas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FflDealerName",
                table: "Rmas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FflExpiry",
                table: "Rmas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FflNumber",
                table: "Rmas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabelCarrier",
                table: "Rmas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LabelCost",
                table: "Rmas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LabelDeduction",
                table: "Rmas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LabelExpiresAtUtc",
                table: "Rmas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabelService",
                table: "Rmas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabelUrl",
                table: "Rmas",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastRefundError",
                table: "Rmas",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedOnUtc",
                table: "Rmas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundAttempts",
                table: "Rmas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RefundMethod",
                table: "Rmas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RefundReference",
                table: "Rmas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RefundShipping",
                table: "Rmas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundStartedOnUtc",
                table: "Rmas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedOnUtc",
                table: "Rmas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestedResolution",
                table: "Rmas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresFfl",
                table: "Rmas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RestockFee",
                table: "Rmas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReturnInstructions",
                table: "Rmas",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnMethod",
                table: "Rmas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Rmas",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<bool>(
                name: "ShipRestricted",
                table: "Rmas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingRefund",
                table: "Rmas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "Rmas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusChangedOnUtc",
                table: "Rmas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreCreditCode",
                table: "Rmas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StoreCreditGiftCertificateId",
                table: "Rmas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalRefund",
                table: "Rmas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRefund",
                table: "Rmas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalRefund",
                table: "Rmas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                table: "Rmas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WaiveRestockFee",
                table: "Rmas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ConditionReceived",
                table: "RmaLineItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConditionReported",
                table: "RmaLineItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerComment",
                table: "RmaLineItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Deduction",
                table: "RmaLineItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DeductionReason",
                table: "RmaLineItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Disposition",
                table: "RmaLineItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FeeApplies",
                table: "RmaLineItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LineRefund",
                table: "RmaLineItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "MerchantFault",
                table: "RmaLineItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QuantityAccepted",
                table: "RmaLineItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuantityReceived",
                table: "RmaLineItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuantityRestocked",
                table: "RmaLineItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ReasonId",
                table: "RmaLineItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitRefund",
                table: "RmaLineItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsReturnable",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RmaId",
                table: "InventoryAuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RmaAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RmaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RmaLineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByType = table.Column<int>(type: "int", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RmaAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RmaAttachments_Rmas_RmaId",
                        column: x => x.RmaId,
                        principalTable: "Rmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RmaHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RmaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ActorType = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RmaHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RmaHistory_Rmas_RmaId",
                        column: x => x.RmaId,
                        principalTable: "Rmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RmaMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RmaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorType = table.Column<int>(type: "int", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RmaMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RmaMessages_Rmas_RmaId",
                        column: x => x.RmaId,
                        principalTable: "Rmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RmaReasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CustomerVisible = table.Column<bool>(type: "bit", nullable: false),
                    FeeApplies = table.Column<bool>(type: "bit", nullable: false),
                    MerchantFault = table.Column<bool>(type: "bit", nullable: false),
                    AutoApprove = table.Column<bool>(type: "bit", nullable: false),
                    RequiresComment = table.Column<bool>(type: "bit", nullable: false),
                    RequiresPhoto = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RmaReasons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rmas_LabelExpiresAtUtc",
                table: "Rmas",
                column: "LabelExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RmaLineItems_OrderLineItemId",
                table: "RmaLineItems",
                column: "OrderLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAuditLogs_RmaId",
                table: "InventoryAuditLogs",
                column: "RmaId");

            migrationBuilder.CreateIndex(
                name: "IX_RmaAttachments_RmaId",
                table: "RmaAttachments",
                column: "RmaId");

            migrationBuilder.CreateIndex(
                name: "IX_RmaHistory_RmaId_CreatedOnUtc",
                table: "RmaHistory",
                columns: new[] { "RmaId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RmaMessages_RmaId_CreatedOnUtc",
                table: "RmaMessages",
                columns: new[] { "RmaId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RmaReasons_SortOrder",
                table: "RmaReasons",
                column: "SortOrder");

            // Back-fill returns created by the old one-step flow (Requested → approved "Completed" = Closed now):
            //  - the requested resolution is the resolution they were settled with (or Refund when none was set);
            //  - closed returns received, accepted and restocked every requested unit (the old approve restocked them),
            //    so those units stay claimed and are never restocked twice;
            //  - their refund total is what was refunded / issued as store credit.
            // EXEC so the idempotent db-script (one batch) compiles before these columns exist.
            migrationBuilder.Sql(@"EXEC(N'
UPDATE [Rmas] SET [RequestedResolution] = CASE WHEN [Resolution] = 0 THEN 1 ELSE [Resolution] END,
                  [StatusChangedOnUtc] = COALESCE([ResolvedOnUtc], [RequestedOnUtc]);
UPDATE [Rmas] SET [ClosedOnUtc] = [ResolvedOnUtc], [ReceivedOnUtc] = [ResolvedOnUtc], [ApprovedOnUtc] = [ResolvedOnUtc],
                  [TotalRefund] = COALESCE([RefundedAmount], [StoreCreditIssued], 0),
                  [RefundedOnUtc] = CASE WHEN [RefundedAmount] IS NOT NULL OR [StoreCreditIssued] IS NOT NULL THEN [ResolvedOnUtc] END,
                  [RefundMethod] = CASE WHEN [Resolution] = 3 THEN 2 WHEN [RefundedAmount] IS NOT NULL THEN 1 ELSE 0 END
WHERE [Status] = 3;
UPDATE l SET l.[QuantityReceived] = l.[Quantity], l.[QuantityAccepted] = l.[Quantity], l.[QuantityRestocked] = l.[Quantity]
FROM [RmaLineItems] l INNER JOIN [Rmas] r ON r.[Id] = l.[RmaId]
WHERE r.[Status] = 3;
')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RmaAttachments");

            migrationBuilder.DropTable(
                name: "RmaHistory");

            migrationBuilder.DropTable(
                name: "RmaMessages");

            migrationBuilder.DropTable(
                name: "RmaReasons");

            migrationBuilder.DropIndex(
                name: "IX_Rmas_LabelExpiresAtUtc",
                table: "Rmas");

            migrationBuilder.DropIndex(
                name: "IX_RmaLineItems_OrderLineItemId",
                table: "RmaLineItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryAuditLogs_RmaId",
                table: "InventoryAuditLogs");

            migrationBuilder.DropColumn(
                name: "Adjustment",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "AdjustmentReason",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "ApprovedOnUtc",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "ClosedOnUtc",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "CreatedByAdminId",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "EligibilityOverrideReason",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "ExpiryWarningSentOnUtc",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "FflDealerName",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "FflExpiry",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "FflNumber",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "LabelCarrier",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "LabelCost",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "LabelDeduction",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "LabelExpiresAtUtc",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "LabelService",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "LabelUrl",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "LastRefundError",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "ReceivedOnUtc",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "RefundAttempts",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "RefundMethod",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "RefundReference",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "RefundShipping",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "RefundStartedOnUtc",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "RefundedOnUtc",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "RequestedResolution",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "RequiresFfl",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "RestockFee",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "ReturnInstructions",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "ReturnMethod",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "ShipRestricted",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "ShippingRefund",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "StatusChangedOnUtc",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "StoreCreditCode",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "StoreCreditGiftCertificateId",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "SubtotalRefund",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "TaxRefund",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "TotalRefund",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "WaiveRestockFee",
                table: "Rmas");

            migrationBuilder.DropColumn(
                name: "ConditionReceived",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "ConditionReported",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "CustomerComment",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "Deduction",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "DeductionReason",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "Disposition",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "FeeApplies",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "LineRefund",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "MerchantFault",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "QuantityAccepted",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "QuantityReceived",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "QuantityRestocked",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "ReasonId",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "UnitRefund",
                table: "RmaLineItems");

            migrationBuilder.DropColumn(
                name: "IsReturnable",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RmaId",
                table: "InventoryAuditLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "Rmas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
