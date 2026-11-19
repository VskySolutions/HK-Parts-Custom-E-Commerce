/* =====================================================================================
 Checkout billing address: the bill-to the buyer confirms on the checkout page is stored on the order.
 Generated from EF migration AddOrderBillingAddress:
   dotnet ef migrations script AddFflDealerIsManual AddOrderBillingAddress --idempotent

 Run AFTER 26_2026-11-17_AddFflDealerIsManual.sql. Additive:
   - Orders.BillingAddressId (uniqueidentifier NULL, FK -> Addresses, no cascade) + index.
     Existing orders keep NULL; the invoice then falls back to the customer's default billing
     address, else the shipping address (unchanged behaviour).
 Idempotent: guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117121823_AddOrderBillingAddress'
)
BEGIN
    ALTER TABLE [Orders] ADD [BillingAddressId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117121823_AddOrderBillingAddress'
)
BEGIN
    CREATE INDEX [IX_Orders_BillingAddressId] ON [Orders] ([BillingAddressId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117121823_AddOrderBillingAddress'
)
BEGIN
    ALTER TABLE [Orders] ADD CONSTRAINT [FK_Orders_Addresses_BillingAddressId] FOREIGN KEY ([BillingAddressId]) REFERENCES [Addresses] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117121823_AddOrderBillingAddress'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261117121823_AddOrderBillingAddress', N'9.0.0');
END;

COMMIT;
GO

