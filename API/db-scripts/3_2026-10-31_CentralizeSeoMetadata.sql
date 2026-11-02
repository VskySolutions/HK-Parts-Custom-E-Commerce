/* =====================================================================================
 SEO — one central SeoMetadata table for every module
 Generated from EF migration 20261031053252_CentralizeSeoMetadata:
   dotnet ef migrations script AddProductCategoryPhpParity CentralizeSeoMetadata --idempotent

 Idempotent: guarded by __EFMigrationsHistory, so it is safe to run more than once and safe to run
 on a DB where the app already applied the migration at startup (DatabaseInitializer.MigrateAsync).
 Do not hand-edit — change the entities/configurations, add a migration, and regenerate.

 Order matters (data-preserving):
   1. CREATE TABLE SeoMetadata (+ unique index EntityType/EntityId, FK MetaImageMediaId -> Media)
   2. Copy existing SEO values into SeoMetadata (only rows with any SEO data)
        EntityType: 1 Product, 2 Category, 3 Manufacturer, 4 CmsPage, 5 BlogPost
   3. Drop the old columns:
        Products      : MetaTitle, MetaDescription, MetaKeywords, H1, MetaImageMediaId,
                        MetaOpenGraph, MetaCustomTags, MetaFooterTags
        Categories    : same as Products + CanonicalUrl
        Manufacturers : MetaTitle, MetaDescription, MetaKeywords
        CMSPages      : MetaTitle, MetaDescription, MetaKeywords, CanonicalUrl
        CMSBlogPosts  : MetaTitle, MetaDescription
 BACK UP THE DATABASE FIRST — step 3 is destructive.
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    CREATE TABLE [SeoMetadata] (
        [Id] uniqueidentifier NOT NULL,
        [EntityType] int NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [MetaTitle] nvarchar(300) NULL,
        [MetaDescription] nvarchar(500) NULL,
        [MetaKeywords] nvarchar(500) NULL,
        [CanonicalUrl] nvarchar(500) NULL,
        [H1] nvarchar(300) NULL,
        [MetaImageMediaId] uniqueidentifier NULL,
        [MetaOpenGraph] nvarchar(max) NULL,
        [MetaCustomTags] nvarchar(max) NULL,
        [MetaFooterTags] nvarchar(max) NULL,
        [NoIndex] bit NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_SeoMetadata] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SeoMetadata_Media_MetaImageMediaId] FOREIGN KEY ([MetaImageMediaId]) REFERENCES [Media] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SeoMetadata_EntityType_EntityId] ON [SeoMetadata] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    CREATE INDEX [IX_SeoMetadata_MetaImageMediaId] ON [SeoMetadata] ([MetaImageMediaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    EXEC(N'INSERT INTO [SeoMetadata] ([Id],[EntityType],[EntityId],[MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaImageMediaId],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags],[NoIndex],[CreatedOnUtc],[UpdatedOnUtc]) SELECT NEWID(), 1, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], NULL, [H1], [MetaImageMediaId], [MetaOpenGraph], [MetaCustomTags], [MetaFooterTags], 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [Products] WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords],[H1],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags]) IS NOT NULL OR [MetaImageMediaId] IS NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    EXEC(N'INSERT INTO [SeoMetadata] ([Id],[EntityType],[EntityId],[MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaImageMediaId],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags],[NoIndex],[CreatedOnUtc],[UpdatedOnUtc]) SELECT NEWID(), 2, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], [CanonicalUrl], [H1], [MetaImageMediaId], [MetaOpenGraph], [MetaCustomTags], [MetaFooterTags], 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [Categories] WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags]) IS NOT NULL OR [MetaImageMediaId] IS NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    EXEC(N'INSERT INTO [SeoMetadata] ([Id],[EntityType],[EntityId],[MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaImageMediaId],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags],[NoIndex],[CreatedOnUtc],[UpdatedOnUtc]) SELECT NEWID(), 3, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], NULL, NULL, NULL, NULL, NULL, NULL, 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [Manufacturers] WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords]) IS NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    EXEC(N'INSERT INTO [SeoMetadata] ([Id],[EntityType],[EntityId],[MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaImageMediaId],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags],[NoIndex],[CreatedOnUtc],[UpdatedOnUtc]) SELECT NEWID(), 4, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], [CanonicalUrl], NULL, NULL, NULL, NULL, NULL, 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [CMSPages] WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl]) IS NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    EXEC(N'INSERT INTO [SeoMetadata] ([Id],[EntityType],[EntityId],[MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaImageMediaId],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags],[NoIndex],[CreatedOnUtc],[UpdatedOnUtc]) SELECT NEWID(), 5, [Id], [MetaTitle], [MetaDescription], NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [CMSBlogPosts] WHERE COALESCE([MetaTitle],[MetaDescription]) IS NOT NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    ALTER TABLE [Categories] DROP CONSTRAINT [FK_Categories_Media_MetaImageMediaId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    ALTER TABLE [Products] DROP CONSTRAINT [FK_Products_Media_MetaImageMediaId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DROP INDEX [IX_Products_MetaImageMediaId] ON [Products];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DROP INDEX [IX_Categories_MetaImageMediaId] ON [Categories];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'H1');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Products] DROP COLUMN [H1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaCustomTags');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Products] DROP COLUMN [MetaCustomTags];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaDescription');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Products] DROP COLUMN [MetaDescription];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaFooterTags');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Products] DROP COLUMN [MetaFooterTags];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaImageMediaId');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Products] DROP COLUMN [MetaImageMediaId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaKeywords');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [Products] DROP COLUMN [MetaKeywords];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaOpenGraph');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [Products] DROP COLUMN [MetaOpenGraph];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaTitle');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [Products] DROP COLUMN [MetaTitle];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Manufacturers]') AND [c].[name] = N'MetaDescription');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Manufacturers] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [Manufacturers] DROP COLUMN [MetaDescription];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Manufacturers]') AND [c].[name] = N'MetaKeywords');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Manufacturers] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [Manufacturers] DROP COLUMN [MetaKeywords];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Manufacturers]') AND [c].[name] = N'MetaTitle');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Manufacturers] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [Manufacturers] DROP COLUMN [MetaTitle];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSPages]') AND [c].[name] = N'CanonicalUrl');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [CMSPages] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [CMSPages] DROP COLUMN [CanonicalUrl];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSPages]') AND [c].[name] = N'MetaDescription');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [CMSPages] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [CMSPages] DROP COLUMN [MetaDescription];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSPages]') AND [c].[name] = N'MetaKeywords');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [CMSPages] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [CMSPages] DROP COLUMN [MetaKeywords];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSPages]') AND [c].[name] = N'MetaTitle');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [CMSPages] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [CMSPages] DROP COLUMN [MetaTitle];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSBlogPosts]') AND [c].[name] = N'MetaDescription');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [CMSBlogPosts] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [CMSBlogPosts] DROP COLUMN [MetaDescription];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSBlogPosts]') AND [c].[name] = N'MetaTitle');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [CMSBlogPosts] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [CMSBlogPosts] DROP COLUMN [MetaTitle];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'CanonicalUrl');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [Categories] DROP COLUMN [CanonicalUrl];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'H1');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [Categories] DROP COLUMN [H1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaCustomTags');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [Categories] DROP COLUMN [MetaCustomTags];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaDescription');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [Categories] DROP COLUMN [MetaDescription];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaFooterTags');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [Categories] DROP COLUMN [MetaFooterTags];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaImageMediaId');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [Categories] DROP COLUMN [MetaImageMediaId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var23 sysname;
    SELECT @var23 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaKeywords');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var23 + '];');
    ALTER TABLE [Categories] DROP COLUMN [MetaKeywords];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var24 sysname;
    SELECT @var24 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaOpenGraph');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var24 + '];');
    ALTER TABLE [Categories] DROP COLUMN [MetaOpenGraph];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    DECLARE @var25 sysname;
    SELECT @var25 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaTitle');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var25 + '];');
    ALTER TABLE [Categories] DROP COLUMN [MetaTitle];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031053252_CentralizeSeoMetadata'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031053252_CentralizeSeoMetadata', N'9.0.0');
END;

COMMIT;
GO

