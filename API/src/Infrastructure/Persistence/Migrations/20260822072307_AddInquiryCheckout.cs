using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInquiryCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ProtectInquiry",
                table: "RecaptchaConfigs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "InquiryButtonLabel",
                table: "Products",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInquiryOnly",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerNote",
                table: "Orders",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InquiryStatus",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalNotes",
                table: "Orders",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInquiry",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PreferredContact",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuotedOnUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequiredByUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_IsInquiry",
                table: "Orders",
                column: "IsInquiry");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_IsInquiry",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ProtectInquiry",
                table: "RecaptchaConfigs");

            migrationBuilder.DropColumn(
                name: "InquiryButtonLabel",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsInquiryOnly",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerNote",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InquiryStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InternalNotes",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsInquiry",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PreferredContact",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "QuotedOnUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RequiredByUtc",
                table: "Orders");
        }
    }
}
