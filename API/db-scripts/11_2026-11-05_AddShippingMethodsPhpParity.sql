/* =====================================================================================
 Shipping methods: PHP (Laravel / Edreamz ecommcore) parity — docs/shipping-requirements.md §2
 Generated from EF migration AddShippingMethodsPhpParity:
   dotnet ef migrations script AddGiftCertificates AddShippingMethodsPhpParity --idempotent

 Run AFTER 10_2026-11-04_AddGiftCertificates.sql. Additive only:
   - ShippingMethods.FlatRatePerItem      flat rate charged per item instead of per order (SR-61)
   - ShippingMethods.FflFlatRate          size-based method: flat charge for FFL items (SR-22)
   - ShippingMethods.InternationalViaUsps size-based method: cheapest USPS rate abroad (SR-23..25)
   - Credentials_Freightview              Freightview LTL API key + dock/pickup details (SR-70/71)
 Then seeds the legacy "Standard Shipping" size-based method (SR-20/21/22) — DISABLED, so turning
 it on is a deliberate admin step (Shipping → Methods). Skipped if a size-based method exists.

 Idempotent: guarded by __EFMigrationsHistory / NOT EXISTS, safe to re-run.
 Do not hand-edit the migration part. Change the entities/configurations, add a migration, regenerate.
===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105070225_AddShippingMethodsPhpParity'
)
BEGIN
    ALTER TABLE [ShippingMethods] ADD [FflFlatRate] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105070225_AddShippingMethodsPhpParity'
)
BEGIN
    ALTER TABLE [ShippingMethods] ADD [FlatRatePerItem] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105070225_AddShippingMethodsPhpParity'
)
BEGIN
    ALTER TABLE [ShippingMethods] ADD [InternationalViaUsps] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105070225_AddShippingMethodsPhpParity'
)
BEGIN
    CREATE TABLE [Credentials_Freightview] (
        [Id] uniqueidentifier NOT NULL,
        [BaseUrl] nvarchar(max) NULL,
        [ApiKey] nvarchar(max) NULL,
        [OriginType] nvarchar(50) NULL,
        [ContactName] nvarchar(200) NULL,
        [ContactPhone] nvarchar(50) NULL,
        [ContactEmail] nvarchar(256) NULL,
        [PickupInstructions] nvarchar(1000) NULL,
        [DockOpenTime] nvarchar(20) NULL,
        [DockCloseTime] nvarchar(20) NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        [Active] bit NOT NULL,
        [IsProduction] bit NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [TransactionFeePercent] decimal(5,2) NULL,
        [Deleted] bit NOT NULL,
        [DeletedOnUtc] datetime2 NULL,
        CONSTRAINT [PK_Credentials_Freightview] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105070225_AddShippingMethodsPhpParity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105070225_AddShippingMethodsPhpParity', N'9.0.0');
END;

COMMIT;
GO


/* ---- Seed: legacy "Standard Shipping" (MethodType 4 = SizeBased) ------------------------------- */
IF NOT EXISTS (SELECT 1 FROM [ShippingMethods] WHERE [MethodType] = 4 AND [Deleted] = 0)
BEGIN
    INSERT INTO [ShippingMethods]
        ([Id], [Name], [MethodType], [FlatRate], [FreeShippingThreshold], [TiersJson], [TransitDays],
         [IsEnabled], [DisplayOrder], [Deleted], [DeletedOnUtc], [CreatedOnUtc], [UpdatedOnUtc],
         [FlatRatePerItem], [FflFlatRate], [InternationalViaUsps])
    VALUES
        (NEWID(), N'Standard Shipping', 4, 9.50, NULL,
         N'[{"size":"Free","rate":0.00},{"size":"Small","rate":9.50},{"size":"Medium","rate":16.50},{"size":"Large","rate":22.50},{"size":"Extra Large","rate":28.00}]',
         NULL, 0, 1, 0, NULL, SYSUTCDATETIME(), SYSUTCDATETIME(), 0, 40.00, 1);
END;
GO
