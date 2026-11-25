-- 39  2026-11-20  AddSquareDisplayName
-- Square "Display Name" (PHP parity): Credentials_Square.DisplayName, the method name shown at checkout.
-- Run after 38. Idempotent; the app applies the same migration at startup.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120135641_AddSquareDisplayName'
)
BEGIN
    ALTER TABLE [Credentials_Square] ADD [DisplayName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120135641_AddSquareDisplayName'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120135641_AddSquareDisplayName', N'9.0.0');
END;

COMMIT;
GO

