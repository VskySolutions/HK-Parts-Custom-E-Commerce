/* =====================================================================================
 ShipStation Custom Store + checkout address verification
 (docs/shipstation-verification-requirements.md).
 Generated from EF migration AddShipStationAndAddressVerification:
   dotnet ef migrations script AddCustomerBlacklistEvents AddShipStationAndAddressVerification --idempotent

 Run AFTER the scripts for migrations LinkBlacklistEntryToCustomer and AddCustomerBlacklistEvents. Additive:
   - ShipStationSettings (singleton; Password encrypted by the app), ShipStationLogs (call log, no PII).
   - AddressVerificationSettings (singleton), AddressVerificationResults (30-day cache by address hash).
   - Credentials_Avalara (AvaTax v2 account for address verification; LicenseKey encrypted by the app).
   - Carts.ShipToAddressHash / ShipToVerifyStatus / ShipToAddressOverridden.
   - OrderShippings.AddressVerifyStatus / AddressOverridden.
   - Shipments.Source / ShippingCost (ShipStation ship notices).
   No data back-fill: ShipStation and verification start switched off until saved in the admin.
 Idempotent: guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    ALTER TABLE [Shipments] ADD [ShippingCost] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    ALTER TABLE [Shipments] ADD [Source] nvarchar(32) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    ALTER TABLE [OrderShippings] ADD [AddressOverridden] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    ALTER TABLE [OrderShippings] ADD [AddressVerifyStatus] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    ALTER TABLE [Carts] ADD [ShipToAddressHash] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    ALTER TABLE [Carts] ADD [ShipToAddressOverridden] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    ALTER TABLE [Carts] ADD [ShipToVerifyStatus] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    CREATE TABLE [AddressVerificationResults] (
        [Id] uniqueidentifier NOT NULL,
        [AddressHash] nvarchar(64) NOT NULL,
        [Provider] int NOT NULL,
        [Status] int NOT NULL,
        [IsPoBox] bit NOT NULL,
        [IsResidential] bit NULL,
        [SuggestedJson] nvarchar(2000) NULL,
        [Messages] nvarchar(2000) NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AddressVerificationResults] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    CREATE TABLE [AddressVerificationSettings] (
        [Id] uniqueidentifier NOT NULL,
        [Provider] int NOT NULL,
        [FailedMessage] nvarchar(255) NULL,
        [Countries] nvarchar(1000) NULL,
        [OfferSuggestion] bit NOT NULL,
        [OnUnavailable] int NOT NULL,
        [LoggingEnabled] bit NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AddressVerificationSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    CREATE TABLE [Credentials_Avalara] (
        [Id] uniqueidentifier NOT NULL,
        [BaseUrl] nvarchar(500) NULL,
        [AccountId] nvarchar(100) NULL,
        [LicenseKey] nvarchar(max) NULL,
        [CompanyCode] nvarchar(100) NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        [Active] bit NOT NULL,
        [IsProduction] bit NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [TransactionFeePercent] decimal(5,2) NULL,
        [Deleted] bit NOT NULL,
        [DeletedOnUtc] datetime2 NULL,
        CONSTRAINT [PK_Credentials_Avalara] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    CREATE TABLE [ShipStationLogs] (
        [Id] uniqueidentifier NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [Page] int NULL,
        [OrderCount] int NULL,
        [OrderNumber] nvarchar(64) NULL,
        [HttpStatus] int NOT NULL,
        [DurationMs] bigint NOT NULL,
        [Error] nvarchar(1000) NULL,
        [ClientIp] nvarchar(64) NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ShipStationLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    CREATE TABLE [ShipStationSettings] (
        [Id] uniqueidentifier NOT NULL,
        [Enabled] bit NOT NULL,
        [Username] nvarchar(200) NULL,
        [Password] nvarchar(max) NULL,
        [IncludeEbay] bit NOT NULL,
        [SplitFfl] bit NOT NULL,
        [NotifyCustomer] bit NOT NULL,
        [ExportStatuses] nvarchar(200) NOT NULL,
        [TimeZone] nvarchar(100) NOT NULL,
        [AllowQueryCredentials] bit NOT NULL,
        [DebugUntilUtc] datetime2 NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ShipStationSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    CREATE INDEX [IX_AddressVerificationResults_AddressHash_Provider_CreatedOnUtc] ON [AddressVerificationResults] ([AddressHash], [Provider], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    CREATE INDEX [IX_AddressVerificationResults_CreatedOnUtc] ON [AddressVerificationResults] ([CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    CREATE INDEX [IX_ShipStationLogs_Action_CreatedOnUtc] ON [ShipStationLogs] ([Action], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    CREATE INDEX [IX_ShipStationLogs_CreatedOnUtc] ON [ShipStationLogs] ([CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118095859_AddShipStationAndAddressVerification'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261118095859_AddShipStationAndAddressVerification', N'9.0.0');
END;

COMMIT;
GO

