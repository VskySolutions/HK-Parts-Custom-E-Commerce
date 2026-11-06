using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GiftCertificateOnline",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true); // existing products: online certificates (the app always writes the value)

            migrationBuilder.AddColumn<bool>(
                name: "IsMaster",
                table: "PaymentRecords",
                type: "bit",
                nullable: false,
                defaultValue: true); // every existing payment settled its order alone

            migrationBuilder.AddColumn<decimal>(
                name: "GiftCertificateAppliedAmount",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "GiftCertificateCode",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GiftCertificateId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GcMessage",
                table: "OrderLineItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GcRecipientEmail",
                table: "OrderLineItems",
                type: "nvarchar(75)",
                maxLength: 75,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GcRecipientName",
                table: "OrderLineItems",
                type: "nvarchar(75)",
                maxLength: 75,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GcSenderEmail",
                table: "OrderLineItems",
                type: "nvarchar(75)",
                maxLength: 75,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GcSenderName",
                table: "OrderLineItems",
                type: "nvarchar(75)",
                maxLength: 75,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CcEmail",
                table: "EmailQueue",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEntityId",
                table: "EmailQueue",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyToEmail",
                table: "EmailQueue",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GiftCertificateId",
                table: "Carts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GcMessage",
                table: "CartItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GcRecipientEmail",
                table: "CartItems",
                type: "nvarchar(75)",
                maxLength: 75,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GcRecipientName",
                table: "CartItems",
                type: "nvarchar(75)",
                maxLength: 75,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GcSenderEmail",
                table: "CartItems",
                type: "nvarchar(75)",
                maxLength: 75,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GcSenderName",
                table: "CartItems",
                type: "nvarchar(75)",
                maxLength: 75,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GiftCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OriginalBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    SourceOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceUnitIndex = table.Column<int>(type: "int", nullable: true),
                    IsOnline = table.Column<bool>(type: "bit", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: true),
                    SenderEmail = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: true),
                    RecipientName = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: true),
                    RecipientEmail = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EmailSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCertificates", x => x.Id);
                    table.CheckConstraint("CK_GiftCertificates_CurrentBalance", "[CurrentBalance] >= 0");
                    table.CheckConstraint("CK_GiftCertificates_OriginalBalance", "[OriginalBalance] >= 0");
                    table.ForeignKey(
                        name: "FK_GiftCertificates_Orders_SourceOrderId",
                        column: x => x.SourceOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GiftCertificates_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GiftCertificateTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiftCertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<byte>(type: "tinyint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftCertificateTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiftCertificateTransactions_GiftCertificates_GiftCertificateId",
                        column: x => x.GiftCertificateId,
                        principalTable: "GiftCertificates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_GiftCertificateId",
                table: "Orders",
                column: "GiftCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftCertificates_CreatedOnUtc",
                table: "GiftCertificates",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GiftCertificates_SourceOrderId",
                table: "GiftCertificates",
                column: "SourceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_GiftCertificates_SourceOrderItemId_SourceUnitIndex",
                table: "GiftCertificates",
                columns: new[] { "SourceOrderItemId", "SourceUnitIndex" },
                unique: true,
                filter: "[SourceOrderItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GiftCertificates_StoreId_Code",
                table: "GiftCertificates",
                columns: new[] { "StoreId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiftCertificateTransactions_GiftCertificateId_CreatedOnUtc",
                table: "GiftCertificateTransactions",
                columns: new[] { "GiftCertificateId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GiftCertificateTransactions_OrderId",
                table: "GiftCertificateTransactions",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GiftCertificateTransactions");

            migrationBuilder.DropTable(
                name: "GiftCertificates");

            migrationBuilder.DropIndex(
                name: "IX_Orders_GiftCertificateId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GiftCertificateOnline",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsMaster",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "GiftCertificateAppliedAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GiftCertificateCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GiftCertificateId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GcMessage",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "GcRecipientEmail",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "GcRecipientName",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "GcSenderEmail",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "GcSenderName",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "CcEmail",
                table: "EmailQueue");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "EmailQueue");

            migrationBuilder.DropColumn(
                name: "ReplyToEmail",
                table: "EmailQueue");

            migrationBuilder.DropColumn(
                name: "GiftCertificateId",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "GcMessage",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "GcRecipientEmail",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "GcRecipientName",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "GcSenderEmail",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "GcSenderName",
                table: "CartItems");
        }
    }
}
