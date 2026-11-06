/* =====================================================================================
 Price Rules: automatic catalog discounts by category x customer group
 Generated from EF migration AddPriceRules:
   dotnet ef migrations script AddInventoryAuditScanDetails AddPriceRules --idempotent

 Run AFTER 7_2026-10-31_AddInventoryAuditScanDetails.sql. Additive only: creates PriceRules
 (+ date/amount/percent check constraints, rowversion), PriceRuleCustomerGroups and
 PriceRuleCategories (composite PKs, cascade from the rule), and the pre-calculated flat table
 PriceRuleProductPrices (unique per rule x product/variant x group, cascade from the rule, and the
 covering lookup index IX_PriceRuleProductPrices_Lookup). CustomerGroupId 00000000-0000-0000-0000-
 000000000000 means "Retail" (guests and customers without a group). Idempotent: guarded by
 __EFMigrationsHistory, safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.

 The flat table starts empty; the app fills it (rule save, "Re-apply All Rules", nightly job).
===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104085622_AddPriceRules'
)
BEGIN
    CREATE TABLE [PriceRules] (
        [Id] uniqueidentifier NOT NULL,
        [StoreId] uniqueidentifier NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [Priority] int NOT NULL DEFAULT 0,
        [StopFurtherRules] bit NOT NULL DEFAULT CAST(0 AS bit),
        [StartDate] date NULL,
        [EndDate] date NULL,
        [DiscountType] tinyint NOT NULL,
        [DiscountAmount] decimal(18,4) NOT NULL,
        [LastAppliedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PriceRules] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PriceRules_DateRange] CHECK ([StartDate] IS NULL OR [EndDate] IS NULL OR [EndDate] >= [StartDate]),
        CONSTRAINT [CK_PriceRules_DiscountAmount] CHECK ([DiscountAmount] > 0),
        CONSTRAINT [CK_PriceRules_PercentMax] CHECK ([DiscountType] <> 1 OR [DiscountAmount] <= 100),
        CONSTRAINT [FK_PriceRules_Stores_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [Stores] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104085622_AddPriceRules'
)
BEGIN
    CREATE TABLE [PriceRuleCategories] (
        [PriceRuleId] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PriceRuleCategories] PRIMARY KEY ([PriceRuleId], [CategoryId]),
        CONSTRAINT [FK_PriceRuleCategories_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]),
        CONSTRAINT [FK_PriceRuleCategories_PriceRules_PriceRuleId] FOREIGN KEY ([PriceRuleId]) REFERENCES [PriceRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104085622_AddPriceRules'
)
BEGIN
    CREATE TABLE [PriceRuleCustomerGroups] (
        [PriceRuleId] uniqueidentifier NOT NULL,
        [CustomerGroupId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PriceRuleCustomerGroups] PRIMARY KEY ([PriceRuleId], [CustomerGroupId]),
        CONSTRAINT [FK_PriceRuleCustomerGroups_PriceRules_PriceRuleId] FOREIGN KEY ([PriceRuleId]) REFERENCES [PriceRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104085622_AddPriceRules'
)
BEGIN
    CREATE TABLE [PriceRuleProductPrices] (
        [Id] bigint NOT NULL IDENTITY,
        [PriceRuleId] uniqueidentifier NOT NULL,
        [StoreId] uniqueidentifier NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductVariantId] uniqueidentifier NULL,
        [CustomerGroupId] uniqueidentifier NOT NULL,
        [BasePrice] decimal(18,4) NOT NULL,
        [RulePrice] decimal(18,4) NOT NULL,
        [StartDate] date NULL,
        [EndDate] date NULL,
        [Priority] int NOT NULL,
        [StopFurtherRules] bit NOT NULL,
        CONSTRAINT [PK_PriceRuleProductPrices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PriceRuleProductPrices_PriceRules_PriceRuleId] FOREIGN KEY ([PriceRuleId]) REFERENCES [PriceRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104085622_AddPriceRules'
)
BEGIN
    CREATE INDEX [IX_PriceRuleCategories_CategoryId] ON [PriceRuleCategories] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104085622_AddPriceRules'
)
BEGIN
    CREATE INDEX [IX_PriceRuleProductPrices_Lookup] ON [PriceRuleProductPrices] ([ProductId], [CustomerGroupId], [StoreId]) INCLUDE ([ProductVariantId], [PriceRuleId], [RulePrice], [StartDate], [EndDate], [Priority], [StopFurtherRules]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104085622_AddPriceRules'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PriceRuleProductPrices_PriceRuleId_ProductId_ProductVariantId_CustomerGroupId] ON [PriceRuleProductPrices] ([PriceRuleId], [ProductId], [ProductVariantId], [CustomerGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104085622_AddPriceRules'
)
BEGIN
    CREATE INDEX [IX_PriceRules_Name] ON [PriceRules] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104085622_AddPriceRules'
)
BEGIN
    CREATE INDEX [IX_PriceRules_StoreId] ON [PriceRules] ([StoreId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104085622_AddPriceRules'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104085622_AddPriceRules', N'9.0.0');
END;

COMMIT;
GO

