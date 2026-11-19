/* =====================================================================================
 Checkout: FFL shipping information — docs/ffl-shipping-information-requirements.md
 Generated from EF migration AddFflDealerShipping:
   dotnet ef migrations script AddReturnsWorkflow AddFflDealerShipping --idempotent

 Run AFTER 23_2026-11-13_AddReturnsWorkflow.sql. Additive:
   - New table FflDealers: the licensed dealer directory (ATF licensee list + store dealer data:
     AllowsTransfers, LastTransferFee, AverageReviewRating). Unique Key (region + district + sequence).
   - New table ZipGeocodes: ZIP centre cache for the radius search (unique Zip).
   - Carts.FflDealerKey: the dealer the buyer selected at checkout.
   - OrderShippings: FFL dealer ship-to (key, street, city, state, zip, phone, source).
   - Orders.HasFflItems, OrderLineItems.IsFfl.
 Back-fill: order lines of FFL-required products get IsFfl = 1; orders with such a line, or whose frozen
 shipment held FFL items, get HasFflItems = 1.
 The directory starts empty: load the legacy PHP dealers with 25_2026-11-16_ImportLegacyFflDealers.sql, or
 import the ATF list on the admin FFL Dealers page. FflDealers / ZipGeocodes are only created when missing, so
 this script also works after script 25 created them.
 Idempotent: guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.
 The two SET lines below are the only addition to the generated script: the back-fill UPDATEs touch tables
 with filtered indexes, which need QUOTED_IDENTIFIER ON (SSMS has it on; sqlcmd does not by default).
 ===================================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    ALTER TABLE [OrderShippings] ADD [FflDealerCity] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    ALTER TABLE [OrderShippings] ADD [FflDealerKey] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    ALTER TABLE [OrderShippings] ADD [FflDealerPhone] nvarchar(30) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    ALTER TABLE [OrderShippings] ADD [FflDealerState] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    ALTER TABLE [OrderShippings] ADD [FflDealerStreet] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    ALTER TABLE [OrderShippings] ADD [FflDealerZip] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    ALTER TABLE [OrderShippings] ADD [FflSource] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    ALTER TABLE [Orders] ADD [HasFflItems] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    ALTER TABLE [OrderLineItems] ADD [IsFfl] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    ALTER TABLE [Carts] ADD [FflDealerKey] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN

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
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN

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
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    EXEC(N'UPDATE l SET l.IsFfl = 1 FROM OrderLineItems l INNER JOIN Products p ON p.Id = l.ProductId WHERE p.IsFflRequired = 1 AND l.IsFfl = 0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    EXEC(N'UPDATE o SET o.HasFflItems = 1 FROM Orders o WHERE o.HasFflItems = 0 AND (EXISTS (SELECT 1 FROM OrderShippings s WHERE s.OrderId = o.Id AND s.ContainsFflItems = 1) OR EXISTS (SELECT 1 FROM OrderLineItems l WHERE l.OrderId = o.Id AND l.IsFfl = 1));');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116092834_AddFflDealerShipping'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261116092834_AddFflDealerShipping', N'9.0.0');
END;

COMMIT;
GO

