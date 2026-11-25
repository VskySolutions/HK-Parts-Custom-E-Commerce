-- 35  2026-11-20  AddGuestCheckoutFollowUps
-- Guest checkout follow-ups (docs/guest-checkout-requirements.md): Orders.CancelRequestedOnUtc / CancelRequestReason
-- (U-63 guest cancel request), Carts.ContactEmail / MarketingOptIn (A-43 abandoned carts), Guests.AnonymizedOnUtc (N-05).
-- Run after 34. Idempotent: safe to run more than once; the app applies the same migration at startup.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120091813_AddGuestCheckoutFollowUps'
)
BEGIN
    ALTER TABLE [Orders] ADD [CancelRequestReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120091813_AddGuestCheckoutFollowUps'
)
BEGIN
    ALTER TABLE [Orders] ADD [CancelRequestedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120091813_AddGuestCheckoutFollowUps'
)
BEGIN
    ALTER TABLE [Guests] ADD [AnonymizedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120091813_AddGuestCheckoutFollowUps'
)
BEGIN
    ALTER TABLE [Carts] ADD [ContactEmail] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120091813_AddGuestCheckoutFollowUps'
)
BEGIN
    ALTER TABLE [Carts] ADD [MarketingOptIn] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120091813_AddGuestCheckoutFollowUps'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120091813_AddGuestCheckoutFollowUps', N'9.0.0');
END;

COMMIT;
GO

