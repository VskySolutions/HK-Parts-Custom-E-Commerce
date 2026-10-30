/* =====================================================================================
 Product + Category — PHP (Laravel / Edreamz ecommcore) parity
 Generated from EF migration 20261030103310_AddProductCategoryPhpParity:
   dotnet ef migrations script AddLineItemCustomAttributes AddProductCategoryPhpParity --idempotent

 Idempotent: guarded by __EFMigrationsHistory, so it is safe to run more than once and safe to run
 on a DB where the app already applied the migration at startup (DatabaseInitializer.MigrateAsync).
 Do not hand-edit — change the entities/configurations, add a migration, and regenerate.

 Products         : Gtin, Cost, Msrp, SalePrice + SaleStart/EndDateUtc, TaxCode, IsPurchasable,
                    IsFflRequired, Min/MaxOrderQuantity, StockStatusOverride, BackorderMessage,
                    Weight (lb), Length/Width/Height (in), PackagePieces, PackageType, FreightClass,
                    IsHazardous, H1, MetaImageMediaId, MetaOpenGraph, MetaCustomTags, MetaFooterTags
 ProductVariants  : Gtin, Cost, Msrp, Weight
 Categories       : Summary, ShowInMenu, ImageMediaId/ImageAltText, Image2MediaId/Image2AltText,
                    H1, MetaImageMediaId, MetaOpenGraph, MetaCustomTags, MetaFooterTags, ShowBrandFilter
 New tables       : ProductStoreMappings, ProductCustomFields, ProductCustomFieldValues,
                    CategoryFilterAttributes
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [ProductVariants] ADD [Cost] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [ProductVariants] ADD [Gtin] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [ProductVariants] ADD [Msrp] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [ProductVariants] ADD [Weight] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [BackorderMessage] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [Cost] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [FreightClass] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [Gtin] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [H1] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [Height] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [IsFflRequired] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [IsHazardous] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [IsPurchasable] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [Length] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [MaxOrderQuantity] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [MetaCustomTags] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [MetaFooterTags] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [MetaImageMediaId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [MetaOpenGraph] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [MinOrderQuantity] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [Msrp] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [PackagePieces] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [PackageType] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [SaleEndDateUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [SalePrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [SaleStartDateUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [StockStatusOverride] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [TaxCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [Weight] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD [Width] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [H1] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [Image2AltText] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [Image2MediaId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [ImageAltText] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [ImageMediaId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [MetaCustomTags] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [MetaFooterTags] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [MetaImageMediaId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [MetaOpenGraph] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [ShowBrandFilter] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [ShowInMenu] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD [Summary] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE TABLE [CategoryFilterAttributes] (
        [Id] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [SpecificationAttributeId] uniqueidentifier NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_CategoryFilterAttributes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CategoryFilterAttributes_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CategoryFilterAttributes_SpecificationAttributes_SpecificationAttributeId] FOREIGN KEY ([SpecificationAttributeId]) REFERENCES [SpecificationAttributes] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE TABLE [ProductCustomFields] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [FieldType] int NOT NULL,
        [IsRequired] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [Deleted] bit NOT NULL,
        [DeletedOnUtc] datetime2 NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ProductCustomFields] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE TABLE [ProductStoreMappings] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [StoreId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ProductStoreMappings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductStoreMappings_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductStoreMappings_Stores_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [Stores] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE TABLE [ProductCustomFieldValues] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [ProductCustomFieldId] uniqueidentifier NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductCustomFieldValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductCustomFieldValues_ProductCustomFields_ProductCustomFieldId] FOREIGN KEY ([ProductCustomFieldId]) REFERENCES [ProductCustomFields] ([Id]),
        CONSTRAINT [FK_ProductCustomFieldValues_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ProductVariants_Gtin] ON [ProductVariants] ([Gtin]) WHERE [Gtin] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Products_Gtin] ON [Products] ([Gtin]) WHERE [Gtin] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE INDEX [IX_Products_MetaImageMediaId] ON [Products] ([MetaImageMediaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Products_Sku] ON [Products] ([Sku]) WHERE [Sku] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    EXEC(N'ALTER TABLE [Products] ADD CONSTRAINT [CK_Products_OrderQuantityRange] CHECK (([MinOrderQuantity] IS NULL OR [MinOrderQuantity] >= 1) AND ([MaxOrderQuantity] IS NULL OR [MaxOrderQuantity] >= 1) AND ([MinOrderQuantity] IS NULL OR [MaxOrderQuantity] IS NULL OR [MaxOrderQuantity] >= [MinOrderQuantity]))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    EXEC(N'ALTER TABLE [Products] ADD CONSTRAINT [CK_Products_PackagePieces] CHECK ([PackagePieces] >= 1)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    EXEC(N'ALTER TABLE [Products] ADD CONSTRAINT [CK_Products_SaleWindow] CHECK ([SaleStartDateUtc] IS NULL OR [SaleEndDateUtc] IS NULL OR [SaleEndDateUtc] >= [SaleStartDateUtc])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE INDEX [IX_Categories_Image2MediaId] ON [Categories] ([Image2MediaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE INDEX [IX_Categories_ImageMediaId] ON [Categories] ([ImageMediaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE INDEX [IX_Categories_MetaImageMediaId] ON [Categories] ([MetaImageMediaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CategoryFilterAttributes_CategoryId_SpecificationAttributeId] ON [CategoryFilterAttributes] ([CategoryId], [SpecificationAttributeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE INDEX [IX_CategoryFilterAttributes_SpecificationAttributeId] ON [CategoryFilterAttributes] ([SpecificationAttributeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ProductCustomFields_Code] ON [ProductCustomFields] ([Code]) WHERE [Deleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE INDEX [IX_ProductCustomFieldValues_ProductCustomFieldId] ON [ProductCustomFieldValues] ([ProductCustomFieldId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductCustomFieldValues_ProductId_ProductCustomFieldId] ON [ProductCustomFieldValues] ([ProductId], [ProductCustomFieldId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductStoreMappings_ProductId_StoreId] ON [ProductStoreMappings] ([ProductId], [StoreId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    CREATE INDEX [IX_ProductStoreMappings_StoreId] ON [ProductStoreMappings] ([StoreId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Media_Image2MediaId] FOREIGN KEY ([Image2MediaId]) REFERENCES [Media] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Media_ImageMediaId] FOREIGN KEY ([ImageMediaId]) REFERENCES [Media] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Media_MetaImageMediaId] FOREIGN KEY ([MetaImageMediaId]) REFERENCES [Media] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_Media_MetaImageMediaId] FOREIGN KEY ([MetaImageMediaId]) REFERENCES [Media] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030103310_AddProductCategoryPhpParity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030103310_AddProductCategoryPhpParity', N'9.0.0');
END;

COMMIT;
GO

