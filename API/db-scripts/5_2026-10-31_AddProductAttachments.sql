/* =====================================================================================
 Product attachments (PHP parity: product list "Attachments" column + Media-tab files)
 Generated from EF migration AddProductAttachments:
   dotnet ef migrations script CentralizeSeoMetadata AddProductAttachments --idempotent

 Run AFTER 2026-10-31_CentralizeSeoMetadata.sql. Additive only: creates ProductAttachments
 (ProductId -> Products cascade, MediaId -> Media no-action). Idempotent: guarded by
 __EFMigrationsHistory, so it is safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit — change the entities/configurations, add a migration, and regenerate.
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031092541_AddProductAttachments'
)
BEGIN
    CREATE TABLE [ProductAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [MediaId] uniqueidentifier NOT NULL,
        [Title] nvarchar(300) NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ProductAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductAttachments_Media_MediaId] FOREIGN KEY ([MediaId]) REFERENCES [Media] ([Id]),
        CONSTRAINT [FK_ProductAttachments_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031092541_AddProductAttachments'
)
BEGIN
    CREATE INDEX [IX_ProductAttachments_MediaId] ON [ProductAttachments] ([MediaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031092541_AddProductAttachments'
)
BEGIN
    CREATE INDEX [IX_ProductAttachments_ProductId] ON [ProductAttachments] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031092541_AddProductAttachments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031092541_AddProductAttachments', N'9.0.0');
END;

COMMIT;
GO

