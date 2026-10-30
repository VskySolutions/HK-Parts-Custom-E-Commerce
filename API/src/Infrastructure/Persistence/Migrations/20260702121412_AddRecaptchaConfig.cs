using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecaptchaConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecaptchaConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SiteKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SecretKeyEncrypted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecretKeyLast4 = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ScoreThreshold = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    FailBehaviour = table.Column<int>(type: "int", nullable: false),
                    ProtectRegister = table.Column<bool>(type: "bit", nullable: false),
                    ProtectLogin = table.Column<bool>(type: "bit", nullable: false),
                    ProtectPasswordReset = table.Column<bool>(type: "bit", nullable: false),
                    ProtectGuestCheckout = table.Column<bool>(type: "bit", nullable: false),
                    ProtectContact = table.Column<bool>(type: "bit", nullable: false),
                    ProtectNewsletter = table.Column<bool>(type: "bit", nullable: false),
                    ProtectReviewSubmit = table.Column<bool>(type: "bit", nullable: false),
                    ProtectQaSubmit = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecaptchaConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecaptchaConfigs");
        }
    }
}
