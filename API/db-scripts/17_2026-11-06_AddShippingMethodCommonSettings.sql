/* =====================================================================================
 Common shipping-method settings (AD-04) — docs/shipping-requirements.md §6.1,
 docs/shipping-order-placement-requirements.md §4
 Generated from EF migration AddShippingMethodCommonSettings:
   dotnet ef migrations script AddOrderShippingSnapshot AddShippingMethodCommonSettings --idempotent

 Run AFTER 16_2026-11-06_AddOrderShippingSnapshot.sql. Additive only (all columns nullable).
   ShippingMethods          HandlingFee, MinimumOrderAmount, ShipToCountries (custom methods:
                            flat rate, table rate, free, size-based)
   Credentials_Freightview  DisplayName, HandlingFee, MinimumOrderAmount, ShipToCountries
 USPS / UPS / FedEx already carry these settings (scripts 12-14).

 Idempotent: guarded by __EFMigrationsHistory, safe to re-run.
 Do not hand-edit the migration part. Change the entities/configurations, add a migration, regenerate.
===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106111750_AddShippingMethodCommonSettings'
)
BEGIN
    ALTER TABLE [ShippingMethods] ADD [HandlingFee] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106111750_AddShippingMethodCommonSettings'
)
BEGIN
    ALTER TABLE [ShippingMethods] ADD [MinimumOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106111750_AddShippingMethodCommonSettings'
)
BEGIN
    ALTER TABLE [ShippingMethods] ADD [ShipToCountries] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106111750_AddShippingMethodCommonSettings'
)
BEGIN
    ALTER TABLE [Credentials_Freightview] ADD [DisplayName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106111750_AddShippingMethodCommonSettings'
)
BEGIN
    ALTER TABLE [Credentials_Freightview] ADD [HandlingFee] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106111750_AddShippingMethodCommonSettings'
)
BEGIN
    ALTER TABLE [Credentials_Freightview] ADD [MinimumOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106111750_AddShippingMethodCommonSettings'
)
BEGIN
    ALTER TABLE [Credentials_Freightview] ADD [ShipToCountries] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106111750_AddShippingMethodCommonSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106111750_AddShippingMethodCommonSettings', N'9.0.0');
END;

COMMIT;
GO

