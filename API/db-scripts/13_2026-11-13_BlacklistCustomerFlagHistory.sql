/* =====================================================================================
 Customers: blacklist current state moved to Customers.IsBlacklisted
 Generated from EF migration BlacklistCustomerFlagHistory:
   dotnet ef migrations script AddBlacklistedCustomers BlacklistCustomerFlagHistory --idempotent

 Run AFTER 12_2026-11-12_AddBlacklistedCustomers.sql.
   - Adds Customers.IsBlacklisted (bit, default 0) + IX_Customers_IsBlacklisted.
   - Backfills IsBlacklisted = 1 for customers with an active (Deleted = 0) block.
   - Converts BlacklistedCustomers into append-only history: adds Action (1 Blocked / 2 Unblocked),
     makes Reason nullable, drops Deleted/DeletedOnUtc/UpdatedById/UpdatedOnUtc and the filtered unique index,
     adds IX_BlacklistedCustomers_CustomerId_CreatedOnUtc.
   - Existing rows become Action = 1; each soft-deleted row with DeletedOnUtc gets a matching Action = 2 row.
 Idempotent: guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.
 ===================================================================================== */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    ALTER TABLE [Customers] ADD [IsBlacklisted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    CREATE INDEX [IX_Customers_IsBlacklisted] ON [Customers] ([IsBlacklisted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    ALTER TABLE [BlacklistedCustomers] ADD [Action] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    UPDATE [BlacklistedCustomers] SET [Action] = 1 WHERE [Action] IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BlacklistedCustomers]') AND [c].[name] = N'Action');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [BlacklistedCustomers] ALTER COLUMN [Action] int NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BlacklistedCustomers]') AND [c].[name] = N'Reason');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [BlacklistedCustomers] ALTER COLUMN [Reason] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    -- Dynamic SQL: on a re-run the old Deleted column no longer exists, and SQL Server compiles
    -- statements inside a skipped IF block too.
    EXEC(N'
    UPDATE c
    SET c.[IsBlacklisted] = 1
    FROM [Customers] c
    WHERE EXISTS (
        SELECT 1
        FROM [BlacklistedCustomers] b
        WHERE b.[CustomerId] = c.[Id] AND b.[Deleted] = 0
    );');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    DROP INDEX [UX_BlacklistedCustomers_CustomerId_Active] ON [BlacklistedCustomers];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    EXEC(N'
    INSERT INTO [BlacklistedCustomers] ([Id], [CustomerId], [Action], [Reason], [InternalNotes], [CreatedById], [CreatedOnUtc], [Deleted], [UpdatedById], [UpdatedOnUtc])
    SELECT NEWID(), b.[CustomerId], 2, NULL, NULL, COALESCE(b.[UpdatedById], b.[CreatedById]), b.[DeletedOnUtc], 1,
           COALESCE(b.[UpdatedById], b.[CreatedById]), b.[DeletedOnUtc]
    FROM [BlacklistedCustomers] b
    WHERE b.[Deleted] = 1 AND b.[DeletedOnUtc] IS NOT NULL;');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN

    DECLARE @defaultConstraint sysname;
    SELECT @defaultConstraint = dc.[name]
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.[object_id] = dc.[parent_object_id] AND c.[column_id] = dc.[parent_column_id]
    WHERE dc.[parent_object_id] = OBJECT_ID(N'[BlacklistedCustomers]') AND c.[name] = N'Deleted';
    IF @defaultConstraint IS NOT NULL
        EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP CONSTRAINT [' + @defaultConstraint + N']');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BlacklistedCustomers]') AND [c].[name] = N'Deleted');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP CONSTRAINT [' + @var2 + '];');
    IF COL_LENGTH(N'BlacklistedCustomers', N'Deleted') IS NOT NULL
        EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP COLUMN [Deleted]');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BlacklistedCustomers]') AND [c].[name] = N'DeletedOnUtc');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP CONSTRAINT [' + @var3 + '];');
    IF COL_LENGTH(N'BlacklistedCustomers', N'DeletedOnUtc') IS NOT NULL
        EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP COLUMN [DeletedOnUtc]');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BlacklistedCustomers]') AND [c].[name] = N'UpdatedById');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP CONSTRAINT [' + @var4 + '];');
    IF COL_LENGTH(N'BlacklistedCustomers', N'UpdatedById') IS NOT NULL
        EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP COLUMN [UpdatedById]');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BlacklistedCustomers]') AND [c].[name] = N'UpdatedOnUtc');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP CONSTRAINT [' + @var5 + '];');
    IF COL_LENGTH(N'BlacklistedCustomers', N'UpdatedOnUtc') IS NOT NULL
        EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP COLUMN [UpdatedOnUtc]');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    CREATE INDEX [IX_BlacklistedCustomers_CustomerId_CreatedOnUtc] ON [BlacklistedCustomers] ([CustomerId], [CreatedOnUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113120000_BlacklistCustomerFlagHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113120000_BlacklistCustomerFlagHistory', N'9.0.0');
END;
GO

COMMIT;
GO

