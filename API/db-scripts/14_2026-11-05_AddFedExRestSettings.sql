/* =====================================================================================
 FedEx shipping: REST Rate API settings + rating fixes — docs/fedex-shipping-requirements.md
 Generated from EF migration AddFedExRestSettings:
   dotnet ef migrations script AddUpsRestSettings AddFedExRestSettings --idempotent

 Run AFTER 13_2026-11-05_AddUpsRestSettings.sql. Additive only — Credentials_FedEx rating settings:
   DisplayName                          "FedEx - <service>" (FX-14)
   RateMode                             ACCOUNT / LIST — the only rate type requested and read (FX-13)
   AllowedServices                      comma-separated FedEx service types (FX-14, FX-19, §3.2)
   FflAllowedServices                   optional FFL-cart service filter (FX-21 [ENH]); empty = none
   PickupType                           USE_SCHEDULED_PICKUP / DROPOFF_AT_FEDEX_LOCATION (FX-50)
   SendPackageInfo                      one package per unit with product dimensions (FX-16)
   DefaultLengthIn / WidthIn / HeightIn default package, inches (§4)
   HandlingFee, MinimumOrderAmount      (FX-15, FX-10)
   ShipToCountries                      comma-separated ISO-2; blank = all (§4)
   LoggingEnabled                       (FX-56)
 Then back-fills existing FedEx rows with the FX-19 parcel defaults (FEDEX_GROUND, GROUND_HOME_DELIVERY,
 FEDEX_EXPRESS_SAVER, FEDEX_2_DAY, STANDARD_OVERNIGHT, PRIORITY_OVERNIGHT), ACCOUNT rates and scheduled pickup.

 Idempotent: guarded by __EFMigrationsHistory, safe to re-run.
 Do not hand-edit the migration part. Change the entities/configurations, add a migration, regenerate.
===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [AllowedServices] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [DefaultHeightIn] decimal(9,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [DefaultLengthIn] decimal(9,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [DefaultWidthIn] decimal(9,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [DisplayName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [FflAllowedServices] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [HandlingFee] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [LoggingEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [MinimumOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [PickupType] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [RateMode] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [SendPackageInfo] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [ShipToCountries] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    EXEC(N'UPDATE [Credentials_FedEx] SET [DisplayName] = N''FedEx'', [RateMode] = N''ACCOUNT'', [PickupType] = N''USE_SCHEDULED_PICKUP'', [AllowedServices] = N''FEDEX_GROUND,GROUND_HOME_DELIVERY,FEDEX_EXPRESS_SAVER,FEDEX_2_DAY,STANDARD_OVERNIGHT,PRIORITY_OVERNIGHT'' WHERE [AllowedServices] IS NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105133944_AddFedExRestSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105133944_AddFedExRestSettings', N'9.0.0');
END;

COMMIT;
GO

