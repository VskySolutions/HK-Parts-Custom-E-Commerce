-- 40  2026-11-23  AddPhpTaxMethods
-- PHP tax parity: tax methods None / By State / By ZIP / Avalara, "Tax Shipping Amount", Avalara settings,
-- TaxStateRates + TaxZipRates + TaxLogs tables, CustomerGroups.TaxMethod, Orders.TaxDocumentCode / TaxDocumentVoidedOnUtc.
-- Run after 39. Idempotent; the app applies the same migration at startup.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    ALTER TABLE [TaxProviderConfigurations] ADD [AvalaraCommitDocuments] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    ALTER TABLE [TaxProviderConfigurations] ADD [AvalaraLoggingEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    ALTER TABLE [TaxProviderConfigurations] ADD [AvalaraShippingTaxCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    ALTER TABLE [TaxProviderConfigurations] ADD [AvalaraTimeoutSeconds] int NOT NULL DEFAULT 10;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    ALTER TABLE [TaxProviderConfigurations] ADD [TaxShipping] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    ALTER TABLE [Orders] ADD [TaxDocumentCode] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    ALTER TABLE [Orders] ADD [TaxDocumentVoidedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    ALTER TABLE [CustomerGroups] ADD [TaxMethod] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    CREATE TABLE [TaxLogs] (
        [Id] uniqueidentifier NOT NULL,
        [Provider] nvarchar(30) NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [Success] bit NOT NULL,
        [OrderNumber] nvarchar(50) NULL,
        [Url] nvarchar(500) NULL,
        [HttpStatus] int NULL,
        [RequestJson] nvarchar(max) NULL,
        [ResponseJson] nvarchar(max) NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [DurationMs] int NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TaxLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    CREATE TABLE [TaxStateRates] (
        [Id] uniqueidentifier NOT NULL,
        [CountryCode] nvarchar(2) NOT NULL,
        [StateCode] nvarchar(3) NOT NULL,
        [StateName] nvarchar(100) NOT NULL,
        [RatePercent] decimal(9,4) NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TaxStateRates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    CREATE TABLE [TaxZipRates] (
        [Id] uniqueidentifier NOT NULL,
        [PostalCode] nvarchar(20) NOT NULL,
        [RatePercent] decimal(9,4) NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TaxZipRates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    CREATE INDEX [IX_TaxLogs_CreatedOnUtc] ON [TaxLogs] ([CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaxStateRates_CountryCode_StateCode] ON [TaxStateRates] ([CountryCode], [StateCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaxZipRates_PostalCode] ON [TaxZipRates] ([PostalCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    EXEC(N'UPDATE [TaxProviderConfigurations] SET [AvalaraShippingTaxCode] = N''FR020100'' WHERE [AvalaraShippingTaxCode] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123073222_AddPhpTaxMethods'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261123073222_AddPhpTaxMethods', N'9.0.0');
END;

COMMIT;
GO

