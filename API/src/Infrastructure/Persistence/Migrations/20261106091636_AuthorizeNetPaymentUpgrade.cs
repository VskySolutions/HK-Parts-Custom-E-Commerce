using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuthorizeNetPaymentUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthCode",
                table: "PaymentRecords",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AuthorizedAmount",
                table: "PaymentRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvsResultCode",
                table: "PaymentRecords",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardLastFour",
                table: "PaymentRecords",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardType",
                table: "PaymentRecords",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPaymentProfileId",
                table: "PaymentRecords",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerProfileId",
                table: "PaymentRecords",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvvResultCode",
                table: "PaymentRecords",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayReasonCode",
                table: "PaymentRecords",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayResponseCode",
                table: "PaymentRecords",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayResponseText",
                table: "PaymentRecords",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NetworkTransactionId",
                table: "PaymentRecords",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedCardTypes",
                table: "Credentials_AuthorizeNet",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedCustomerGroupIds",
                table: "Credentials_AuthorizeNet",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CustomerProfilesEnabled",
                table: "Credentials_AuthorizeNet",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DefaultOrderStatus",
                table: "Credentials_AuthorizeNet",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Credentials_AuthorizeNet",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LoggingEnabled",
                table: "Credentials_AuthorizeNet",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxOrderAmount",
                table: "Credentials_AuthorizeNet",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinOrderAmount",
                table: "Credentials_AuthorizeNet",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "Credentials_AuthorizeNet",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RefTransactionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponseCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_PaymentRecords_PaymentRecordId",
                        column: x => x.PaymentRecordId,
                        principalTable: "PaymentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PaymentRecordId",
                table: "PaymentTransactions",
                column: "PaymentRecordId");

            // Existing Authorize.Net rows keep today's behaviour: the legacy method row was "Credit Card",
            // auth-and-capture (anet_trans_type = authcapture). EXEC keeps the idempotent db-script valid,
            // since these columns are added in this same batch.
            migrationBuilder.Sql(
                "EXEC(N'UPDATE [Credentials_AuthorizeNet] SET [TransactionType] = N''AuthCapture'' WHERE [TransactionType] IS NULL; " +
                "UPDATE [Credentials_AuthorizeNet] SET [DisplayName] = N''Credit Card'' WHERE [DisplayName] IS NULL;')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "AuthCode",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "AuthorizedAmount",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "AvsResultCode",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "CardLastFour",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "CardType",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "CustomerPaymentProfileId",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "CustomerProfileId",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "CvvResultCode",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "GatewayReasonCode",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "GatewayResponseCode",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "GatewayResponseText",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "NetworkTransactionId",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "AcceptedCardTypes",
                table: "Credentials_AuthorizeNet");

            migrationBuilder.DropColumn(
                name: "AllowedCustomerGroupIds",
                table: "Credentials_AuthorizeNet");

            migrationBuilder.DropColumn(
                name: "CustomerProfilesEnabled",
                table: "Credentials_AuthorizeNet");

            migrationBuilder.DropColumn(
                name: "DefaultOrderStatus",
                table: "Credentials_AuthorizeNet");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Credentials_AuthorizeNet");

            migrationBuilder.DropColumn(
                name: "LoggingEnabled",
                table: "Credentials_AuthorizeNet");

            migrationBuilder.DropColumn(
                name: "MaxOrderAmount",
                table: "Credentials_AuthorizeNet");

            migrationBuilder.DropColumn(
                name: "MinOrderAmount",
                table: "Credentials_AuthorizeNet");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "Credentials_AuthorizeNet");
        }
    }
}
