/* =====================================================================================
 Barcode inventory scanner audit log (port of the PHP inventory_audit_log table)
 Generated from EF migration AddInventoryAuditLog:
   dotnet ef migrations script AddProductAttachments AddInventoryAuditLog --idempotent

 Run AFTER 2026-10-31_AddProductAttachments.sql. Additive only: creates InventoryAuditLogs (one row
 per scanner stock/price change; FKs to Products / ProductVariants / Stores are no-action). Idempotent:
 guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit — change the entities/configurations, add a migration, and regenerate.
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031102250_AddInventoryAuditLog'
)
BEGIN
    CREATE TABLE [InventoryAuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductVariantId] uniqueidentifier NULL,
        [StoreId] uniqueidentifier NULL,
        [OldQty] int NULL,
        [NewQty] int NULL,
        [OldPrice] decimal(18,2) NULL,
        [NewPrice] decimal(18,2) NULL,
        [Action] int NOT NULL,
        [ScannedCode] nvarchar(400) NULL,
        [Notes] nvarchar(500) NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_InventoryAuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryAuditLogs_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]),
        CONSTRAINT [FK_InventoryAuditLogs_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]),
        CONSTRAINT [FK_InventoryAuditLogs_Stores_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [Stores] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031102250_AddInventoryAuditLog'
)
BEGIN
    CREATE INDEX [IX_InventoryAuditLogs_CreatedOnUtc] ON [InventoryAuditLogs] ([CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031102250_AddInventoryAuditLog'
)
BEGIN
    CREATE INDEX [IX_InventoryAuditLogs_ProductId_CreatedOnUtc] ON [InventoryAuditLogs] ([ProductId], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031102250_AddInventoryAuditLog'
)
BEGIN
    CREATE INDEX [IX_InventoryAuditLogs_ProductVariantId] ON [InventoryAuditLogs] ([ProductVariantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031102250_AddInventoryAuditLog'
)
BEGIN
    CREATE INDEX [IX_InventoryAuditLogs_StoreId] ON [InventoryAuditLogs] ([StoreId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031102250_AddInventoryAuditLog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031102250_AddInventoryAuditLog', N'9.0.0');
END;

COMMIT;
GO

