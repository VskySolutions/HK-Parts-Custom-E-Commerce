/* =====================================================================================
 Gift certificates: certificates, append-only balance ledger, cart/order/product/payment columns
 Generated from EF migration AddGiftCertificates:
   dotnet ef migrations script AddEbayChannel AddGiftCertificates --idempotent

 Run AFTER 9_2026-11-04_AddEbayChannel.sql. Additive only:
   - GiftCertificates (unique StoreId+Code; CurrentBalance >= 0 check; rowversion; one row per
     purchased unit via the filtered unique index on SourceOrderItemId+SourceUnitIndex)
   - GiftCertificateTransactions (the ledger; never updated or deleted)
   - Carts.GiftCertificateId; Orders.GiftCertificateId/Code/AppliedAmount;
     CartItems + OrderLineItems.Gc* purchase details; Products.GiftCertificateOnline (existing rows 1);
     PaymentRecords.IsMaster (existing rows 1); EmailQueue.CcEmail/ReplyToEmail/RelatedEntityId
 Idempotent: guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.
===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [Products] ADD [GiftCertificateOnline] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [IsMaster] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [Orders] ADD [GiftCertificateAppliedAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [Orders] ADD [GiftCertificateCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [Orders] ADD [GiftCertificateId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [OrderLineItems] ADD [GcMessage] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [OrderLineItems] ADD [GcRecipientEmail] nvarchar(75) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [OrderLineItems] ADD [GcRecipientName] nvarchar(75) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [OrderLineItems] ADD [GcSenderEmail] nvarchar(75) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [OrderLineItems] ADD [GcSenderName] nvarchar(75) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [EmailQueue] ADD [CcEmail] nvarchar(255) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [EmailQueue] ADD [RelatedEntityId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [EmailQueue] ADD [ReplyToEmail] nvarchar(255) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [Carts] ADD [GiftCertificateId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [CartItems] ADD [GcMessage] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [CartItems] ADD [GcRecipientEmail] nvarchar(75) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [CartItems] ADD [GcRecipientName] nvarchar(75) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [CartItems] ADD [GcSenderEmail] nvarchar(75) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    ALTER TABLE [CartItems] ADD [GcSenderName] nvarchar(75) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    CREATE TABLE [GiftCertificates] (
        [Id] uniqueidentifier NOT NULL,
        [StoreId] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [OriginalBalance] decimal(18,2) NOT NULL,
        [CurrentBalance] decimal(18,2) NOT NULL,
        [Status] tinyint NOT NULL,
        [Source] tinyint NOT NULL,
        [SourceOrderId] uniqueidentifier NULL,
        [SourceOrderItemId] uniqueidentifier NULL,
        [SourceUnitIndex] int NULL,
        [IsOnline] bit NOT NULL,
        [SenderName] nvarchar(75) NULL,
        [SenderEmail] nvarchar(75) NULL,
        [RecipientName] nvarchar(75) NULL,
        [RecipientEmail] nvarchar(75) NULL,
        [Message] nvarchar(1000) NULL,
        [EmailSentAtUtc] datetime2 NULL,
        [ExpiresAtUtc] datetime2 NULL,
        [Deleted] bit NOT NULL,
        [DeletedOnUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_GiftCertificates] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_GiftCertificates_CurrentBalance] CHECK ([CurrentBalance] >= 0),
        CONSTRAINT [CK_GiftCertificates_OriginalBalance] CHECK ([OriginalBalance] >= 0),
        CONSTRAINT [FK_GiftCertificates_Orders_SourceOrderId] FOREIGN KEY ([SourceOrderId]) REFERENCES [Orders] ([Id]),
        CONSTRAINT [FK_GiftCertificates_Stores_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [Stores] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    CREATE TABLE [GiftCertificateTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [GiftCertificateId] uniqueidentifier NOT NULL,
        [Action] tinyint NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [BalanceAfter] decimal(18,2) NOT NULL,
        [OrderId] uniqueidentifier NULL,
        [Comment] nvarchar(255) NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        CONSTRAINT [PK_GiftCertificateTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GiftCertificateTransactions_GiftCertificates_GiftCertificateId] FOREIGN KEY ([GiftCertificateId]) REFERENCES [GiftCertificates] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    CREATE INDEX [IX_Orders_GiftCertificateId] ON [Orders] ([GiftCertificateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    CREATE INDEX [IX_GiftCertificates_CreatedOnUtc] ON [GiftCertificates] ([CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    CREATE INDEX [IX_GiftCertificates_SourceOrderId] ON [GiftCertificates] ([SourceOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_GiftCertificates_SourceOrderItemId_SourceUnitIndex] ON [GiftCertificates] ([SourceOrderItemId], [SourceUnitIndex]) WHERE [SourceOrderItemId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GiftCertificates_StoreId_Code] ON [GiftCertificates] ([StoreId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    CREATE INDEX [IX_GiftCertificateTransactions_GiftCertificateId_CreatedOnUtc] ON [GiftCertificateTransactions] ([GiftCertificateId], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    CREATE INDEX [IX_GiftCertificateTransactions_OrderId] ON [GiftCertificateTransactions] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104103048_AddGiftCertificates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104103048_AddGiftCertificates', N'9.0.0');
END;

COMMIT;
GO

