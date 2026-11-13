/* =====================================================================================
 Checkout settings, low-stock checkout lock and customer blacklist
 docs/checkout-shipping-payment-settings-requirements.md (CK-01..23, SH-23/40-43, PY-10..15, PY-40..43)
 Generated from EF migration AddCheckoutSettingsLockBlacklist:
   dotnet ef migrations script AddShippingMethodCommonSettings AddCheckoutSettingsLockBlacklist --idempotent

 Run AFTER 17_2026-11-06_AddShippingMethodCommonSettings.sql. Additive only.
   Carts                   LastCheckoutActivityUtc (checkout session timeout, CK-01/02)
   CheckoutLocks           one row per locked low-stock product (CK-20..23)
   CustomerBlacklist       blocked names / emails (PY-10)
   CustomerBlacklistHits   log of refused placement attempts (PY-15)
 The settings themselves are Checkout.* rows in PlatformSettings, created on first save from
 the admin "Checkout Settings" page; until then the legacy defaults apply.
 The "checkout.blacklist-alert" email template is seeded automatically at startup.

 Idempotent: guarded by __EFMigrationsHistory, safe to re-run.
 Do not hand-edit the migration part. Change the entities/configurations, add a migration, regenerate.
===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111063734_AddCheckoutSettingsLockBlacklist'
)
BEGIN
    ALTER TABLE [Carts] ADD [LastCheckoutActivityUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111063734_AddCheckoutSettingsLockBlacklist'
)
BEGIN
    CREATE TABLE [CheckoutLocks] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [OwnerCartId] uniqueidentifier NOT NULL,
        [LockedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CheckoutLocks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111063734_AddCheckoutSettingsLockBlacklist'
)
BEGIN
    CREATE TABLE [CustomerBlacklist] (
        [Id] uniqueidentifier NOT NULL,
        [FirstName] nvarchar(100) NULL,
        [LastName] nvarchar(100) NULL,
        [Email] nvarchar(256) NULL,
        [Reason] nvarchar(1000) NULL,
        [Status] int NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomerBlacklist] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111063734_AddCheckoutSettingsLockBlacklist'
)
BEGIN
    CREATE TABLE [CustomerBlacklistHits] (
        [Id] uniqueidentifier NOT NULL,
        [BlacklistEntryId] uniqueidentifier NOT NULL,
        [CartId] uniqueidentifier NULL,
        [FirstName] nvarchar(100) NULL,
        [LastName] nvarchar(100) NULL,
        [Email] nvarchar(256) NULL,
        [IpAddress] nvarchar(64) NULL,
        [MatchedOn] nvarchar(20) NOT NULL,
        [AtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomerBlacklistHits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerBlacklistHits_CustomerBlacklist_BlacklistEntryId] FOREIGN KEY ([BlacklistEntryId]) REFERENCES [CustomerBlacklist] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111063734_AddCheckoutSettingsLockBlacklist'
)
BEGIN
    CREATE INDEX [IX_CheckoutLocks_OwnerCartId] ON [CheckoutLocks] ([OwnerCartId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111063734_AddCheckoutSettingsLockBlacklist'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CheckoutLocks_ProductId] ON [CheckoutLocks] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111063734_AddCheckoutSettingsLockBlacklist'
)
BEGIN
    CREATE INDEX [IX_CustomerBlacklist_Email] ON [CustomerBlacklist] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111063734_AddCheckoutSettingsLockBlacklist'
)
BEGIN
    CREATE INDEX [IX_CustomerBlacklist_LastName_FirstName] ON [CustomerBlacklist] ([LastName], [FirstName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111063734_AddCheckoutSettingsLockBlacklist'
)
BEGIN
    CREATE INDEX [IX_CustomerBlacklistHits_AtUtc] ON [CustomerBlacklistHits] ([AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111063734_AddCheckoutSettingsLockBlacklist'
)
BEGIN
    CREATE INDEX [IX_CustomerBlacklistHits_BlacklistEntryId] ON [CustomerBlacklistHits] ([BlacklistEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111063734_AddCheckoutSettingsLockBlacklist'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111063734_AddCheckoutSettingsLockBlacklist', N'9.0.0');
END;

COMMIT;
GO

