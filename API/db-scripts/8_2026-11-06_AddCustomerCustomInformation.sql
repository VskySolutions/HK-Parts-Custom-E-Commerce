/* =====================================================================================
 Customers: "Custom information" section (admin Add / Edit customer)
 Generated from EF migration AddCustomerCustomInformation:
   dotnet ef migrations script AddInventoryAuditScanDetails AddCustomerCustomInformation --idempotent

 Run AFTER 7_2026-10-31_AddInventoryAuditScanDetails.sql. Additive only: adds to Customers
   - LongText                          nvarchar(4000) NULL   ("Long" free-text field)
   - AllowRestrictedCapacityPurchases  bit NOT NULL DEFAULT 0 (No / Yes; existing rows = No)
 Existing customer data is untouched. Idempotent: guarded by __EFMigrationsHistory, safe to re-run
 and safe if the app already applied it at startup.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106101833_AddCustomerCustomInformation'
)
BEGIN
    ALTER TABLE [Customers] ADD [AllowRestrictedCapacityPurchases] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106101833_AddCustomerCustomInformation'
)
BEGIN
    ALTER TABLE [Customers] ADD [LongText] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106101833_AddCustomerCustomInformation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106101833_AddCustomerCustomInformation', N'9.0.0');
END;

COMMIT;
GO

