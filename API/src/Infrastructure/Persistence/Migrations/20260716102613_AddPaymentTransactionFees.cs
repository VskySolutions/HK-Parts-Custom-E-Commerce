using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransactionFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaymentFeePercent",
                table: "Orders",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaymentFeeTotal",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_USPS",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_UPS",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_Twilio",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_TaxJar",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_StripeTax",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_Stripe",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_Square",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_Razorpay",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_PayPal",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_FedEx",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_DHLExpress",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_AzureBlob",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionFeePercent",
                table: "Credentials_AuthorizeNet",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentFeePercent",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentFeeTotal",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_USPS");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_UPS");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_Twilio");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_TaxJar");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_StripeTax");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_Stripe");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_Square");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_Razorpay");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_PayPal");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_FedEx");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_DHLExpress");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_AzureBlob");

            migrationBuilder.DropColumn(
                name: "TransactionFeePercent",
                table: "Credentials_AuthorizeNet");
        }
    }
}
