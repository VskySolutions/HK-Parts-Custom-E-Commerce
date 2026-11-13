/* =====================================================================================
 Customers: Blacklist (Customers list → Actions → Blacklist)
 Generated from EF migration AddBlacklistedCustomers:
   dotnet ef migrations script AddCustomerDocuments AddBlacklistedCustomers --idempotent

 Run AFTER 9_2026-11-06_AddCustomerDocuments.sql. Additive only: creates BlacklistedCustomers
 (CustomerId -> Customers, no cascade; customers are soft-deleted). The Customers table and all
 customer data are untouched; an active (Deleted = 0) row here hides the customer from the admin list.
   - IX_BlacklistedCustomers_CustomerId           lookup / NOT EXISTS on CustomerId
   - UX_BlacklistedCustomers_CustomerId_Active    filtered unique index: one active block per customer
 Idempotent: guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112084749_AddBlacklistedCustomers'
)
BEGIN
    CREATE TABLE [BlacklistedCustomers] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [InternalNotes] nvarchar(2000) NULL,
        [Deleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedOnUtc] datetime2 NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_BlacklistedCustomers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BlacklistedCustomers_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112084749_AddBlacklistedCustomers'
)
BEGIN
    CREATE INDEX [IX_BlacklistedCustomers_CustomerId] ON [BlacklistedCustomers] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112084749_AddBlacklistedCustomers'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_BlacklistedCustomers_CustomerId_Active] ON [BlacklistedCustomers] ([CustomerId]) WHERE [Deleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112084749_AddBlacklistedCustomers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112084749_AddBlacklistedCustomers', N'9.0.0');
END;

COMMIT;
GO

