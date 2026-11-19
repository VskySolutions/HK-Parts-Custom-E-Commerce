using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFflDealerShipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FflDealerCity",
                table: "OrderShippings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FflDealerKey",
                table: "OrderShippings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FflDealerPhone",
                table: "OrderShippings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FflDealerState",
                table: "OrderShippings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FflDealerStreet",
                table: "OrderShippings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FflDealerZip",
                table: "OrderShippings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FflSource",
                table: "OrderShippings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasFflItems",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFfl",
                table: "OrderLineItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FflDealerKey",
                table: "Carts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // The directory tables may already exist: db-scripts/25 (the legacy dealer data load) creates them
            // when it runs before the app has migrated. Create them only when missing; same shape as the model.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[FflDealers]', N'U') IS NULL
BEGIN
    CREATE TABLE [FflDealers] (
        [Id] uniqueidentifier NOT NULL,
        [Key] nvarchar(20) NOT NULL,
        [LicRegn] nvarchar(2) NOT NULL,
        [LicDist] nvarchar(3) NOT NULL,
        [LicCnty] nvarchar(5) NOT NULL,
        [LicType] nvarchar(3) NOT NULL,
        [LicXprdte] nvarchar(4) NOT NULL,
        [LicSeqn] nvarchar(10) NOT NULL,
        [LicenseName] nvarchar(255) NULL,
        [BusinessName] nvarchar(255) NULL,
        [PremiseStreet] nvarchar(200) NULL,
        [PremiseCity] nvarchar(100) NULL,
        [PremiseState] nvarchar(50) NULL,
        [PremiseZipCode] nvarchar(20) NULL,
        [MailStreet] nvarchar(200) NULL,
        [MailCity] nvarchar(100) NULL,
        [MailState] nvarchar(50) NULL,
        [MailZipCode] nvarchar(20) NULL,
        [VoicePhone] nvarchar(30) NULL,
        [Latitude] float NULL,
        [Longitude] float NULL,
        [IsActive] bit NOT NULL,
        [LastImportedOnUtc] datetime2 NULL,
        [AllowsTransfers] bit NULL,
        [LastTransferFee] decimal(18,2) NULL,
        [AverageReviewRating] decimal(5,2) NULL,
        [DealerDataUpdatedOnUtc] datetime2 NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_FflDealers] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_FflDealers_Key] ON [FflDealers] ([Key]);
    CREATE INDEX [IX_FflDealers_LicType_Latitude_Longitude] ON [FflDealers] ([LicType], [Latitude], [Longitude]) INCLUDE ([IsActive], [AllowsTransfers]);
    CREATE INDEX [IX_FflDealers_PremiseZipCode] ON [FflDealers] ([PremiseZipCode]);
END;");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ZipGeocodes]', N'U') IS NULL
BEGIN
    CREATE TABLE [ZipGeocodes] (
        [Id] uniqueidentifier NOT NULL,
        [Zip] nvarchar(10) NOT NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [Source] nvarchar(30) NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ZipGeocodes] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_ZipGeocodes_Zip] ON [ZipGeocodes] ([Zip]);
END;");

            // Back-fill: lines of FFL-required products, and orders whose frozen shipment held FFL items.
            // EXEC so the idempotent script (one batch) compiles before the new columns exist.
            migrationBuilder.Sql(@"EXEC(N'UPDATE l SET l.IsFfl = 1 FROM OrderLineItems l INNER JOIN Products p ON p.Id = l.ProductId WHERE p.IsFflRequired = 1 AND l.IsFfl = 0;');");
            migrationBuilder.Sql(@"EXEC(N'UPDATE o SET o.HasFflItems = 1 FROM Orders o WHERE o.HasFflItems = 0 AND (EXISTS (SELECT 1 FROM OrderShippings s WHERE s.OrderId = o.Id AND s.ContainsFflItems = 1) OR EXISTS (SELECT 1 FROM OrderLineItems l WHERE l.OrderId = o.Id AND l.IsFfl = 1));');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FflDealers");

            migrationBuilder.DropTable(
                name: "ZipGeocodes");

            migrationBuilder.DropColumn(
                name: "FflDealerCity",
                table: "OrderShippings");

            migrationBuilder.DropColumn(
                name: "FflDealerKey",
                table: "OrderShippings");

            migrationBuilder.DropColumn(
                name: "FflDealerPhone",
                table: "OrderShippings");

            migrationBuilder.DropColumn(
                name: "FflDealerState",
                table: "OrderShippings");

            migrationBuilder.DropColumn(
                name: "FflDealerStreet",
                table: "OrderShippings");

            migrationBuilder.DropColumn(
                name: "FflDealerZip",
                table: "OrderShippings");

            migrationBuilder.DropColumn(
                name: "FflSource",
                table: "OrderShippings");

            migrationBuilder.DropColumn(
                name: "HasFflItems",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsFfl",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "FflDealerKey",
                table: "Carts");
        }
    }
}
