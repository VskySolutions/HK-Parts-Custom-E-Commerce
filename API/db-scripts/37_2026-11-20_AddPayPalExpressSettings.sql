-- 37  2026-11-20  AddPayPalExpressSettings
-- PayPal Express settings from the PHP admin on Credentials_PayPal: classic API ApiUsername / ApiPassword /
-- ApiSignature (encrypted by the app), BrandName (store name shown in PayPal), MinOrderAmount, MaxOrderAmount,
-- AllowedCustomerGroupIds, DefaultOrderStatus, SortOrder. Run after 36. Idempotent; the app applies it at startup.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120112837_AddPayPalExpressSettings'
)
BEGIN
    ALTER TABLE [Credentials_PayPal] ADD [AllowedCustomerGroupIds] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120112837_AddPayPalExpressSettings'
)
BEGIN
    ALTER TABLE [Credentials_PayPal] ADD [ApiPassword] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120112837_AddPayPalExpressSettings'
)
BEGIN
    ALTER TABLE [Credentials_PayPal] ADD [ApiSignature] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120112837_AddPayPalExpressSettings'
)
BEGIN
    ALTER TABLE [Credentials_PayPal] ADD [ApiUsername] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120112837_AddPayPalExpressSettings'
)
BEGIN
    ALTER TABLE [Credentials_PayPal] ADD [BrandName] nvarchar(127) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120112837_AddPayPalExpressSettings'
)
BEGIN
    ALTER TABLE [Credentials_PayPal] ADD [DefaultOrderStatus] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120112837_AddPayPalExpressSettings'
)
BEGIN
    ALTER TABLE [Credentials_PayPal] ADD [MaxOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120112837_AddPayPalExpressSettings'
)
BEGIN
    ALTER TABLE [Credentials_PayPal] ADD [MinOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120112837_AddPayPalExpressSettings'
)
BEGIN
    ALTER TABLE [Credentials_PayPal] ADD [SortOrder] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120112837_AddPayPalExpressSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120112837_AddPayPalExpressSettings', N'9.0.0');
END;

COMMIT;
GO

