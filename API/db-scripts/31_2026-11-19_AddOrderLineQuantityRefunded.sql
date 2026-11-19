-- 31  2026-11-19  AddOrderLineQuantityRefunded
-- Apply credit (PHP parity): OrderLineItems.QuantityRefunded = units taken off a line by "Apply credit"
-- (legacy qty_refunded). Idempotent: safe to run more than once; the app applies the same migration at startup.

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119085828_AddOrderLineQuantityRefunded'
)
BEGIN
    ALTER TABLE [OrderLineItems] ADD [QuantityRefunded] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119085828_AddOrderLineQuantityRefunded'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261119085828_AddOrderLineQuantityRefunded', N'9.0.0');
END;

COMMIT;
GO

