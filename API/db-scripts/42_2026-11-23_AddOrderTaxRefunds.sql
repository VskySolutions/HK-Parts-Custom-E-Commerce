-- 42  2026-11-23  AddOrderTaxRefunds
-- Tax on refunds: Orders.TaxRefundedTotal (tax handed back by admin refunds / returns, taken off the Tax Report)
-- and Orders.TaxReturnDocumentCodes (Avalara ReturnInvoice codes, voided with the order on cancel).
-- Run after 41. Idempotent; the app applies the same migration at startup.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123083829_AddOrderTaxRefunds'
)
BEGIN
    ALTER TABLE [Orders] ADD [TaxRefundedTotal] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123083829_AddOrderTaxRefunds'
)
BEGIN
    ALTER TABLE [Orders] ADD [TaxReturnDocumentCodes] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123083829_AddOrderTaxRefunds'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261123083829_AddOrderTaxRefunds', N'9.0.0');
END;

COMMIT;
GO

