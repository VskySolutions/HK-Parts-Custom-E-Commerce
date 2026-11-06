/* =====================================================================================
 eBay channel: accounts, listings (+ item specifics), category tree, API log, vehicle fitment
 Generated from EF migration AddEbayChannel:
   dotnet ef migrations script AddPriceRules AddEbayChannel --idempotent

 Run AFTER 8_2026-11-04_AddPriceRules.sql. Additive only:
   - new tables EbayAccounts (one row per store; AccessToken/RefreshToken hold Data-Protection
     ciphertext, never plain text), EbayListings (rowversion; filtered unique index
     UX_EbayListings_Product_NotEnded = one non-ended listing per product), EbayListingAspects,
     EbayCategories, EbayApiLogs, VehicleFitments
   - Products.ChannelEbay (bit, default 0)
   - Orders.EbayOrderId (unique where not null) + Orders.EbayBuyerMessage
   - OrderLineItems.EbayLineItemId, Shipments.EbayFulfillmentId
 Idempotent: guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.

 eBay app credentials are NOT stored in the database: set Ebay:{Sandbox|Production}:{ClientId|
 ClientSecret|RuName} in User Secrets / environment variables / Key Vault.
===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    ALTER TABLE [Shipments] ADD [EbayFulfillmentId] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    ALTER TABLE [Products] ADD [ChannelEbay] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    ALTER TABLE [Orders] ADD [EbayBuyerMessage] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    ALTER TABLE [Orders] ADD [EbayOrderId] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    ALTER TABLE [OrderLineItems] ADD [EbayLineItemId] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE TABLE [EbayAccounts] (
        [StoreId] uniqueidentifier NOT NULL,
        [IsEnabled] bit NOT NULL,
        [Environment] tinyint NOT NULL,
        [MarketplaceId] nvarchar(32) NOT NULL DEFAULT N'EBAY_US',
        [CategoryTreeId] nvarchar(16) NULL,
        [AccessToken] nvarchar(max) NULL,
        [AccessTokenExpiresAtUtc] datetime2 NULL,
        [RefreshToken] nvarchar(max) NULL,
        [RefreshTokenExpiresAtUtc] datetime2 NULL,
        [ConnectedAtUtc] datetime2 NULL,
        [ConnectedEbayUser] nvarchar(128) NULL,
        [LoggingEnabled] bit NOT NULL,
        [DefaultCategoryId] nvarchar(20) NULL,
        [DefaultPaymentPolicyId] nvarchar(40) NULL,
        [DefaultFulfillmentPolicyId] nvarchar(40) NULL,
        [DefaultReturnPolicyId] nvarchar(40) NULL,
        [MerchantLocationKey] nvarchar(36) NULL,
        [FitmentEnabled] bit NOT NULL,
        [SyncPriceFromStore] bit NOT NULL,
        [OrdersLastSyncedAtUtc] datetime2 NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_EbayAccounts] PRIMARY KEY ([StoreId]),
        CONSTRAINT [FK_EbayAccounts_Stores_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [Stores] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE TABLE [EbayApiLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [StoreId] uniqueidentifier NOT NULL,
        [Operation] nvarchar(100) NOT NULL,
        [HttpMethod] nvarchar(10) NOT NULL,
        [Url] nvarchar(2000) NOT NULL,
        [RequestBody] nvarchar(max) NULL,
        [ResponseStatus] int NULL,
        [ResponseBody] nvarchar(max) NULL,
        [Success] bit NOT NULL,
        [DurationMs] int NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_EbayApiLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE TABLE [EbayCategories] (
        [CategoryId] nvarchar(20) NOT NULL,
        [ParentCategoryId] nvarchar(20) NULL,
        [Name] nvarchar(200) NOT NULL,
        [Level] int NOT NULL,
        [IsLeaf] bit NOT NULL,
        [TreeVersion] nvarchar(20) NULL,
        CONSTRAINT [PK_EbayCategories] PRIMARY KEY ([CategoryId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE TABLE [EbayListings] (
        [Id] uniqueidentifier NOT NULL,
        [StoreId] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Sku] nvarchar(50) NOT NULL,
        [CategoryId] nvarchar(20) NOT NULL,
        [Status] tinyint NOT NULL,
        [Title] nvarchar(80) NOT NULL,
        [Subtitle] nvarchar(55) NULL,
        [Description] nvarchar(max) NULL,
        [Price] decimal(18,2) NOT NULL,
        [Quantity] int NOT NULL,
        [QuantitySold] int NOT NULL DEFAULT 0,
        [ConditionId] nvarchar(20) NULL,
        [PaymentPolicyId] nvarchar(40) NULL,
        [FulfillmentPolicyId] nvarchar(40) NULL,
        [ReturnPolicyId] nvarchar(40) NULL,
        [OfferId] nvarchar(40) NULL,
        [EbayListingIdExternal] nvarchar(40) NULL,
        [LastError] nvarchar(max) NULL,
        [PublishedAtUtc] datetime2 NULL,
        [LastSyncedAtUtc] datetime2 NULL,
        [EndedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_EbayListings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EbayListings_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE TABLE [VehicleFitments] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [Year] int NOT NULL,
        [Make] nvarchar(60) NOT NULL,
        [Model] nvarchar(60) NOT NULL,
        [Trim] nvarchar(120) NULL,
        [Engine] nvarchar(120) NULL,
        CONSTRAINT [PK_VehicleFitments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VehicleFitments_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE TABLE [EbayListingAspects] (
        [Id] bigint NOT NULL IDENTITY,
        [EbayListingId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Value] nvarchar(500) NOT NULL,
        CONSTRAINT [PK_EbayListingAspects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EbayListingAspects_EbayListings_EbayListingId] FOREIGN KEY ([EbayListingId]) REFERENCES [EbayListings] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Orders_EbayOrderId] ON [Orders] ([EbayOrderId]) WHERE [EbayOrderId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE INDEX [IX_EbayApiLogs_StoreId_CreatedOnUtc] ON [EbayApiLogs] ([StoreId], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE INDEX [IX_EbayCategories_Name] ON [EbayCategories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE INDEX [IX_EbayCategories_ParentCategoryId] ON [EbayCategories] ([ParentCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE INDEX [IX_EbayListingAspects_EbayListingId] ON [EbayListingAspects] ([EbayListingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE INDEX [IX_EbayListings_EbayListingIdExternal] ON [EbayListings] ([EbayListingIdExternal]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE INDEX [IX_EbayListings_Sku] ON [EbayListings] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE INDEX [IX_EbayListings_StoreId_Status] ON [EbayListings] ([StoreId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_EbayListings_Product_NotEnded] ON [EbayListings] ([ProductId]) WHERE [Status] <> 2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    CREATE INDEX [IX_VehicleFitments_ProductId_Year] ON [VehicleFitments] ([ProductId], [Year]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104092850_AddEbayChannel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104092850_AddEbayChannel', N'9.0.0');
END;

COMMIT;
GO

