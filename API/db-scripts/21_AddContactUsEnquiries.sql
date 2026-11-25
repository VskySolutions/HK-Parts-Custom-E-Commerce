IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [ContactUsEnquiries] (
    [Id] uniqueidentifier NOT NULL,
    [FirstName] nvarchar(200) NOT NULL,
    [LastName] nvarchar(200) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [PhoneNumber] nvarchar(50) NULL,
    [Comments] nvarchar(max) NULL,
    [Deleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [DeletedOnUtc] datetime2(7) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2(7) NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2(7) NOT NULL,
    CONSTRAINT [PK_ContactUsEnquiries] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261106113915_AddContactUsEnquiries', N'9.0.0');

COMMIT;
GO

