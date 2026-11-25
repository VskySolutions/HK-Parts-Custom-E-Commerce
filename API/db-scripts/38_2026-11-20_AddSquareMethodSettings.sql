-- 38  2026-11-20  AddSquareMethodSettings
-- Square method settings from the PHP admin on Credentials_Square: TransactionType, RefreshToken (encrypted by
-- the app), AccessTokenExpiresAtUtc, MinOrderAmount, MaxOrderAmount, AllowedCustomerGroupIds, DefaultOrderStatus,
-- SortOrder. Run after 37. Idempotent; the app applies the same migration at startup.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120120935_AddSquareMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Square] ADD [AccessTokenExpiresAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120120935_AddSquareMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Square] ADD [AllowedCustomerGroupIds] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120120935_AddSquareMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Square] ADD [DefaultOrderStatus] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120120935_AddSquareMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Square] ADD [MaxOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120120935_AddSquareMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Square] ADD [MinOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120120935_AddSquareMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Square] ADD [RefreshToken] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120120935_AddSquareMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Square] ADD [SortOrder] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120120935_AddSquareMethodSettings'
)
BEGIN
    ALTER TABLE [Credentials_Square] ADD [TransactionType] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120120935_AddSquareMethodSettings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120120935_AddSquareMethodSettings', N'9.0.0');
END;

COMMIT;
GO

