-- 36  2026-11-20  AddStripeMethodSettings
-- Stripe method settings from the PHP admin: TransactionType, CustomerProfilesEnabled, AcceptedCardTypes,
-- MinOrderAmount, MaxOrderAmount, AllowedCustomerGroupIds, DefaultOrderStatus, SortOrder on Credentials_Stripe.
-- Run after 35. Idempotent: safe to run more than once; the app applies the same migration at startup.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120111730_AddStripeMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Stripe] ADD [AcceptedCardTypes] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120111730_AddStripeMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Stripe] ADD [AllowedCustomerGroupIds] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120111730_AddStripeMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Stripe] ADD [CustomerProfilesEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120111730_AddStripeMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Stripe] ADD [DefaultOrderStatus] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120111730_AddStripeMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Stripe] ADD [MaxOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120111730_AddStripeMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Stripe] ADD [MinOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120111730_AddStripeMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Stripe] ADD [SortOrder] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120111730_AddStripeMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Stripe] ADD [TransactionType] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120111730_AddStripeMethodSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120111730_AddStripeMethodSettings', N'9.0.0');
END;

COMMIT;
GO

