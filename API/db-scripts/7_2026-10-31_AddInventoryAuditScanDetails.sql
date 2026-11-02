/* =====================================================================================
 Barcode inventory scanner: scan details on the audit log
 Generated from EF migration AddInventoryAuditScanDetails:
   dotnet ef migrations script AddInventoryAuditLog AddInventoryAuditScanDetails --idempotent

 Run AFTER 2026-10-31_AddInventoryAuditLog.sql. Additive only: adds Source (camera / gallery /
 manual / handheld scanner, existing rows = 1 Camera), MatchedBy (SKU / barcode), ClientIp and
 UserAgent to InventoryAuditLogs, plus a non-negative check on NewQty / NewPrice. Idempotent:
 guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.

 Pre-flight (optional): products / variants sharing a SKU or barcode make the scanner answer
 "several products match" (409 + picker) instead of opening one. List them with:
   SELECT 'SKU' AS KeyType, Sku AS Value, COUNT(*) AS Cnt FROM Products
    WHERE Deleted = 0 AND Sku <> '' GROUP BY Sku HAVING COUNT(*) > 1
   UNION ALL
   SELECT 'BARCODE', Gtin, COUNT(*) FROM Products
    WHERE Deleted = 0 AND Gtin <> '' GROUP BY Gtin HAVING COUNT(*) > 1;
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031113440_AddInventoryAuditScanDetails'
)
BEGIN
    ALTER TABLE [InventoryAuditLogs] ADD [ClientIp] nvarchar(45) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031113440_AddInventoryAuditScanDetails'
)
BEGIN
    ALTER TABLE [InventoryAuditLogs] ADD [MatchedBy] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031113440_AddInventoryAuditScanDetails'
)
BEGIN
    ALTER TABLE [InventoryAuditLogs] ADD [Source] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031113440_AddInventoryAuditScanDetails'
)
BEGIN
    ALTER TABLE [InventoryAuditLogs] ADD [UserAgent] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031113440_AddInventoryAuditScanDetails'
)
BEGIN
    EXEC(N'ALTER TABLE [InventoryAuditLogs] ADD CONSTRAINT [CK_InventoryAuditLogs_NonNegative] CHECK (([NewQty] IS NULL OR [NewQty] >= 0) AND ([NewPrice] IS NULL OR [NewPrice] >= 0))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031113440_AddInventoryAuditScanDetails'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031113440_AddInventoryAuditScanDetails', N'9.0.0');
END;

COMMIT;
GO

