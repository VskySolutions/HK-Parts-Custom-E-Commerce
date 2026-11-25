-- 34  2026-11-20  AddGuestCheckout
-- Guest checkout (docs/guest-checkout-requirements.md): Guests table (one row per normalized guest email),
-- Orders.GuestId, Products.AccountRequired, CustomerBlacklist.Source, GuestOrderAccessLogs (lookup rate limit),
-- and a back-fill of Guests + Orders.GuestId from existing guest orders. Idempotent: safe to run more than once;
-- the app applies the same migration at startup.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    ALTER TABLE [Products] ADD [AccountRequired] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    ALTER TABLE [Orders] ADD [GuestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    ALTER TABLE [CustomerBlacklist] ADD [Source] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    CREATE TABLE [GuestOrderAccessLogs] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NULL,
        [IpAddress] nvarchar(64) NULL,
        [Success] bit NOT NULL,
        [Method] nvarchar(20) NOT NULL,
        [AtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_GuestOrderAccessLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    CREATE TABLE [Guests] (
        [Id] uniqueidentifier NOT NULL,
        [EmailNormalized] nvarchar(256) NOT NULL,
        [FirstName] nvarchar(200) NULL,
        [LastName] nvarchar(200) NULL,
        [PhoneNumber] nvarchar(50) NULL,
        [MarketingOptIn] bit NOT NULL,
        [OptInOnUtc] datetime2 NULL,
        [LinkedCustomerId] uniqueidentifier NULL,
        [LinkedOnUtc] datetime2 NULL,
        [LastOrderOnUtc] datetime2 NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Guests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Guests_Customers_LinkedCustomerId] FOREIGN KEY ([LinkedCustomerId]) REFERENCES [Customers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    CREATE INDEX [IX_Orders_GuestId] ON [Orders] ([GuestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    CREATE INDEX [IX_GuestOrderAccessLogs_IpAddress_AtUtc] ON [GuestOrderAccessLogs] ([IpAddress], [AtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    CREATE INDEX [IX_GuestOrderAccessLogs_OrderId] ON [GuestOrderAccessLogs] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Guests_EmailNormalized] ON [Guests] ([EmailNormalized]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    CREATE INDEX [IX_Guests_LastOrderOnUtc] ON [Guests] ([LastOrderOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    CREATE INDEX [IX_Guests_LinkedCustomerId] ON [Guests] ([LinkedCustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    ALTER TABLE [Orders] ADD CONSTRAINT [FK_Orders_Guests_GuestId] FOREIGN KEY ([GuestId]) REFERENCES [Guests] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    EXEC(N'
    ;WITH guestOrders AS (
        SELECT o.Id, o.PlacedOnUtc, LOWER(LTRIM(RTRIM(a.Email))) AS Email, a.FirstName, a.LastName, a.PhoneNumber,
               ROW_NUMBER() OVER (PARTITION BY LOWER(LTRIM(RTRIM(a.Email))) ORDER BY o.PlacedOnUtc DESC) AS rn
        FROM Orders o
        JOIN Addresses a ON a.Id = o.ShippingAddressId
        WHERE o.CustomerId IS NULL AND o.EbayOrderId IS NULL AND o.IsInquiry = 0
          AND a.Email IS NOT NULL AND LTRIM(RTRIM(a.Email)) <> N''''
    )
    INSERT INTO Guests (Id, EmailNormalized, FirstName, LastName, PhoneNumber, MarketingOptIn, LastOrderOnUtc, CreatedOnUtc, UpdatedOnUtc)
    SELECT NEWID(), g.Email, LEFT(g.FirstName, 200), LEFT(g.LastName, 200), LEFT(g.PhoneNumber, 50), 0, g.PlacedOnUtc, SYSUTCDATETIME(), SYSUTCDATETIME()
    FROM guestOrders g
    WHERE g.rn = 1 AND LEN(g.Email) <= 256
      AND NOT EXISTS (SELECT 1 FROM Guests x WHERE x.EmailNormalized = g.Email);

    UPDATE o SET o.GuestId = g.Id
    FROM Orders o
    JOIN Addresses a ON a.Id = o.ShippingAddressId
    JOIN Guests g ON g.EmailNormalized = LOWER(LTRIM(RTRIM(a.Email)))
    WHERE o.CustomerId IS NULL AND o.EbayOrderId IS NULL AND o.IsInquiry = 0 AND o.GuestId IS NULL;

    UPDATE a SET a.Email = LOWER(LTRIM(RTRIM(a.Email)))
    FROM Addresses a
    JOIN Orders o ON o.ShippingAddressId = a.Id
    WHERE o.GuestId IS NOT NULL AND a.Email <> LOWER(LTRIM(RTRIM(a.Email))) COLLATE Latin1_General_BIN2;
    ')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261120072614_AddGuestCheckout'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261120072614_AddGuestCheckout', N'9.0.0');
END;

COMMIT;
GO

