using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSquareLocationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationId",
                table: "Credentials_Square",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Credentials_Square");
        }
    }
}
