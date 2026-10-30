using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationCredentialVault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationProviders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationProviders_IntegrationCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "IntegrationCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CredentialDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FieldCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DataType = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsSecret = table.Column<bool>(type: "bit", nullable: false),
                    Placeholder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HelpText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CredentialDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CredentialDefinitions_IntegrationProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "IntegrationProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastFourChars = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    IsSecret = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationCredentials_CredentialDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "CredentialDefinitions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IntegrationCredentials_IntegrationProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "IntegrationProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CredentialDefinitions_ProviderId_FieldCode",
                table: "CredentialDefinitions",
                columns: new[] { "ProviderId", "FieldCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCategories_Code",
                table: "IntegrationCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCredentials_DefinitionId",
                table: "IntegrationCredentials",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCredentials_ProviderId_DefinitionId",
                table: "IntegrationCredentials",
                columns: new[] { "ProviderId", "DefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationProviders_CategoryId",
                table: "IntegrationProviders",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationProviders_Code",
                table: "IntegrationProviders",
                column: "Code",
                unique: true,
                filter: "[Deleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationCredentials");

            migrationBuilder.DropTable(
                name: "CredentialDefinitions");

            migrationBuilder.DropTable(
                name: "IntegrationProviders");

            migrationBuilder.DropTable(
                name: "IntegrationCategories");
        }
    }
}
