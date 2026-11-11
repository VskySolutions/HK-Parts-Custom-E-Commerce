/* =====================================================================================
 Customers: Documents tab (admin-kept customer documents: licenses / IDs)
 Generated from EF migration AddCustomerDocuments:
   dotnet ef migrations script AddCustomerCustomInformation AddCustomerDocuments --idempotent

 Run AFTER 8_2026-11-06_AddCustomerCustomInformation.sql. Additive only: creates CustomerDocuments
 (CustomerId -> Customers, no cascade; customers are soft-deleted). Files themselves are NOT in the
 database: they live in private storage (storage.private.root, default <API content root>/App_Data/private,
 or the private Azure container when storage.provider = AzureBlobStorage).
 Idempotent: guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106113540_AddCustomerDocuments'
)
BEGIN
    CREATE TABLE [CustomerDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [DocumentType] int NOT NULL,
        [OriginalFileName] nvarchar(255) NOT NULL,
        [StorageKey] nvarchar(500) NOT NULL,
        [MimeType] nvarchar(100) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [Note] nvarchar(2000) NULL,
        [Deleted] bit NOT NULL,
        [DeletedOnUtc] datetime2 NULL,
        [DeletedById] uniqueidentifier NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomerDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerDocuments_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106113540_AddCustomerDocuments'
)
BEGIN
    CREATE INDEX [IX_CustomerDocuments_CustomerId_Deleted_CreatedOnUtc] ON [CustomerDocuments] ([CustomerId], [Deleted], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106113540_AddCustomerDocuments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106113540_AddCustomerDocuments', N'9.0.0');
END;

COMMIT;
GO

