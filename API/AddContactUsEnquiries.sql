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
CREATE TABLE [AdminAlerts] (
    [Id] uniqueidentifier NOT NULL,
    [AlertType] nvarchar(100) NOT NULL,
    [Severity] nvarchar(20) NOT NULL,
    [Title] nvarchar(300) NOT NULL,
    [Message] nvarchar(max) NULL,
    [Source] nvarchar(100) NULL,
    [RelatedEntityType] nvarchar(100) NULL,
    [RelatedEntityId] nvarchar(100) NULL,
    [IsResolved] bit NOT NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [ResolvedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_AdminAlerts] PRIMARY KEY ([Id])
);

CREATE TABLE [ApplicationLogs] (
    [Id] bigint NOT NULL IDENTITY,
    [Message] nvarchar(max) NULL,
    [MessageTemplate] nvarchar(max) NULL,
    [Level] nvarchar(128) NOT NULL,
    [TimeStamp] datetime2 NOT NULL,
    [Exception] nvarchar(max) NULL,
    [Properties] xml NULL,
    [CorrelationId] nvarchar(64) NULL,
    [Source] nvarchar(50) NULL,
    [Route] nvarchar(500) NULL,
    CONSTRAINT [PK_ApplicationLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [AuditTrails] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [ActorName] nvarchar(200) NULL,
    [Action] nvarchar(100) NOT NULL,
    [EntityType] nvarchar(100) NULL,
    [EntityId] nvarchar(100) NULL,
    [CorrelationId] nvarchar(64) NULL,
    [IpAddress] nvarchar(64) NULL,
    [MetadataJson] nvarchar(max) NULL,
    [TimestampUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditTrails] PRIMARY KEY ([Id])
);

CREATE TABLE [BackgroundTaskLogs] (
    [Id] uniqueidentifier NOT NULL,
    [TaskName] nvarchar(200) NOT NULL,
    [StartedOnUtc] datetime2 NOT NULL,
    [CompletedOnUtc] datetime2 NULL,
    [Status] nvarchar(50) NOT NULL,
    [ErrorMessage] nvarchar(max) NULL,
    CONSTRAINT [PK_BackgroundTaskLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [EmailQueue] (
    [Id] uniqueidentifier NOT NULL,
    [TemplateKey] nvarchar(100) NOT NULL,
    [RecipientEmail] nvarchar(255) NOT NULL,
    [RecipientName] nvarchar(200) NULL,
    [RenderedSubject] nvarchar(1024) NOT NULL,
    [RenderedBody] nvarchar(max) NOT NULL,
    [Category] int NOT NULL,
    [Status] int NOT NULL,
    [AttemptCount] int NOT NULL,
    [LastAttemptOnUtc] datetime2 NULL,
    [NextAttemptOnUtc] datetime2 NULL,
    [ErrorMessage] nvarchar(max) NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_EmailQueue] PRIMARY KEY ([Id])
);

CREATE TABLE [EmailTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [TemplateKey] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [SubjectLine] nvarchar(512) NOT NULL,
    [HtmlBody] nvarchar(max) NOT NULL,
    [PlainTextBody] nvarchar(max) NULL,
    [Category] int NOT NULL,
    [Enabled] bit NOT NULL,
    [IsCritical] bit NOT NULL,
    [Description] nvarchar(500) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_EmailTemplates] PRIMARY KEY ([Id])
);

CREATE TABLE [MarketingSuppressions] (
    [Id] uniqueidentifier NOT NULL,
    [RecipientEmail] nvarchar(255) NOT NULL,
    [SuppressedOnUtc] datetime2 NOT NULL,
    [Source] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_MarketingSuppressions] PRIMARY KEY ([Id])
);

CREATE TABLE [PlatformSettings] (
    [Id] uniqueidentifier NOT NULL,
    [Key] nvarchar(200) NOT NULL,
    [Value] nvarchar(max) NULL,
    [ValueType] nvarchar(50) NOT NULL,
    [Category] nvarchar(100) NULL,
    [Description] nvarchar(500) NULL,
    [IsSensitive] bit NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_PlatformSettings] PRIMARY KEY ([Id])
);

CREATE TABLE [Roles] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [NormalizedName] nvarchar(100) NOT NULL,
    [Description] nvarchar(300) NULL,
    [IsSystemRole] bit NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);

CREATE TABLE [SettingsChangeHistory] (
    [Id] uniqueidentifier NOT NULL,
    [SettingKey] nvarchar(200) NOT NULL,
    [PreviousValue] nvarchar(max) NULL,
    [NewValue] nvarchar(max) NULL,
    [ChangedOnUtc] datetime2 NOT NULL,
    [ActorId] uniqueidentifier NULL,
    [ActorName] nvarchar(200) NULL,
    CONSTRAINT [PK_SettingsChangeHistory] PRIMARY KEY ([Id])
);

CREATE TABLE [SmtpAccounts] (
    [Id] uniqueidentifier NOT NULL,
    [DisplayName] nvarchar(200) NOT NULL,
    [Host] nvarchar(255) NOT NULL,
    [Port] int NOT NULL,
    [Username] nvarchar(255) NULL,
    [EncryptedPassword] nvarchar(max) NULL,
    [FromName] nvarchar(200) NOT NULL,
    [FromEmail] nvarchar(255) NOT NULL,
    [EncryptionMode] int NOT NULL,
    [AuthMethod] int NOT NULL,
    [Category] int NULL,
    [Enabled] bit NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_SmtpAccounts] PRIMARY KEY ([Id])
);

CREATE TABLE [Stores] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [AddressLine1] nvarchar(255) NULL,
    [AddressLine2] nvarchar(255) NULL,
    [City] nvarchar(120) NULL,
    [StateProvince] nvarchar(120) NULL,
    [PostalCode] nvarchar(20) NULL,
    [CountryCode] nvarchar(2) NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [ContactEmail] nvarchar(255) NULL,
    [ContactPhone] nvarchar(50) NULL,
    [OperatingHoursJson] nvarchar(max) NULL,
    [TimeZone] nvarchar(64) NOT NULL,
    [CurrencyDisplay] nvarchar(3) NULL,
    [IsEnabled] bit NOT NULL,
    [MaintenanceMode] bit NOT NULL,
    [DeliveryZoneJson] nvarchar(max) NULL,
    [OrderCapacityLimit] int NULL,
    [GuestOrderingEnabled] bit NOT NULL DEFAULT CAST(1 AS bit),
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Stores] PRIMARY KEY ([Id])
);

CREATE TABLE [SupportedCurrencies] (
    [Id] uniqueidentifier NOT NULL,
    [CurrencyCode] nvarchar(3) NOT NULL,
    [Symbol] nvarchar(8) NOT NULL,
    [ExchangeRate] decimal(18,6) NOT NULL,
    [IsEnabled] bit NOT NULL,
    [IsBaseCurrency] bit NOT NULL,
    [IsRateLocked] bit NOT NULL,
    [LastRateUpdatedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_SupportedCurrencies] PRIMARY KEY ([Id])
);

CREATE TABLE [TenantBrandings] (
    [Id] uniqueidentifier NOT NULL,
    [BrandName] nvarchar(200) NOT NULL,
    [Domain] nvarchar(255) NULL,
    [LogoUrl] nvarchar(500) NULL,
    [FaviconUrl] nvarchar(500) NULL,
    [PrimaryColor] nvarchar(32) NULL,
    [SecondaryColor] nvarchar(32) NULL,
    [AccentColor] nvarchar(32) NULL,
    [FontFamily] nvarchar(200) NULL,
    [SupportEmail] nvarchar(255) NULL,
    [SupportPhone] nvarchar(50) NULL,
    [SocialLinksJson] nvarchar(max) NULL,
    [LayoutOptionsJson] nvarchar(max) NULL,
    [DefaultLanguage] nvarchar(10) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_TenantBrandings] PRIMARY KEY ([Id])
);

CREATE TABLE [TenantCredentials] (
    [Id] uniqueidentifier NOT NULL,
    [ServiceType] nvarchar(100) NOT NULL,
    [EncryptedValue] nvarchar(max) NOT NULL,
    [LastFourChars] nvarchar(8) NOT NULL,
    [Description] nvarchar(500) NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_TenantCredentials] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [Username] nvarchar(256) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [PasswordHash] nvarchar(512) NOT NULL,
    [EmailVerified] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [LastLoginOnUtc] datetime2 NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE TABLE [DeliveryZones] (
    [Id] uniqueidentifier NOT NULL,
    [StoreId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [CountryCode] nvarchar(2) NOT NULL,
    [Region] nvarchar(120) NULL,
    [PostalCodeStart] nvarchar(20) NULL,
    [PostalCodeEnd] nvarchar(20) NULL,
    [IsActive] bit NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_DeliveryZones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DeliveryZones_Stores_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [Stores] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Customers] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [FirstName] nvarchar(200) NOT NULL,
    [LastName] nvarchar(200) NOT NULL,
    [PhoneNumber] nvarchar(50) NULL,
    [IsTaxExempt] bit NOT NULL,
    [TaxExemptionCertificate] nvarchar(200) NULL,
    [VatId] nvarchar(64) NULL,
    [WhatsAppPhoneNumber] nvarchar(50) NULL,
    [WhatsAppOptIn] bit NOT NULL,
    [WhatsAppOptInAtUtc] datetime2 NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Customers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RefreshTokens] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TokenHash] nvarchar(256) NOT NULL,
    [ExpiresOnUtc] datetime2 NOT NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [RevokedOnUtc] datetime2 NULL,
    [ReplacedByTokenId] uniqueidentifier NULL,
    [CreatedByIp] nvarchar(64) NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [UserRoles] (
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [AssignedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AdminAlerts_IsResolved_CreatedOnUtc] ON [AdminAlerts] ([IsResolved], [CreatedOnUtc]);

CREATE INDEX [IX_ApplicationLogs_CorrelationId] ON [ApplicationLogs] ([CorrelationId]);

CREATE INDEX [IX_ApplicationLogs_TimeStamp] ON [ApplicationLogs] ([TimeStamp]);

CREATE INDEX [IX_AuditTrails_EntityType_EntityId] ON [AuditTrails] ([EntityType], [EntityId]);

CREATE INDEX [IX_AuditTrails_TimestampUtc] ON [AuditTrails] ([TimestampUtc]);

CREATE INDEX [IX_BackgroundTaskLogs_TaskName_StartedOnUtc] ON [BackgroundTaskLogs] ([TaskName], [StartedOnUtc]);

CREATE UNIQUE INDEX [IX_Customers_UserId] ON [Customers] ([UserId]);

CREATE INDEX [IX_DeliveryZones_StoreId] ON [DeliveryZones] ([StoreId]);

CREATE INDEX [IX_EmailQueue_Status_NextAttemptOnUtc] ON [EmailQueue] ([Status], [NextAttemptOnUtc]);

CREATE UNIQUE INDEX [IX_EmailTemplates_TemplateKey] ON [EmailTemplates] ([TemplateKey]);

CREATE UNIQUE INDEX [IX_MarketingSuppressions_RecipientEmail] ON [MarketingSuppressions] ([RecipientEmail]);

CREATE UNIQUE INDEX [IX_PlatformSettings_Key] ON [PlatformSettings] ([Key]);

CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);

CREATE INDEX [IX_RefreshTokens_UserId_RevokedOnUtc] ON [RefreshTokens] ([UserId], [RevokedOnUtc]);

CREATE UNIQUE INDEX [IX_Roles_NormalizedName] ON [Roles] ([NormalizedName]);

CREATE INDEX [IX_SettingsChangeHistory_SettingKey_ChangedOnUtc] ON [SettingsChangeHistory] ([SettingKey], [ChangedOnUtc]);

CREATE UNIQUE INDEX [IX_SupportedCurrencies_CurrencyCode] ON [SupportedCurrencies] ([CurrencyCode]);

CREATE UNIQUE INDEX [IX_TenantCredentials_ServiceType] ON [TenantCredentials] ([ServiceType]);

CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);

CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]) WHERE [Deleted] = 0;

CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]) WHERE [Deleted] = 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260701134631_InitialSchema', N'9.0.0');

ALTER TABLE [Roles] ADD [AccessibleModules] nvarchar(max) NOT NULL DEFAULT N'[]';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260701141421_AddRoleAccessibleModules', N'9.0.0');

CREATE TABLE [ApiKeys] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [KeyHash] nvarchar(128) NOT NULL,
    [Prefix] nvarchar(32) NOT NULL,
    [Scopes] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [ExpiresAtUtc] datetime2 NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ApiKeys] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_ApiKeys_KeyHash] ON [ApiKeys] ([KeyHash]) WHERE [Deleted] = 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260701144638_AddApiKeys', N'9.0.0');

CREATE TABLE [Categories] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [ParentId] uniqueidentifier NULL,
    [Slug] nvarchar(220) NULL,
    [MetaTitle] nvarchar(300) NULL,
    [MetaDescription] nvarchar(500) NULL,
    [MetaKeywords] nvarchar(500) NULL,
    [CanonicalUrl] nvarchar(500) NULL,
    [DisplayOrder] int NOT NULL,
    [IsEnabled] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Categories_Categories_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Categories] ([Id])
);

CREATE TABLE [Manufacturers] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [LogoUrl] nvarchar(500) NULL,
    [Slug] nvarchar(220) NULL,
    [MetaTitle] nvarchar(300) NULL,
    [MetaDescription] nvarchar(500) NULL,
    [MetaKeywords] nvarchar(500) NULL,
    [DisplayOrder] int NOT NULL,
    [IsEnabled] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Manufacturers] PRIMARY KEY ([Id])
);

CREATE TABLE [ProductAttributes] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [DisplayOrder] int NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductAttributes] PRIMARY KEY ([Id])
);

CREATE TABLE [ProductTags] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Slug] nvarchar(220) NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductTags] PRIMARY KEY ([Id])
);

CREATE TABLE [SpecificationAttributes] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [IsFilterable] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_SpecificationAttributes] PRIMARY KEY ([Id])
);

CREATE TABLE [TaxCategories] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [Description] nvarchar(500) NULL,
    [DefaultRatePercent] decimal(9,4) NULL,
    [DisplayOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_TaxCategories] PRIMARY KEY ([Id])
);

CREATE TABLE [ProductAttributeValues] (
    [Id] uniqueidentifier NOT NULL,
    [ProductAttributeId] uniqueidentifier NOT NULL,
    [Value] nvarchar(400) NOT NULL,
    [DisplayOrder] int NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductAttributeValues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductAttributeValues_ProductAttributes_ProductAttributeId] FOREIGN KEY ([ProductAttributeId]) REFERENCES [ProductAttributes] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [SpecificationAttributeOptions] (
    [Id] uniqueidentifier NOT NULL,
    [SpecificationAttributeId] uniqueidentifier NOT NULL,
    [Value] nvarchar(400) NOT NULL,
    [DisplayOrder] int NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_SpecificationAttributeOptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SpecificationAttributeOptions_SpecificationAttributes_SpecificationAttributeId] FOREIGN KEY ([SpecificationAttributeId]) REFERENCES [SpecificationAttributes] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Products] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(400) NOT NULL,
    [Slug] nvarchar(400) NULL,
    [ProductType] int NOT NULL,
    [ShortDescription] nvarchar(1000) NULL,
    [FullDescription] nvarchar(max) NULL,
    [Sku] nvarchar(100) NULL,
    [Price] decimal(18,2) NULL,
    [StockQuantity] int NOT NULL,
    [AllowBackorder] bit NOT NULL,
    [EstimatedRestockDate] datetime2 NULL,
    [TaxCategoryId] uniqueidentifier NOT NULL,
    [ManufacturerId] uniqueidentifier NULL,
    [IsPublished] bit NOT NULL,
    [ReviewsEnabled] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [DownloadExpiryDays] int NULL,
    [DownloadLimit] int NULL,
    [GiftCardType] int NULL,
    [GiftCardAmount] decimal(18,2) NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Products_Manufacturers_ManufacturerId] FOREIGN KEY ([ManufacturerId]) REFERENCES [Manufacturers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Products_TaxCategories_TaxCategoryId] FOREIGN KEY ([TaxCategoryId]) REFERENCES [TaxCategories] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [GroupedProductMembers] (
    [Id] uniqueidentifier NOT NULL,
    [GroupedProductId] uniqueidentifier NOT NULL,
    [MemberProductId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_GroupedProductMembers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GroupedProductMembers_Products_GroupedProductId] FOREIGN KEY ([GroupedProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_GroupedProductMembers_Products_MemberProductId] FOREIGN KEY ([MemberProductId]) REFERENCES [Products] ([Id])
);

CREATE TABLE [ProductAttributeMappings] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductAttributeId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_ProductAttributeMappings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductAttributeMappings_ProductAttributes_ProductAttributeId] FOREIGN KEY ([ProductAttributeId]) REFERENCES [ProductAttributes] ([Id]),
    CONSTRAINT [FK_ProductAttributeMappings_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ProductCategories] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductCategories_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]),
    CONSTRAINT [FK_ProductCategories_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ProductDownloads] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [Name] nvarchar(300) NOT NULL,
    [Url] nvarchar(1000) NULL,
    [FileKey] nvarchar(500) NULL,
    [DisplayOrder] int NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductDownloads] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductDownloads_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ProductRelationships] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [RelatedProductId] uniqueidentifier NOT NULL,
    [RelationshipType] int NOT NULL,
    [DisplayOrder] int NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductRelationships] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductRelationships_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProductRelationships_Products_RelatedProductId] FOREIGN KEY ([RelatedProductId]) REFERENCES [Products] ([Id])
);

CREATE TABLE [ProductSpecificationValues] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [SpecificationAttributeOptionId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProductSpecificationValues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductSpecificationValues_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProductSpecificationValues_SpecificationAttributeOptions_SpecificationAttributeOptionId] FOREIGN KEY ([SpecificationAttributeOptionId]) REFERENCES [SpecificationAttributeOptions] ([Id])
);

CREATE TABLE [ProductTagMappings] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductTagId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProductTagMappings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductTagMappings_ProductTags_ProductTagId] FOREIGN KEY ([ProductTagId]) REFERENCES [ProductTags] ([Id]),
    CONSTRAINT [FK_ProductTagMappings_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ProductVariants] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [Sku] nvarchar(100) NULL,
    [Price] decimal(18,2) NULL,
    [StockQuantity] int NOT NULL,
    [AllowBackorder] bit NOT NULL,
    [IsEnabled] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductVariants_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [InventoryLevels] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductVariantId] uniqueidentifier NULL,
    [StoreId] uniqueidentifier NOT NULL,
    [StockQuantity] int NOT NULL,
    [LowStockThreshold] int NOT NULL,
    [ReservedQuantity] int NOT NULL,
    [LowStockAlerted] bit NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_InventoryLevels] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryLevels_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]),
    CONSTRAINT [FK_InventoryLevels_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InventoryLevels_Stores_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [Stores] ([Id])
);

CREATE TABLE [ProductImages] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductVariantId] uniqueidentifier NULL,
    [MediaType] int NOT NULL,
    [Url] nvarchar(1000) NOT NULL,
    [ThumbnailUrl] nvarchar(1000) NULL,
    [AltText] nvarchar(400) NULL,
    [DisplayOrder] int NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductImages_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]),
    CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ProductVariantAttributeValues] (
    [Id] uniqueidentifier NOT NULL,
    [ProductVariantId] uniqueidentifier NOT NULL,
    [ProductAttributeValueId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProductVariantAttributeValues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductVariantAttributeValues_ProductAttributeValues_ProductAttributeValueId] FOREIGN KEY ([ProductAttributeValueId]) REFERENCES [ProductAttributeValues] ([Id]),
    CONSTRAINT [FK_ProductVariantAttributeValues_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [TierPrices] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NULL,
    [ProductVariantId] uniqueidentifier NULL,
    [MinQuantity] int NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_TierPrices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TierPrices_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]),
    CONSTRAINT [FK_TierPrices_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Categories_ParentId] ON [Categories] ([ParentId]);

CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]) WHERE [Slug] IS NOT NULL AND [Deleted] = 0;

CREATE UNIQUE INDEX [IX_GroupedProductMembers_GroupedProductId_MemberProductId] ON [GroupedProductMembers] ([GroupedProductId], [MemberProductId]);

CREATE INDEX [IX_GroupedProductMembers_MemberProductId] ON [GroupedProductMembers] ([MemberProductId]);

CREATE UNIQUE INDEX [IX_InventoryLevels_ProductId_ProductVariantId_StoreId] ON [InventoryLevels] ([ProductId], [ProductVariantId], [StoreId]) WHERE [ProductVariantId] IS NOT NULL;

CREATE INDEX [IX_InventoryLevels_ProductVariantId] ON [InventoryLevels] ([ProductVariantId]);

CREATE INDEX [IX_InventoryLevels_StoreId] ON [InventoryLevels] ([StoreId]);

CREATE UNIQUE INDEX [IX_Manufacturers_Slug] ON [Manufacturers] ([Slug]) WHERE [Slug] IS NOT NULL AND [Deleted] = 0;

CREATE INDEX [IX_ProductAttributeMappings_ProductAttributeId] ON [ProductAttributeMappings] ([ProductAttributeId]);

CREATE UNIQUE INDEX [IX_ProductAttributeMappings_ProductId_ProductAttributeId] ON [ProductAttributeMappings] ([ProductId], [ProductAttributeId]);

CREATE INDEX [IX_ProductAttributeValues_ProductAttributeId] ON [ProductAttributeValues] ([ProductAttributeId]);

CREATE INDEX [IX_ProductCategories_CategoryId] ON [ProductCategories] ([CategoryId]);

CREATE UNIQUE INDEX [IX_ProductCategories_ProductId_CategoryId] ON [ProductCategories] ([ProductId], [CategoryId]);

CREATE INDEX [IX_ProductDownloads_ProductId] ON [ProductDownloads] ([ProductId]);

CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);

CREATE INDEX [IX_ProductImages_ProductVariantId] ON [ProductImages] ([ProductVariantId]);

CREATE INDEX [IX_ProductRelationships_ProductId_RelationshipType] ON [ProductRelationships] ([ProductId], [RelationshipType]);

CREATE INDEX [IX_ProductRelationships_RelatedProductId] ON [ProductRelationships] ([RelatedProductId]);

CREATE INDEX [IX_Products_IsPublished] ON [Products] ([IsPublished]);

CREATE INDEX [IX_Products_ManufacturerId] ON [Products] ([ManufacturerId]);

CREATE INDEX [IX_Products_ProductType] ON [Products] ([ProductType]);

CREATE UNIQUE INDEX [IX_Products_Slug] ON [Products] ([Slug]) WHERE [Slug] IS NOT NULL AND [Deleted] = 0;

CREATE INDEX [IX_Products_TaxCategoryId] ON [Products] ([TaxCategoryId]);

CREATE UNIQUE INDEX [IX_ProductSpecificationValues_ProductId_SpecificationAttributeOptionId] ON [ProductSpecificationValues] ([ProductId], [SpecificationAttributeOptionId]);

CREATE INDEX [IX_ProductSpecificationValues_SpecificationAttributeOptionId] ON [ProductSpecificationValues] ([SpecificationAttributeOptionId]);

CREATE UNIQUE INDEX [IX_ProductTagMappings_ProductId_ProductTagId] ON [ProductTagMappings] ([ProductId], [ProductTagId]);

CREATE INDEX [IX_ProductTagMappings_ProductTagId] ON [ProductTagMappings] ([ProductTagId]);

CREATE UNIQUE INDEX [IX_ProductTags_Slug] ON [ProductTags] ([Slug]) WHERE [Slug] IS NOT NULL AND [Deleted] = 0;

CREATE INDEX [IX_ProductVariantAttributeValues_ProductAttributeValueId] ON [ProductVariantAttributeValues] ([ProductAttributeValueId]);

CREATE UNIQUE INDEX [IX_ProductVariantAttributeValues_ProductVariantId_ProductAttributeValueId] ON [ProductVariantAttributeValues] ([ProductVariantId], [ProductAttributeValueId]);

CREATE INDEX [IX_ProductVariants_ProductId] ON [ProductVariants] ([ProductId]);

CREATE INDEX [IX_ProductVariants_Sku] ON [ProductVariants] ([Sku]);

CREATE INDEX [IX_SpecificationAttributeOptions_SpecificationAttributeId] ON [SpecificationAttributeOptions] ([SpecificationAttributeId]);

CREATE INDEX [IX_TierPrices_ProductId] ON [TierPrices] ([ProductId]);

CREATE INDEX [IX_TierPrices_ProductVariantId] ON [TierPrices] ([ProductVariantId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260702062323_AddCatalogAndInventory', N'9.0.0');

CREATE TABLE [Addresses] (
    [Id] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [AddressType] int NOT NULL,
    [IsDefault] bit NOT NULL,
    [FirstName] nvarchar(200) NOT NULL,
    [LastName] nvarchar(200) NOT NULL,
    [Company] nvarchar(200) NULL,
    [AddressLine1] nvarchar(255) NOT NULL,
    [AddressLine2] nvarchar(255) NULL,
    [City] nvarchar(120) NOT NULL,
    [StateProvince] nvarchar(120) NULL,
    [PostalCode] nvarchar(20) NOT NULL,
    [CountryCode] nvarchar(2) NOT NULL,
    [PhoneNumber] nvarchar(50) NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Addresses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Addresses_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [UserTokens] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Purpose] int NOT NULL,
    [TokenHash] nvarchar(512) NOT NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [ExpiresOnUtc] datetime2 NOT NULL,
    [ConsumedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_UserTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Addresses_CustomerId] ON [Addresses] ([CustomerId]);

CREATE UNIQUE INDEX [IX_Addresses_CustomerId_AddressType] ON [Addresses] ([CustomerId], [AddressType]) WHERE [IsDefault] = 1 AND [Deleted] = 0;

CREATE INDEX [IX_UserTokens_TokenHash] ON [UserTokens] ([TokenHash]);

CREATE INDEX [IX_UserTokens_UserId_Purpose] ON [UserTokens] ([UserId], [Purpose]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260702064705_AddCustomerAccountsAndTokens', N'9.0.0');

CREATE TABLE [Orders] (
    [Id] uniqueidentifier NOT NULL,
    [OrderNumber] nvarchar(40) NOT NULL,
    [CustomerId] uniqueidentifier NULL,
    [Status] int NOT NULL,
    [ContactName] nvarchar(200) NULL,
    [ContactEmail] nvarchar(256) NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [CountryCode] nvarchar(2) NULL,
    [Region] nvarchar(120) NULL,
    [PostalCode] nvarchar(20) NULL,
    [AddressLine1] nvarchar(255) NULL,
    [AddressLine2] nvarchar(255) NULL,
    [City] nvarchar(120) NULL,
    [StateProvince] nvarchar(120) NULL,
    [AssignedStoreId] uniqueidentifier NULL,
    [PlacedOnUtc] datetime2 NOT NULL,
    [RoutedOnUtc] datetime2 NULL,
    [ExcludedStoreIdsJson] nvarchar(max) NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Orders_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]),
    CONSTRAINT [FK_Orders_Stores_AssignedStoreId] FOREIGN KEY ([AssignedStoreId]) REFERENCES [Stores] ([Id])
);

CREATE TABLE [StoreManagerAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [StoreId] uniqueidentifier NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_StoreManagerAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StoreManagerAssignments_Stores_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [Stores] ([Id]),
    CONSTRAINT [FK_StoreManagerAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
);

CREATE TABLE [OrderLineItems] (
    [Id] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductVariantId] uniqueidentifier NULL,
    [ProductName] nvarchar(400) NOT NULL,
    [Sku] nvarchar(100) NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_OrderLineItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderLineItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_OrderLineItems_OrderId] ON [OrderLineItems] ([OrderId]);

CREATE INDEX [IX_OrderLineItems_ProductId] ON [OrderLineItems] ([ProductId]);

CREATE INDEX [IX_Orders_AssignedStoreId_Status] ON [Orders] ([AssignedStoreId], [Status]);

CREATE INDEX [IX_Orders_CustomerId] ON [Orders] ([CustomerId]);

CREATE UNIQUE INDEX [IX_Orders_OrderNumber] ON [Orders] ([OrderNumber]);

CREATE INDEX [IX_Orders_Status] ON [Orders] ([Status]);

CREATE INDEX [IX_StoreManagerAssignments_StoreId] ON [StoreManagerAssignments] ([StoreId]);

CREATE UNIQUE INDEX [IX_StoreManagerAssignments_UserId] ON [StoreManagerAssignments] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260702074153_AddOrders', N'9.0.0');

ALTER TABLE [Orders] ADD [AppliedCouponCode] nvarchar(64) NULL;

ALTER TABLE [Orders] ADD [CurrencyCode] nvarchar(3) NOT NULL DEFAULT N'';

ALTER TABLE [Orders] ADD [DeliveredOnUtc] datetime2 NULL;

ALTER TABLE [Orders] ADD [DiscountTotal] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Orders] ADD [PaymentStatus] int NOT NULL DEFAULT 0;

ALTER TABLE [Orders] ADD [ShippedOnUtc] datetime2 NULL;

ALTER TABLE [Orders] ADD [ShippingCarrier] nvarchar(100) NULL;

ALTER TABLE [Orders] ADD [ShippingMethodName] nvarchar(200) NULL;

ALTER TABLE [Orders] ADD [ShippingTotal] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Orders] ADD [Subtotal] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Orders] ADD [TaxBreakdownJson] nvarchar(max) NULL;

ALTER TABLE [Orders] ADD [TaxFlaggedForReview] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Orders] ADD [TaxTotal] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Orders] ADD [TrackingNumber] nvarchar(100) NULL;

CREATE TABLE [Carts] (
    [Id] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NULL,
    [SessionId] nvarchar(128) NULL,
    [AppliedCouponCode] nvarchar(64) NULL,
    [CurrencyCode] nvarchar(3) NOT NULL,
    [IsCheckedOut] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Carts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Carts_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id])
);

CREATE TABLE [Discounts] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Scope] int NOT NULL,
    [Type] int NOT NULL,
    [Value] decimal(18,2) NOT NULL,
    [ProductId] uniqueidentifier NULL,
    [CategoryId] uniqueidentifier NULL,
    [StartDateUtc] datetime2 NULL,
    [EndDateUtc] datetime2 NULL,
    [MinimumOrderValue] decimal(18,2) NULL,
    [IsExclusive] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Discounts] PRIMARY KEY ([Id])
);

CREATE TABLE [OrderStatusHistory] (
    [Id] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [FromStatus] int NOT NULL,
    [ToStatus] int NOT NULL,
    [ChangedById] uniqueidentifier NULL,
    [ChangedOnUtc] datetime2 NOT NULL,
    [Note] nvarchar(1000) NULL,
    CONSTRAINT [PK_OrderStatusHistory] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderStatusHistory_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PaymentRecords] (
    [Id] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [Method] int NOT NULL,
    [GatewayName] nvarchar(100) NULL,
    [Status] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [CurrencyCode] nvarchar(3) NOT NULL,
    [RefundedAmount] decimal(18,2) NOT NULL,
    [AuthorizationId] nvarchar(200) NULL,
    [TransactionId] nvarchar(200) NULL,
    [GatewayReference] nvarchar(200) NULL,
    [ErrorMessage] nvarchar(max) NULL,
    [AuthorizedOnUtc] datetime2 NULL,
    [CapturedOnUtc] datetime2 NULL,
    [RefundedOnUtc] datetime2 NULL,
    [AuthorizationExpiresUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_PaymentRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PaymentRecords_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ShippingMethods] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [MethodType] int NOT NULL,
    [FlatRate] decimal(18,2) NULL,
    [FreeShippingThreshold] decimal(18,2) NULL,
    [TiersJson] nvarchar(max) NULL,
    [IsEnabled] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ShippingMethods] PRIMARY KEY ([Id])
);

CREATE TABLE [ShippingZones] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [CountryCode] nvarchar(2) NOT NULL,
    [Region] nvarchar(120) NULL,
    [PostalCodeStart] nvarchar(20) NULL,
    [PostalCodeEnd] nvarchar(20) NULL,
    [IsEnabled] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ShippingZones] PRIMARY KEY ([Id])
);

CREATE TABLE [TaxProviderConfigurations] (
    [Id] uniqueidentifier NOT NULL,
    [ActiveProvider] int NOT NULL,
    [FlatRatePercent] decimal(9,4) NOT NULL,
    [IsEnabled] bit NOT NULL,
    [CacheTtlMinutes] int NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_TaxProviderConfigurations] PRIMARY KEY ([Id])
);

CREATE TABLE [CartItems] (
    [Id] uniqueidentifier NOT NULL,
    [CartId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductVariantId] uniqueidentifier NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CartItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CartItems_Carts_CartId] FOREIGN KEY ([CartId]) REFERENCES [Carts] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [CouponCodes] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(64) NOT NULL,
    [DiscountId] uniqueidentifier NOT NULL,
    [UsageType] int NOT NULL,
    [MaxRedemptions] int NULL,
    [RedemptionCount] int NOT NULL,
    [StartDateUtc] datetime2 NULL,
    [EndDateUtc] datetime2 NULL,
    [IsActive] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CouponCodes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CouponCodes_Discounts_DiscountId] FOREIGN KEY ([DiscountId]) REFERENCES [Discounts] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ShippingMethodZoneRates] (
    [Id] uniqueidentifier NOT NULL,
    [ShippingMethodId] uniqueidentifier NOT NULL,
    [ShippingZoneId] uniqueidentifier NOT NULL,
    [Rate] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_ShippingMethodZoneRates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ShippingMethodZoneRates_ShippingMethods_ShippingMethodId] FOREIGN KEY ([ShippingMethodId]) REFERENCES [ShippingMethods] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ShippingMethodZoneRates_ShippingZones_ShippingZoneId] FOREIGN KEY ([ShippingZoneId]) REFERENCES [ShippingZones] ([Id])
);

CREATE INDEX [IX_CartItems_CartId] ON [CartItems] ([CartId]);

CREATE INDEX [IX_Carts_CustomerId] ON [Carts] ([CustomerId]);

CREATE INDEX [IX_Carts_SessionId] ON [Carts] ([SessionId]);

CREATE UNIQUE INDEX [IX_CouponCodes_Code] ON [CouponCodes] ([Code]) WHERE [Deleted] = 0;

CREATE INDEX [IX_CouponCodes_DiscountId] ON [CouponCodes] ([DiscountId]);

CREATE INDEX [IX_Discounts_Scope] ON [Discounts] ([Scope]);

CREATE INDEX [IX_OrderStatusHistory_OrderId] ON [OrderStatusHistory] ([OrderId]);

CREATE INDEX [IX_PaymentRecords_OrderId] ON [PaymentRecords] ([OrderId]);

CREATE INDEX [IX_PaymentRecords_Status] ON [PaymentRecords] ([Status]);

CREATE UNIQUE INDEX [IX_ShippingMethodZoneRates_ShippingMethodId_ShippingZoneId] ON [ShippingMethodZoneRates] ([ShippingMethodId], [ShippingZoneId]);

CREATE INDEX [IX_ShippingMethodZoneRates_ShippingZoneId] ON [ShippingMethodZoneRates] ([ShippingZoneId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260702085150_AddPhase3Commerce', N'9.0.0');

CREATE TABLE [RecaptchaConfigs] (
    [Id] uniqueidentifier NOT NULL,
    [SiteKey] nvarchar(200) NULL,
    [SecretKeyEncrypted] nvarchar(max) NULL,
    [SecretKeyLast4] nvarchar(8) NULL,
    [Version] int NOT NULL,
    [ScoreThreshold] decimal(3,2) NOT NULL,
    [FailBehaviour] int NOT NULL,
    [ProtectRegister] bit NOT NULL,
    [ProtectLogin] bit NOT NULL,
    [ProtectPasswordReset] bit NOT NULL,
    [ProtectGuestCheckout] bit NOT NULL,
    [ProtectContact] bit NOT NULL,
    [ProtectNewsletter] bit NOT NULL,
    [ProtectReviewSubmit] bit NOT NULL,
    [ProtectQaSubmit] bit NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_RecaptchaConfigs] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260702121412_AddRecaptchaConfig', N'9.0.0');

CREATE TABLE [Wishlists] (
    [Id] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Wishlists] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Wishlists_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id])
);

CREATE TABLE [WishlistItems] (
    [Id] uniqueidentifier NOT NULL,
    [WishlistId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductVariantId] uniqueidentifier NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_WishlistItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WishlistItems_Wishlists_WishlistId] FOREIGN KEY ([WishlistId]) REFERENCES [Wishlists] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_WishlistItems_WishlistId_ProductId] ON [WishlistItems] ([WishlistId], [ProductId]);

CREATE INDEX [IX_Wishlists_CustomerId] ON [Wishlists] ([CustomerId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703064320_AddWishlist', N'9.0.0');

ALTER TABLE [Carts] ADD [AbandonmentNotifiedOnUtc] datetime2 NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703070750_AddAbandonedCartTracking', N'9.0.0');

ALTER TABLE [Orders] ADD [TaxProviderCalculationRef] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703071331_AddOrderTaxCalculationReference', N'9.0.0');

ALTER TABLE [ProductVariants] ADD [EstimatedRestockDate] datetime2 NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703072437_AddVariantRestockDate', N'9.0.0');

CREATE TABLE [StateNexusAccumulators] (
    [Id] uniqueidentifier NOT NULL,
    [StateCode] nvarchar(8) NOT NULL,
    [PeriodStartUtc] datetime2 NOT NULL,
    [GrossSales] decimal(18,2) NOT NULL,
    [TransactionCount] int NOT NULL,
    [ThresholdAmount] decimal(18,2) NOT NULL,
    [ThresholdTransactions] int NULL,
    [WarningPercent] decimal(5,4) NOT NULL,
    [LastAlertedAtUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_StateNexusAccumulators] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_StateNexusAccumulators_StateCode_PeriodStartUtc] ON [StateNexusAccumulators] ([StateCode], [PeriodStartUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703072838_AddTaxNexusTracking', N'9.0.0');

ALTER TABLE [OrderLineItems] ADD [QuantityShipped] int NOT NULL DEFAULT 0;

CREATE TABLE [Shipments] (
    [Id] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [ShipmentNumber] nvarchar(64) NOT NULL,
    [Status] int NOT NULL,
    [Carrier] nvarchar(128) NULL,
    [ServiceName] nvarchar(128) NULL,
    [TrackingNumber] nvarchar(128) NULL,
    [LabelAssetKey] nvarchar(512) NULL,
    [LabelPdfUrl] nvarchar(2048) NULL,
    [LabelGeneratedOnUtc] datetime2 NULL,
    [ShippedOnUtc] datetime2 NULL,
    [DeliveredOnUtc] datetime2 NULL,
    [LastPolledOnUtc] datetime2 NULL,
    [Notes] nvarchar(1024) NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Shipments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Shipments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ShipmentLineItems] (
    [Id] uniqueidentifier NOT NULL,
    [ShipmentId] uniqueidentifier NOT NULL,
    [OrderLineItemId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductVariantId] uniqueidentifier NULL,
    [ProductName] nvarchar(400) NOT NULL,
    [Sku] nvarchar(400) NULL,
    [Quantity] int NOT NULL,
    CONSTRAINT [PK_ShipmentLineItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ShipmentLineItems_Shipments_ShipmentId] FOREIGN KEY ([ShipmentId]) REFERENCES [Shipments] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ShipmentTrackingEvents] (
    [Id] uniqueidentifier NOT NULL,
    [ShipmentId] uniqueidentifier NOT NULL,
    [RawStatus] nvarchar(128) NOT NULL,
    [NormalizedStatus] int NOT NULL,
    [Description] nvarchar(512) NULL,
    [Location] nvarchar(256) NULL,
    [CheckpointOnUtc] datetime2 NOT NULL,
    [RecordedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ShipmentTrackingEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ShipmentTrackingEvents_Shipments_ShipmentId] FOREIGN KEY ([ShipmentId]) REFERENCES [Shipments] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_ShipmentLineItems_ShipmentId] ON [ShipmentLineItems] ([ShipmentId]);

CREATE INDEX [IX_Shipments_OrderId] ON [Shipments] ([OrderId]);

CREATE UNIQUE INDEX [IX_Shipments_ShipmentNumber] ON [Shipments] ([ShipmentNumber]);

CREATE INDEX [IX_Shipments_Status] ON [Shipments] ([Status]);

CREATE INDEX [IX_Shipments_TrackingNumber] ON [Shipments] ([TrackingNumber]);

CREATE INDEX [IX_ShipmentTrackingEvents_ShipmentId] ON [ShipmentTrackingEvents] ([ShipmentId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703073500_AddShipments', N'9.0.0');

CREATE TABLE [Rmas] (
    [Id] uniqueidentifier NOT NULL,
    [RmaNumber] nvarchar(64) NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [Resolution] int NOT NULL,
    [Reason] nvarchar(1024) NOT NULL,
    [ResolutionNotes] nvarchar(1024) NULL,
    [RequestedOnUtc] datetime2 NOT NULL,
    [ResolvedOnUtc] datetime2 NULL,
    [ResolvedById] uniqueidentifier NULL,
    [RefundedAmount] decimal(18,2) NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Rmas] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Rmas_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id])
);

CREATE TABLE [RmaLineItems] (
    [Id] uniqueidentifier NOT NULL,
    [RmaId] uniqueidentifier NOT NULL,
    [OrderLineItemId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductVariantId] uniqueidentifier NULL,
    [ProductName] nvarchar(400) NOT NULL,
    [Sku] nvarchar(400) NULL,
    [Quantity] int NOT NULL,
    [LineReason] nvarchar(512) NULL,
    CONSTRAINT [PK_RmaLineItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RmaLineItems_Rmas_RmaId] FOREIGN KEY ([RmaId]) REFERENCES [Rmas] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_RmaLineItems_RmaId] ON [RmaLineItems] ([RmaId]);

CREATE INDEX [IX_Rmas_CustomerId] ON [Rmas] ([CustomerId]);

CREATE INDEX [IX_Rmas_OrderId] ON [Rmas] ([OrderId]);

CREATE UNIQUE INDEX [IX_Rmas_RmaNumber] ON [Rmas] ([RmaNumber]);

CREATE INDEX [IX_Rmas_Status] ON [Rmas] ([Status]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703073846_AddReturnsRma', N'9.0.0');

CREATE TABLE [WebhookSubscriptions] (
    [Id] uniqueidentifier NOT NULL,
    [Url] nvarchar(2048) NOT NULL,
    [Secret] nvarchar(256) NOT NULL,
    [IsActive] bit NOT NULL,
    [Description] nvarchar(512) NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_WebhookSubscriptions] PRIMARY KEY ([Id])
);

CREATE TABLE [WebhookDeliveries] (
    [Id] uniqueidentifier NOT NULL,
    [SubscriptionId] uniqueidentifier NOT NULL,
    [EventType] nvarchar(128) NOT NULL,
    [PayloadJson] nvarchar(max) NOT NULL,
    [OccurredAtUtc] datetime2 NOT NULL,
    [Status] int NOT NULL,
    [AttemptCount] int NOT NULL,
    [LastResponseStatus] int NULL,
    [LastResponseBody] nvarchar(4000) NULL,
    [LastAttemptOnUtc] datetime2 NULL,
    [NextAttemptOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_WebhookDeliveries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WebhookDeliveries_WebhookSubscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [WebhookSubscriptions] ([Id])
);

CREATE TABLE [WebhookSubscriptionEvents] (
    [Id] uniqueidentifier NOT NULL,
    [SubscriptionId] uniqueidentifier NOT NULL,
    [EventType] nvarchar(128) NOT NULL,
    CONSTRAINT [PK_WebhookSubscriptionEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WebhookSubscriptionEvents_WebhookSubscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [WebhookSubscriptions] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_WebhookDeliveries_Status_NextAttemptOnUtc] ON [WebhookDeliveries] ([Status], [NextAttemptOnUtc]);

CREATE INDEX [IX_WebhookDeliveries_SubscriptionId] ON [WebhookDeliveries] ([SubscriptionId]);

CREATE INDEX [IX_WebhookSubscriptionEvents_EventType] ON [WebhookSubscriptionEvents] ([EventType]);

CREATE INDEX [IX_WebhookSubscriptionEvents_SubscriptionId_EventType] ON [WebhookSubscriptionEvents] ([SubscriptionId], [EventType]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703084133_AddWebhooks', N'9.0.0');

CREATE TABLE [ContentTranslations] (
    [Id] uniqueidentifier NOT NULL,
    [EntityType] nvarchar(64) NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [FieldName] nvarchar(64) NOT NULL,
    [LanguageCode] nvarchar(16) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ContentTranslations] PRIMARY KEY ([Id])
);

CREATE TABLE [Languages] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(16) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [NativeName] nvarchar(128) NULL,
    [IsEnabled] bit NOT NULL,
    [IsDefault] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Languages] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_ContentTranslations_EntityType_EntityId] ON [ContentTranslations] ([EntityType], [EntityId]);

CREATE UNIQUE INDEX [IX_ContentTranslations_EntityType_EntityId_FieldName_LanguageCode] ON [ContentTranslations] ([EntityType], [EntityId], [FieldName], [LanguageCode]);

CREATE UNIQUE INDEX [IX_Languages_Code] ON [Languages] ([Code]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703085058_AddLanguagesAndTranslations', N'9.0.0');

CREATE TABLE [CustomerRoles] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(512) NULL,
    [PricingRuleType] int NOT NULL,
    [DiscountPercent] decimal(5,2) NULL,
    [IsActive] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CustomerRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [CategoryRoleRestrictions] (
    [Id] uniqueidentifier NOT NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [CustomerRoleId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_CategoryRoleRestrictions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CategoryRoleRestrictions_CustomerRoles_CustomerRoleId] FOREIGN KEY ([CustomerRoleId]) REFERENCES [CustomerRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [CustomerGroupPrices] (
    [Id] uniqueidentifier NOT NULL,
    [CustomerRoleId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductVariantId] uniqueidentifier NULL,
    [Price] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_CustomerGroupPrices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CustomerGroupPrices_CustomerRoles_CustomerRoleId] FOREIGN KEY ([CustomerRoleId]) REFERENCES [CustomerRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [CustomerRoleAssignments] (
    [Id] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [CustomerRoleId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_CustomerRoleAssignments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CustomerRoleAssignments_CustomerRoles_CustomerRoleId] FOREIGN KEY ([CustomerRoleId]) REFERENCES [CustomerRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ProductRoleRestrictions] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [CustomerRoleId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProductRoleRestrictions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductRoleRestrictions_CustomerRoles_CustomerRoleId] FOREIGN KEY ([CustomerRoleId]) REFERENCES [CustomerRoles] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_CategoryRoleRestrictions_CategoryId] ON [CategoryRoleRestrictions] ([CategoryId]);

CREATE UNIQUE INDEX [IX_CategoryRoleRestrictions_CategoryId_CustomerRoleId] ON [CategoryRoleRestrictions] ([CategoryId], [CustomerRoleId]);

CREATE INDEX [IX_CategoryRoleRestrictions_CustomerRoleId] ON [CategoryRoleRestrictions] ([CustomerRoleId]);

CREATE INDEX [IX_CustomerGroupPrices_CustomerRoleId] ON [CustomerGroupPrices] ([CustomerRoleId]);

CREATE INDEX [IX_CustomerGroupPrices_ProductId_ProductVariantId] ON [CustomerGroupPrices] ([ProductId], [ProductVariantId]);

CREATE INDEX [IX_CustomerRoleAssignments_CustomerId] ON [CustomerRoleAssignments] ([CustomerId]);

CREATE UNIQUE INDEX [IX_CustomerRoleAssignments_CustomerId_CustomerRoleId] ON [CustomerRoleAssignments] ([CustomerId], [CustomerRoleId]);

CREATE INDEX [IX_CustomerRoleAssignments_CustomerRoleId] ON [CustomerRoleAssignments] ([CustomerRoleId]);

CREATE INDEX [IX_ProductRoleRestrictions_CustomerRoleId] ON [ProductRoleRestrictions] ([CustomerRoleId]);

CREATE INDEX [IX_ProductRoleRestrictions_ProductId] ON [ProductRoleRestrictions] ([ProductId]);

CREATE UNIQUE INDEX [IX_ProductRoleRestrictions_ProductId_CustomerRoleId] ON [ProductRoleRestrictions] ([ProductId], [CustomerRoleId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703090216_AddCustomerRolesAndGroupPricing', N'9.0.0');

ALTER TABLE [Stores] ADD [PickupEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Orders] ADD [IsPickup] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703090902_AddPickupInStore', N'9.0.0');

CREATE TABLE [IntegrationCategories] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NULL,
    [DisplayOrder] int NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_IntegrationCategories] PRIMARY KEY ([Id])
);

CREATE TABLE [IntegrationProviders] (
    [Id] uniqueidentifier NOT NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsEnabled] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_IntegrationProviders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IntegrationProviders_IntegrationCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [IntegrationCategories] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [CredentialDefinitions] (
    [Id] uniqueidentifier NOT NULL,
    [ProviderId] uniqueidentifier NOT NULL,
    [FieldName] nvarchar(100) NOT NULL,
    [FieldCode] nvarchar(50) NOT NULL,
    [DataType] int NOT NULL,
    [IsRequired] bit NOT NULL,
    [IsSecret] bit NOT NULL,
    [Placeholder] nvarchar(200) NULL,
    [HelpText] nvarchar(500) NULL,
    [DisplayOrder] int NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CredentialDefinitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CredentialDefinitions_IntegrationProviders_ProviderId] FOREIGN KEY ([ProviderId]) REFERENCES [IntegrationProviders] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [IntegrationCredentials] (
    [Id] uniqueidentifier NOT NULL,
    [ProviderId] uniqueidentifier NOT NULL,
    [DefinitionId] uniqueidentifier NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [LastFourChars] nvarchar(4) NULL,
    [IsSecret] bit NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_IntegrationCredentials] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IntegrationCredentials_CredentialDefinitions_DefinitionId] FOREIGN KEY ([DefinitionId]) REFERENCES [CredentialDefinitions] ([Id]),
    CONSTRAINT [FK_IntegrationCredentials_IntegrationProviders_ProviderId] FOREIGN KEY ([ProviderId]) REFERENCES [IntegrationProviders] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_CredentialDefinitions_ProviderId_FieldCode] ON [CredentialDefinitions] ([ProviderId], [FieldCode]);

CREATE UNIQUE INDEX [IX_IntegrationCategories_Code] ON [IntegrationCategories] ([Code]);

CREATE INDEX [IX_IntegrationCredentials_DefinitionId] ON [IntegrationCredentials] ([DefinitionId]);

CREATE UNIQUE INDEX [IX_IntegrationCredentials_ProviderId_DefinitionId] ON [IntegrationCredentials] ([ProviderId], [DefinitionId]);

CREATE INDEX [IX_IntegrationProviders_CategoryId] ON [IntegrationProviders] ([CategoryId]);

CREATE UNIQUE INDEX [IX_IntegrationProviders_Code] ON [IntegrationProviders] ([Code]) WHERE [Deleted] = 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260703103845_AddIntegrationCredentialVault', N'9.0.0');

ALTER TABLE [ProductAttributeValues] ADD [ColorHex] nvarchar(9) NULL;

ALTER TABLE [ProductAttributes] ADD [DisplayType] int NOT NULL DEFAULT 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260704064931_AddProductAttributeDisplayType', N'9.0.0');

CREATE TABLE [Media] (
    [Id] uniqueidentifier NOT NULL,
    [OriginalFileName] nvarchar(400) NOT NULL,
    [SeoFileName] nvarchar(400) NOT NULL,
    [AssetKey] nvarchar(1024) NOT NULL,
    [MediaType] int NOT NULL,
    [MimeType] nvarchar(200) NOT NULL,
    [FileSizeBytes] bigint NOT NULL,
    [Width] int NULL,
    [Height] int NULL,
    [AltText] nvarchar(500) NULL,
    [Title] nvarchar(300) NULL,
    [Caption] nvarchar(500) NULL,
    [Description] nvarchar(max) NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Media] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_Media_MediaType] ON [Media] ([MediaType]);

CREATE UNIQUE INDEX [IX_Media_SeoFileName] ON [Media] ([SeoFileName]) WHERE [Deleted] = 0;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260704085304_AddMediaTable', N'9.0.0');

ALTER TABLE [Discounts] ADD [RequiresCoupon] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260704123441_AddDiscountRequiresCoupon', N'9.0.0');

ALTER TABLE [Products] ADD [MetaDescription] nvarchar(500) NULL;

ALTER TABLE [Products] ADD [MetaKeywords] nvarchar(500) NULL;

ALTER TABLE [Products] ADD [MetaTitle] nvarchar(300) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260704135129_AddProductSeoFields', N'9.0.0');

ALTER TABLE [Products] ADD [FeaturedDisplayOrder] int NOT NULL DEFAULT 0;

ALTER TABLE [Products] ADD [IsFeatured] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE TABLE [ProductPictures] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [MediaId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_ProductPictures] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductPictures_Media_MediaId] FOREIGN KEY ([MediaId]) REFERENCES [Media] ([Id]),
    CONSTRAINT [FK_ProductPictures_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Products_IsFeatured_FeaturedDisplayOrder] ON [Products] ([IsFeatured], [FeaturedDisplayOrder]);

CREATE INDEX [IX_ProductPictures_MediaId] ON [ProductPictures] ([MediaId]);

CREATE INDEX [IX_ProductPictures_ProductId_DisplayOrder] ON [ProductPictures] ([ProductId], [DisplayOrder]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260704144620_AddProductPicturesAndFeatured', N'9.0.0');

ALTER TABLE [Rmas] ADD [ReplacementOrderId] uniqueidentifier NULL;

ALTER TABLE [Rmas] ADD [StoreCreditIssued] decimal(18,2) NULL;

CREATE TABLE [StoreCreditTransactions] (
    [Id] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Type] int NOT NULL,
    [CurrencyCode] nvarchar(3) NOT NULL,
    [Reason] nvarchar(512) NULL,
    [RmaId] uniqueidentifier NULL,
    [OrderId] uniqueidentifier NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_StoreCreditTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StoreCreditTransactions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id])
);

CREATE INDEX [IX_StoreCreditTransactions_CustomerId] ON [StoreCreditTransactions] ([CustomerId]);

CREATE INDEX [IX_StoreCreditTransactions_RmaId] ON [StoreCreditTransactions] ([RmaId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260704151513_AddStoreCreditAndRmaResolutions', N'9.0.0');

CREATE TABLE [CategoryPictures] (
    [Id] uniqueidentifier NOT NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [MediaId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_CategoryPictures] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CategoryPictures_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CategoryPictures_Media_MediaId] FOREIGN KEY ([MediaId]) REFERENCES [Media] ([Id])
);

CREATE INDEX [IX_CategoryPictures_CategoryId_DisplayOrder] ON [CategoryPictures] ([CategoryId], [DisplayOrder]);

CREATE INDEX [IX_CategoryPictures_MediaId] ON [CategoryPictures] ([MediaId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260708121535_AddCategoryPictures', N'9.0.0');

ALTER TABLE [ProductPictures] ADD [ProductVariantId] uniqueidentifier NULL;

ALTER TABLE [Media] ADD [Url] nvarchar(2048) NULL;


CREATE TABLE #ImgMap (ProductImageId uniqueidentifier PRIMARY KEY, MediaId uniqueidentifier NOT NULL);

INSERT INTO #ImgMap (ProductImageId, MediaId)
SELECT Id, NEWID() FROM ProductImages;

INSERT INTO Media
    (Id, OriginalFileName, SeoFileName, AssetKey, Url, MediaType, MimeType, FileSizeBytes,
     Width, Height, AltText, Title, Caption, Description, CreatedOnUtc, UpdatedOnUtc, Deleted)
SELECT
    map.MediaId,
    LEFT(pi.Url, 400),
    CONCAT('migrated-', LOWER(REPLACE(CONVERT(varchar(36), map.MediaId), '-', ''))),
    pi.Url,
    pi.Url,
    pi.MediaType,
    CASE WHEN pi.MediaType = 1 THEN 'text/html' ELSE 'image/*' END,
    0,
    NULL, NULL,
    pi.AltText, NULL, NULL, NULL,
    SYSUTCDATETIME(), SYSUTCDATETIME(), 0
FROM ProductImages pi
JOIN #ImgMap map ON map.ProductImageId = pi.Id;

INSERT INTO ProductPictures (Id, ProductId, ProductVariantId, MediaId, DisplayOrder)
SELECT NEWID(), pi.ProductId, pi.ProductVariantId, map.MediaId, pi.DisplayOrder
FROM ProductImages pi
JOIN #ImgMap map ON map.ProductImageId = pi.Id;

DROP TABLE #ImgMap;


CREATE INDEX [IX_ProductPictures_ProductVariantId] ON [ProductPictures] ([ProductVariantId]);

ALTER TABLE [ProductPictures] ADD CONSTRAINT [FK_ProductPictures_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]);

DROP TABLE [ProductImages];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260708140047_MigrateProductImagesToMedia', N'9.0.0');

ALTER TABLE [Manufacturers] ADD [LogoMediaId] uniqueidentifier NULL;


DECLARE @Map TABLE (ManufacturerId uniqueidentifier PRIMARY KEY, MediaId uniqueidentifier NOT NULL);

INSERT INTO @Map (ManufacturerId, MediaId)
SELECT Id, NEWID()
FROM Manufacturers
WHERE LogoUrl IS NOT NULL AND LTRIM(RTRIM(LogoUrl)) <> '' AND LogoMediaId IS NULL;

INSERT INTO Media
    (Id, OriginalFileName, SeoFileName, AssetKey, Url, MediaType, MimeType, FileSizeBytes,
     AltText, CreatedOnUtc, UpdatedOnUtc, Deleted)
SELECT
    map.MediaId,
    LEFT(m.LogoUrl, 400),
    CONCAT('migrated-logo-', LOWER(REPLACE(CONVERT(varchar(36), map.MediaId), '-', ''))),
    m.LogoUrl,
    m.LogoUrl,
    0,
    'image/*',
    0,
    m.Name,
    SYSUTCDATETIME(), SYSUTCDATETIME(), 0
FROM Manufacturers m
JOIN @Map map ON map.ManufacturerId = m.Id;

UPDATE m SET LogoMediaId = map.MediaId
FROM Manufacturers m
JOIN @Map map ON map.ManufacturerId = m.Id;


CREATE INDEX [IX_Manufacturers_LogoMediaId] ON [Manufacturers] ([LogoMediaId]);

ALTER TABLE [Manufacturers] ADD CONSTRAINT [FK_Manufacturers_Media_LogoMediaId] FOREIGN KEY ([LogoMediaId]) REFERENCES [Media] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260708140848_AddManufacturerLogoMedia', N'9.0.0');

ALTER TABLE [TenantBrandings] ADD [FaviconMediaId] uniqueidentifier NULL;

ALTER TABLE [TenantBrandings] ADD [LogoMediaId] uniqueidentifier NULL;


DECLARE @LogoMap TABLE (BrandingId uniqueidentifier PRIMARY KEY, MediaId uniqueidentifier NOT NULL);
INSERT INTO @LogoMap (BrandingId, MediaId)
SELECT Id, NEWID() FROM TenantBrandings
WHERE LogoUrl IS NOT NULL AND LTRIM(RTRIM(LogoUrl)) <> '' AND LogoMediaId IS NULL;

INSERT INTO Media (Id, OriginalFileName, SeoFileName, AssetKey, Url, MediaType, MimeType, FileSizeBytes, CreatedOnUtc, UpdatedOnUtc, Deleted)
SELECT map.MediaId, LEFT(t.LogoUrl, 400),
    CONCAT('migrated-brand-logo-', LOWER(REPLACE(CONVERT(varchar(36), map.MediaId), '-', ''))),
    t.LogoUrl, t.LogoUrl, 0, 'image/*', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
FROM TenantBrandings t JOIN @LogoMap map ON map.BrandingId = t.Id;

UPDATE t SET LogoMediaId = map.MediaId FROM TenantBrandings t JOIN @LogoMap map ON map.BrandingId = t.Id;

DECLARE @FavMap TABLE (BrandingId uniqueidentifier PRIMARY KEY, MediaId uniqueidentifier NOT NULL);
INSERT INTO @FavMap (BrandingId, MediaId)
SELECT Id, NEWID() FROM TenantBrandings
WHERE FaviconUrl IS NOT NULL AND LTRIM(RTRIM(FaviconUrl)) <> '' AND FaviconMediaId IS NULL;

INSERT INTO Media (Id, OriginalFileName, SeoFileName, AssetKey, Url, MediaType, MimeType, FileSizeBytes, CreatedOnUtc, UpdatedOnUtc, Deleted)
SELECT map.MediaId, LEFT(t.FaviconUrl, 400),
    CONCAT('migrated-brand-favicon-', LOWER(REPLACE(CONVERT(varchar(36), map.MediaId), '-', ''))),
    t.FaviconUrl, t.FaviconUrl, 0, 'image/*', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
FROM TenantBrandings t JOIN @FavMap map ON map.BrandingId = t.Id;

UPDATE t SET FaviconMediaId = map.MediaId FROM TenantBrandings t JOIN @FavMap map ON map.BrandingId = t.Id;


CREATE INDEX [IX_TenantBrandings_FaviconMediaId] ON [TenantBrandings] ([FaviconMediaId]);

CREATE INDEX [IX_TenantBrandings_LogoMediaId] ON [TenantBrandings] ([LogoMediaId]);

ALTER TABLE [TenantBrandings] ADD CONSTRAINT [FK_TenantBrandings_Media_FaviconMediaId] FOREIGN KEY ([FaviconMediaId]) REFERENCES [Media] ([Id]);

ALTER TABLE [TenantBrandings] ADD CONSTRAINT [FK_TenantBrandings_Media_LogoMediaId] FOREIGN KEY ([LogoMediaId]) REFERENCES [Media] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260708141347_AddTenantBrandingMedia', N'9.0.0');

ALTER TABLE [Addresses] DROP CONSTRAINT [FK_Addresses_Customers_CustomerId];

DROP INDEX [IX_Addresses_CustomerId] ON [Addresses];

DROP INDEX [IX_Addresses_CustomerId_AddressType] ON [Addresses];

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Stores]') AND [c].[name] = N'AddressLine1');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Stores] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Stores] DROP COLUMN [AddressLine1];

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Stores]') AND [c].[name] = N'AddressLine2');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Stores] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Stores] DROP COLUMN [AddressLine2];

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Stores]') AND [c].[name] = N'City');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Stores] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [Stores] DROP COLUMN [City];

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Stores]') AND [c].[name] = N'CountryCode');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Stores] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [Stores] DROP COLUMN [CountryCode];

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Stores]') AND [c].[name] = N'Latitude');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Stores] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [Stores] DROP COLUMN [Latitude];

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Stores]') AND [c].[name] = N'Longitude');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Stores] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [Stores] DROP COLUMN [Longitude];

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Stores]') AND [c].[name] = N'PostalCode');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Stores] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [Stores] DROP COLUMN [PostalCode];

DECLARE @var7 sysname;
SELECT @var7 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Stores]') AND [c].[name] = N'StateProvince');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Stores] DROP CONSTRAINT [' + @var7 + '];');
ALTER TABLE [Stores] DROP COLUMN [StateProvince];

DECLARE @var8 sysname;
SELECT @var8 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'AddressLine1');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var8 + '];');
ALTER TABLE [Orders] DROP COLUMN [AddressLine1];

DECLARE @var9 sysname;
SELECT @var9 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'AddressLine2');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var9 + '];');
ALTER TABLE [Orders] DROP COLUMN [AddressLine2];

DECLARE @var10 sysname;
SELECT @var10 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'City');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var10 + '];');
ALTER TABLE [Orders] DROP COLUMN [City];

DECLARE @var11 sysname;
SELECT @var11 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'ContactEmail');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var11 + '];');
ALTER TABLE [Orders] DROP COLUMN [ContactEmail];

DECLARE @var12 sysname;
SELECT @var12 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'ContactName');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var12 + '];');
ALTER TABLE [Orders] DROP COLUMN [ContactName];

DECLARE @var13 sysname;
SELECT @var13 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'CountryCode');
IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var13 + '];');
ALTER TABLE [Orders] DROP COLUMN [CountryCode];

DECLARE @var14 sysname;
SELECT @var14 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'Latitude');
IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var14 + '];');
ALTER TABLE [Orders] DROP COLUMN [Latitude];

DECLARE @var15 sysname;
SELECT @var15 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'Longitude');
IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var15 + '];');
ALTER TABLE [Orders] DROP COLUMN [Longitude];

DECLARE @var16 sysname;
SELECT @var16 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'PostalCode');
IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var16 + '];');
ALTER TABLE [Orders] DROP COLUMN [PostalCode];

DECLARE @var17 sysname;
SELECT @var17 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'Region');
IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var17 + '];');
ALTER TABLE [Orders] DROP COLUMN [Region];

DECLARE @var18 sysname;
SELECT @var18 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'StateProvince');
IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT [' + @var18 + '];');
ALTER TABLE [Orders] DROP COLUMN [StateProvince];

DECLARE @var19 sysname;
SELECT @var19 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'AddressType');
IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var19 + '];');
ALTER TABLE [Addresses] DROP COLUMN [AddressType];

DECLARE @var20 sysname;
SELECT @var20 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'CustomerId');
IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var20 + '];');
ALTER TABLE [Addresses] DROP COLUMN [CustomerId];

DECLARE @var21 sysname;
SELECT @var21 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'IsDefault');
IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var21 + '];');
ALTER TABLE [Addresses] DROP COLUMN [IsDefault];

ALTER TABLE [Stores] ADD [AddressId] uniqueidentifier NULL;

ALTER TABLE [Orders] ADD [ShippingAddressId] uniqueidentifier NULL;

DECLARE @var22 sysname;
SELECT @var22 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'LastName');
IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var22 + '];');
ALTER TABLE [Addresses] ALTER COLUMN [LastName] nvarchar(200) NULL;

DECLARE @var23 sysname;
SELECT @var23 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Addresses]') AND [c].[name] = N'FirstName');
IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [Addresses] DROP CONSTRAINT [' + @var23 + '];');
ALTER TABLE [Addresses] ALTER COLUMN [FirstName] nvarchar(200) NULL;

ALTER TABLE [Addresses] ADD [Email] nvarchar(256) NULL;

ALTER TABLE [Addresses] ADD [Landmark] nvarchar(200) NULL;

ALTER TABLE [Addresses] ADD [Latitude] float NULL;

ALTER TABLE [Addresses] ADD [Longitude] float NULL;

CREATE TABLE [CustomerAddresses] (
    [Id] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [AddressId] uniqueidentifier NOT NULL,
    [AddressType] int NOT NULL,
    [IsDefault] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CustomerAddresses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CustomerAddresses_Addresses_AddressId] FOREIGN KEY ([AddressId]) REFERENCES [Addresses] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CustomerAddresses_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Stores_AddressId] ON [Stores] ([AddressId]);

CREATE INDEX [IX_Orders_ShippingAddressId] ON [Orders] ([ShippingAddressId]);

CREATE INDEX [IX_CustomerAddresses_AddressId] ON [CustomerAddresses] ([AddressId]);

CREATE INDEX [IX_CustomerAddresses_CustomerId] ON [CustomerAddresses] ([CustomerId]);

CREATE UNIQUE INDEX [IX_CustomerAddresses_CustomerId_AddressType] ON [CustomerAddresses] ([CustomerId], [AddressType]) WHERE [IsDefault] = 1 AND [Deleted] = 0;

ALTER TABLE [Orders] ADD CONSTRAINT [FK_Orders_Addresses_ShippingAddressId] FOREIGN KEY ([ShippingAddressId]) REFERENCES [Addresses] ([Id]);

ALTER TABLE [Stores] ADD CONSTRAINT [FK_Stores_Addresses_AddressId] FOREIGN KEY ([AddressId]) REFERENCES [Addresses] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260709122908_CentralizeAddresses', N'9.0.0');

DECLARE @var24 sysname;
SELECT @var24 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductVariants]') AND [c].[name] = N'StockQuantity');
IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [ProductVariants] DROP CONSTRAINT [' + @var24 + '];');
ALTER TABLE [ProductVariants] DROP COLUMN [StockQuantity];

DECLARE @var25 sysname;
SELECT @var25 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'StockQuantity');
IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var25 + '];');
ALTER TABLE [Products] DROP COLUMN [StockQuantity];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260709143800_DropProductCatalogStock', N'9.0.0');

ALTER TABLE [TenantBrandings] ADD [DisplayTimeZone] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [Customers] ADD [PreferredTimeZone] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260710093946_AddDisplayTimeZones', N'9.0.0');

ALTER TABLE [EmailQueue] ADD [IsHtml] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260710101200_AddEmailQueueIsHtml', N'9.0.0');

ALTER TABLE [Stores] ADD [NotificationEmail] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260710101619_AddStoreNotificationEmail', N'9.0.0');

DROP TABLE [IntegrationCredentials];

DROP TABLE [TenantCredentials];

DROP TABLE [CredentialDefinitions];

DROP TABLE [IntegrationProviders];

DROP TABLE [IntegrationCategories];

CREATE TABLE [Credentials_AuthorizeNet] (
    [Id] uniqueidentifier NOT NULL,
    [ApplicationLoginId] nvarchar(200) NULL,
    [TransactionKey] nvarchar(max) NULL,
    [SignatureKey] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_AuthorizeNet] PRIMARY KEY ([Id])
);

CREATE TABLE [Credentials_AzureBlob] (
    [Id] uniqueidentifier NOT NULL,
    [ConnectionString] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_AzureBlob] PRIMARY KEY ([Id])
);

CREATE TABLE [Credentials_DHLExpress] (
    [Id] uniqueidentifier NOT NULL,
    [ApiKey] nvarchar(300) NULL,
    [ApiSecret] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_DHLExpress] PRIMARY KEY ([Id])
);

CREATE TABLE [Credentials_FedEx] (
    [Id] uniqueidentifier NOT NULL,
    [ApiKey] nvarchar(300) NULL,
    [ApiSecret] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_FedEx] PRIMARY KEY ([Id])
);

CREATE TABLE [Credentials_PayPal] (
    [Id] uniqueidentifier NOT NULL,
    [ClientId] nvarchar(300) NULL,
    [SecretKey] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_PayPal] PRIMARY KEY ([Id])
);

CREATE TABLE [Credentials_Razorpay] (
    [Id] uniqueidentifier NOT NULL,
    [KeyId] nvarchar(200) NULL,
    [KeySecret] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_Razorpay] PRIMARY KEY ([Id])
);

CREATE TABLE [Credentials_Square] (
    [Id] uniqueidentifier NOT NULL,
    [ApplicationId] nvarchar(200) NULL,
    [AccessToken] nvarchar(max) NULL,
    [ApplicationSecret] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_Square] PRIMARY KEY ([Id])
);

CREATE TABLE [Credentials_Stripe] (
    [Id] uniqueidentifier NOT NULL,
    [PublishableKey] nvarchar(300) NULL,
    [SecretKey] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_Stripe] PRIMARY KEY ([Id])
);

CREATE TABLE [Credentials_StripeTax] (
    [Id] uniqueidentifier NOT NULL,
    [PublishableKey] nvarchar(300) NULL,
    [SecretKey] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_StripeTax] PRIMARY KEY ([Id])
);

CREATE TABLE [Credentials_TaxJar] (
    [Id] uniqueidentifier NOT NULL,
    [BaseUrl] nvarchar(500) NULL,
    [SecretKey] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_TaxJar] PRIMARY KEY ([Id])
);

CREATE TABLE [Credentials_Twilio] (
    [Id] uniqueidentifier NOT NULL,
    [AccountSid] nvarchar(200) NULL,
    [AuthToken] nvarchar(max) NULL,
    [WhatsAppNumber] nvarchar(40) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_Twilio] PRIMARY KEY ([Id])
);

CREATE TABLE [Credentials_USPS] (
    [Id] uniqueidentifier NOT NULL,
    [ConsumerKey] nvarchar(300) NULL,
    [ConsumerSecret] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_USPS] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260713085120_ReplaceCredentialVaultWithIntegrationCredentialTables', N'9.0.0');

CREATE TABLE [Credentials_UPS] (
    [Id] uniqueidentifier NOT NULL,
    [MerchantId] nvarchar(100) NULL,
    [ClientId] nvarchar(300) NULL,
    [ClientSecret] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    [Active] bit NOT NULL,
    [IsProduction] bit NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    CONSTRAINT [PK_Credentials_UPS] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260713103004_AddUpsCredentials', N'9.0.0');

ALTER TABLE [Orders] ADD [SourceCartId] uniqueidentifier NULL;

ALTER TABLE [Credentials_Stripe] ADD [ReturnUrl] nvarchar(500) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260713125937_AddStripeReturnUrlAndOrderSourceCart', N'9.0.0');

ALTER TABLE [Stores] ADD [CashOnDeliveryEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260713140350_AddStoreCashOnDeliveryEnabled', N'9.0.0');

ALTER TABLE [TenantBrandings] ADD [BodyBackgroundColor] nvarchar(max) NULL;

ALTER TABLE [TenantBrandings] ADD [Heading1Color] nvarchar(max) NULL;

ALTER TABLE [TenantBrandings] ADD [Heading2Color] nvarchar(max) NULL;

ALTER TABLE [TenantBrandings] ADD [Heading3Color] nvarchar(max) NULL;

ALTER TABLE [TenantBrandings] ADD [Heading4Color] nvarchar(max) NULL;

ALTER TABLE [TenantBrandings] ADD [Heading5Color] nvarchar(max) NULL;

ALTER TABLE [TenantBrandings] ADD [Heading6Color] nvarchar(max) NULL;

ALTER TABLE [TenantBrandings] ADD [HeadingColor] nvarchar(max) NULL;

ALTER TABLE [TenantBrandings] ADD [LinkColor] nvarchar(max) NULL;

ALTER TABLE [TenantBrandings] ADD [ParagraphColor] nvarchar(max) NULL;

ALTER TABLE [TenantBrandings] ADD [SpanColor] nvarchar(max) NULL;

ALTER TABLE [TenantBrandings] ADD [TextColor] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260714122756_AddBrandingTagColors', N'9.0.0');

CREATE TABLE [ShippingProviderConfigurations] (
    [Id] uniqueidentifier NOT NULL,
    [IsEnabled] bit NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ShippingProviderConfigurations] PRIMARY KEY ([Id])
);

CREATE TABLE [ShippingCarrierSettings] (
    [Id] uniqueidentifier NOT NULL,
    [ShippingProviderConfigurationId] uniqueidentifier NOT NULL,
    [Carrier] int NOT NULL,
    [IsEnabled] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_ShippingCarrierSettings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ShippingCarrierSettings_ShippingProviderConfigurations_ShippingProviderConfigurationId] FOREIGN KEY ([ShippingProviderConfigurationId]) REFERENCES [ShippingProviderConfigurations] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_ShippingCarrierSettings_ShippingProviderConfigurationId_Carrier] ON [ShippingCarrierSettings] ([ShippingProviderConfigurationId], [Carrier]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715065453_AddShippingProviderConfiguration', N'9.0.0');

ALTER TABLE [ShippingProviderConfigurations] ADD [AssumedTransitDays] int NOT NULL DEFAULT 7;

ALTER TABLE [ShippingProviderConfigurations] ADD [CostVsSpeedWeight] int NOT NULL DEFAULT 50;

ALTER TABLE [ShippingProviderConfigurations] ADD [SelectionMode] int NOT NULL DEFAULT 0;

ALTER TABLE [ShippingMethods] ADD [TransitDays] int NULL;

ALTER TABLE [Orders] ADD [ShippingMethodId] nvarchar(200) NULL;

ALTER TABLE [Orders] ADD [ShippingWasRecommended] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715071943_AddShippingSelectionModeAndTransitDays', N'9.0.0');

ALTER TABLE [Stores] ADD [BankTransferEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715100104_AddStoreBankTransferEnabled', N'9.0.0');

ALTER TABLE [ShippingProviderConfigurations] ADD [PickupEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715103208_AddShippingPickupEnabled', N'9.0.0');

ALTER TABLE [Credentials_USPS] ADD [BaseUrl] nvarchar(max) NULL;

ALTER TABLE [Credentials_UPS] ADD [BaseUrl] nvarchar(max) NULL;

ALTER TABLE [Credentials_FedEx] ADD [BaseUrl] nvarchar(max) NULL;

ALTER TABLE [Credentials_DHLExpress] ADD [BaseUrl] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715110756_AddCarrierCredentialBaseUrl', N'9.0.0');

ALTER TABLE [Credentials_StripeTax] ADD [BaseUrl] nvarchar(max) NULL;

ALTER TABLE [Credentials_Square] ADD [BaseUrl] nvarchar(max) NULL;

ALTER TABLE [Credentials_Razorpay] ADD [BaseUrl] nvarchar(max) NULL;

ALTER TABLE [Credentials_PayPal] ADD [BaseUrl] nvarchar(max) NULL;

ALTER TABLE [Credentials_AuthorizeNet] ADD [BaseUrl] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715112534_AddIntegrationCredentialBaseUrl', N'9.0.0');

ALTER TABLE [Credentials_FedEx] ADD [AccountNumber] nvarchar(50) NULL;

ALTER TABLE [Credentials_DHLExpress] ADD [AccountNumber] nvarchar(50) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715121727_AddCarrierAccountNumber', N'9.0.0');

DELETE FROM [CustomerGroupPrices];

ALTER TABLE [CustomerGroupPrices] DROP CONSTRAINT [FK_CustomerGroupPrices_CustomerRoles_CustomerRoleId];

DROP TABLE [CategoryRoleRestrictions];

DROP TABLE [CustomerRoleAssignments];

DROP TABLE [ProductRoleRestrictions];

DROP TABLE [CustomerRoles];

DROP INDEX [IX_CustomerGroupPrices_CustomerRoleId] ON [CustomerGroupPrices];

DROP INDEX [IX_CustomerGroupPrices_ProductId_ProductVariantId] ON [CustomerGroupPrices];

EXEC sp_rename N'[CustomerGroupPrices].[CustomerRoleId]', N'CustomerGroupId', 'COLUMN';

ALTER TABLE [Customers] ADD [CustomerGroupId] uniqueidentifier NULL;

CREATE TABLE [CustomerGroups] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(512) NULL,
    [PricingRuleType] int NOT NULL,
    [DiscountPercent] decimal(5,2) NULL,
    [IsActive] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CustomerGroups] PRIMARY KEY ([Id])
);

CREATE TABLE [TaxExemptionRequests] (
    [Id] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [CertificateNumber] nvarchar(200) NULL,
    [VatId] nvarchar(64) NULL,
    [AdminNote] nvarchar(1024) NULL,
    [SubmittedOnUtc] datetime2 NOT NULL,
    [ReviewedOnUtc] datetime2 NULL,
    [ReviewedById] uniqueidentifier NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_TaxExemptionRequests] PRIMARY KEY ([Id])
);

CREATE TABLE [TaxExemptionRequestDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [TaxExemptionRequestId] uniqueidentifier NOT NULL,
    [MediaId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_TaxExemptionRequestDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TaxExemptionRequestDocuments_TaxExemptionRequests_TaxExemptionRequestId] FOREIGN KEY ([TaxExemptionRequestId]) REFERENCES [TaxExemptionRequests] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Customers_CustomerGroupId] ON [Customers] ([CustomerGroupId]);

CREATE UNIQUE INDEX [IX_CustomerGroupPrices_CustomerGroupId_ProductId_ProductVariantId] ON [CustomerGroupPrices] ([CustomerGroupId], [ProductId], [ProductVariantId]);

CREATE INDEX [IX_TaxExemptionRequestDocuments_TaxExemptionRequestId] ON [TaxExemptionRequestDocuments] ([TaxExemptionRequestId]);

CREATE INDEX [IX_TaxExemptionRequests_CustomerId_SubmittedOnUtc] ON [TaxExemptionRequests] ([CustomerId], [SubmittedOnUtc]);

CREATE INDEX [IX_TaxExemptionRequests_Status] ON [TaxExemptionRequests] ([Status]);

ALTER TABLE [CustomerGroupPrices] ADD CONSTRAINT [FK_CustomerGroupPrices_CustomerGroups_CustomerGroupId] FOREIGN KEY ([CustomerGroupId]) REFERENCES [CustomerGroups] ([Id]) ON DELETE CASCADE;

ALTER TABLE [Customers] ADD CONSTRAINT [FK_Customers_CustomerGroups_CustomerGroupId] FOREIGN KEY ([CustomerGroupId]) REFERENCES [CustomerGroups] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260716083741_AddCustomerGroupsAndTaxExemptionRequests', N'9.0.0');

ALTER TABLE [OrderLineItems] ADD [DiscountAmount] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [OrderLineItems] ADD [OriginalUnitPrice] decimal(18,2) NOT NULL DEFAULT 0.0;

UPDATE [OrderLineItems] SET [OriginalUnitPrice] = [UnitPrice];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260716094133_AddOrderLinePriceBreakdown', N'9.0.0');

ALTER TABLE [Orders] ADD [PaymentFeePercent] decimal(5,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Orders] ADD [PaymentFeeTotal] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Credentials_USPS] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_UPS] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_Twilio] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_TaxJar] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_StripeTax] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_Stripe] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_Square] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_Razorpay] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_PayPal] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_FedEx] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_DHLExpress] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_AzureBlob] ADD [TransactionFeePercent] decimal(5,2) NULL;

ALTER TABLE [Credentials_AuthorizeNet] ADD [TransactionFeePercent] decimal(5,2) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260716102613_AddPaymentTransactionFees', N'9.0.0');

ALTER TABLE [Credentials_PayPal] ADD [ReturnUrl] nvarchar(500) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260716111419_AddPayPalReturnUrl', N'9.0.0');

ALTER TABLE [Credentials_Square] ADD [LocationId] nvarchar(100) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260720093147_AddSquareLocationId', N'9.0.0');

ALTER TABLE [Credentials_AuthorizeNet] ADD [PublicClientKey] nvarchar(200) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260720110348_AddAuthorizeNetPublicClientKey', N'9.0.0');

ALTER TABLE [PaymentRecords] ADD [PaymentInstrument] nvarchar(20) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260720121422_AddPaymentInstrument', N'9.0.0');

CREATE TABLE [ProductQuestions] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NULL,
    [AskerName] nvarchar(200) NOT NULL,
    [AskerEmail] nvarchar(256) NULL,
    [QuestionText] nvarchar(2000) NOT NULL,
    [Status] int NOT NULL,
    [AnswerText] nvarchar(4000) NULL,
    [AnsweredOnUtc] datetime2 NULL,
    [AnsweredById] uniqueidentifier NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductQuestions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductQuestions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
);

CREATE TABLE [ProductReviews] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [Rating] int NOT NULL,
    [Title] nvarchar(200) NULL,
    [Body] nvarchar(4000) NOT NULL,
    [Status] int NOT NULL,
    [ReviewerName] nvarchar(200) NOT NULL,
    [ModeratedOnUtc] datetime2 NULL,
    [ModeratedById] uniqueidentifier NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductReviews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductReviews_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]),
    CONSTRAINT [FK_ProductReviews_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
);

CREATE INDEX [IX_ProductQuestions_ProductId_Status] ON [ProductQuestions] ([ProductId], [Status]);

CREATE INDEX [IX_ProductReviews_CustomerId] ON [ProductReviews] ([CustomerId]);

CREATE INDEX [IX_ProductReviews_ProductId_Status] ON [ProductReviews] ([ProductId], [Status]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260721072825_AddPhase5ReviewsAndQa', N'9.0.0');

ALTER TABLE [Categories] ADD [IsFeatured] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE TABLE [CMSBanners] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(300) NOT NULL,
    [Subtitle] nvarchar(500) NULL,
    [ImageMediaId] uniqueidentifier NULL,
    [LinkUrl] nvarchar(1000) NULL,
    [CtaLabel] nvarchar(120) NULL,
    [DisplayLocation] nvarchar(100) NOT NULL,
    [StartsOnUtc] datetime2 NULL,
    [EndsOnUtc] datetime2 NULL,
    [DisplayOrder] int NOT NULL,
    [IsEnabled] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CMSBanners] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CMSBanners_Media_ImageMediaId] FOREIGN KEY ([ImageMediaId]) REFERENCES [Media] ([Id])
);

CREATE TABLE [CMSBlogPosts] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(300) NOT NULL,
    [Slug] nvarchar(300) NOT NULL,
    [Summary] nvarchar(1000) NULL,
    [Body] nvarchar(max) NULL,
    [Author] nvarchar(200) NULL,
    [Tags] nvarchar(1000) NULL,
    [FeaturedImageMediaId] uniqueidentifier NULL,
    [Status] int NOT NULL,
    [PublishedOnUtc] datetime2 NULL,
    [MetaTitle] nvarchar(300) NULL,
    [MetaDescription] nvarchar(500) NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CMSBlogPosts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CMSBlogPosts_Media_FeaturedImageMediaId] FOREIGN KEY ([FeaturedImageMediaId]) REFERENCES [Media] ([Id])
);

CREATE TABLE [CMSHomePageSections] (
    [Id] uniqueidentifier NOT NULL,
    [SectionType] int NOT NULL,
    [DisplayName] nvarchar(200) NOT NULL,
    [DisplayOrder] int NOT NULL,
    [IsEnabled] bit NOT NULL,
    [Configuration] nvarchar(max) NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CMSHomePageSections] PRIMARY KEY ([Id])
);

CREATE TABLE [CMSNewsletterSubscriptions] (
    [Id] uniqueidentifier NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [Status] int NOT NULL,
    [ConfirmedOnUtc] datetime2 NULL,
    [UnsubscribedOnUtc] datetime2 NULL,
    [Source] nvarchar(100) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CMSNewsletterSubscriptions] PRIMARY KEY ([Id])
);

CREATE TABLE [CMSPageGroups] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Slug] nvarchar(220) NULL,
    [DisplayOrder] int NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CMSPageGroups] PRIMARY KEY ([Id])
);

CREATE TABLE [CMSProductCollections] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [Slug] nvarchar(220) NULL,
    [IsEnabled] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CMSProductCollections] PRIMARY KEY ([Id])
);

CREATE TABLE [CMSPages] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(300) NOT NULL,
    [Slug] nvarchar(300) NOT NULL,
    [Body] nvarchar(max) NULL,
    [MetaTitle] nvarchar(300) NULL,
    [MetaDescription] nvarchar(500) NULL,
    [MetaKeywords] nvarchar(500) NULL,
    [CanonicalUrl] nvarchar(500) NULL,
    [Status] int NOT NULL,
    [PageGroupId] uniqueidentifier NULL,
    [DisplayOrder] int NOT NULL,
    [IsSystemPage] bit NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CMSPages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CMSPages_CMSPageGroups_PageGroupId] FOREIGN KEY ([PageGroupId]) REFERENCES [CMSPageGroups] ([Id])
);

CREATE TABLE [CMSCategoryPageConfigs] (
    [Id] uniqueidentifier NOT NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [BannerMediaId] uniqueidentifier NULL,
    [PromotionalDescription] nvarchar(max) NULL,
    [YmalCollectionId] uniqueidentifier NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CMSCategoryPageConfigs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CMSCategoryPageConfigs_CMSProductCollections_YmalCollectionId] FOREIGN KEY ([YmalCollectionId]) REFERENCES [CMSProductCollections] ([Id]),
    CONSTRAINT [FK_CMSCategoryPageConfigs_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]),
    CONSTRAINT [FK_CMSCategoryPageConfigs_Media_BannerMediaId] FOREIGN KEY ([BannerMediaId]) REFERENCES [Media] ([Id])
);

CREATE TABLE [CMSProductCollectionItems] (
    [Id] uniqueidentifier NOT NULL,
    [CollectionId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_CMSProductCollectionItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CMSProductCollectionItems_CMSProductCollections_CollectionId] FOREIGN KEY ([CollectionId]) REFERENCES [CMSProductCollections] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CMSProductCollectionItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
);

CREATE TABLE [CMSSearchPageContent] (
    [Id] uniqueidentifier NOT NULL,
    [Heading] nvarchar(300) NULL,
    [PlaceholderText] nvarchar(300) NULL,
    [ResultsCountLabel] nvarchar(300) NULL,
    [NoResultsMessage] nvarchar(max) NULL,
    [NoResultsBannerId] uniqueidentifier NULL,
    [NoResultsCollectionId] uniqueidentifier NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CMSSearchPageContent] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CMSSearchPageContent_CMSBanners_NoResultsBannerId] FOREIGN KEY ([NoResultsBannerId]) REFERENCES [CMSBanners] ([Id]),
    CONSTRAINT [FK_CMSSearchPageContent_CMSProductCollections_NoResultsCollectionId] FOREIGN KEY ([NoResultsCollectionId]) REFERENCES [CMSProductCollections] ([Id])
);

CREATE TABLE [CMSCategoryPinnedProducts] (
    [Id] uniqueidentifier NOT NULL,
    [CategoryPageConfigId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_CMSCategoryPinnedProducts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CMSCategoryPinnedProducts_CMSCategoryPageConfigs_CategoryPageConfigId] FOREIGN KEY ([CategoryPageConfigId]) REFERENCES [CMSCategoryPageConfigs] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CMSCategoryPinnedProducts_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
);

CREATE INDEX [IX_Categories_IsFeatured] ON [Categories] ([IsFeatured]);

CREATE INDEX [IX_CMSBanners_DisplayLocation_DisplayOrder] ON [CMSBanners] ([DisplayLocation], [DisplayOrder]);

CREATE INDEX [IX_CMSBanners_ImageMediaId] ON [CMSBanners] ([ImageMediaId]);

CREATE INDEX [IX_CMSBlogPosts_FeaturedImageMediaId] ON [CMSBlogPosts] ([FeaturedImageMediaId]);

CREATE UNIQUE INDEX [IX_CMSBlogPosts_Slug] ON [CMSBlogPosts] ([Slug]) WHERE [Deleted] = 0;

CREATE INDEX [IX_CMSBlogPosts_Status_PublishedOnUtc] ON [CMSBlogPosts] ([Status], [PublishedOnUtc]);

CREATE INDEX [IX_CMSCategoryPageConfigs_BannerMediaId] ON [CMSCategoryPageConfigs] ([BannerMediaId]);

CREATE UNIQUE INDEX [IX_CMSCategoryPageConfigs_CategoryId] ON [CMSCategoryPageConfigs] ([CategoryId]);

CREATE INDEX [IX_CMSCategoryPageConfigs_YmalCollectionId] ON [CMSCategoryPageConfigs] ([YmalCollectionId]);

CREATE INDEX [IX_CMSCategoryPinnedProducts_CategoryPageConfigId_DisplayOrder] ON [CMSCategoryPinnedProducts] ([CategoryPageConfigId], [DisplayOrder]);

CREATE UNIQUE INDEX [IX_CMSCategoryPinnedProducts_CategoryPageConfigId_ProductId] ON [CMSCategoryPinnedProducts] ([CategoryPageConfigId], [ProductId]);

CREATE INDEX [IX_CMSCategoryPinnedProducts_ProductId] ON [CMSCategoryPinnedProducts] ([ProductId]);

CREATE INDEX [IX_CMSHomePageSections_DisplayOrder] ON [CMSHomePageSections] ([DisplayOrder]);

CREATE UNIQUE INDEX [IX_CMSNewsletterSubscriptions_Email] ON [CMSNewsletterSubscriptions] ([Email]);

CREATE UNIQUE INDEX [IX_CMSPageGroups_Slug] ON [CMSPageGroups] ([Slug]) WHERE [Slug] IS NOT NULL AND [Deleted] = 0;

CREATE INDEX [IX_CMSPages_PageGroupId_DisplayOrder] ON [CMSPages] ([PageGroupId], [DisplayOrder]);

CREATE UNIQUE INDEX [IX_CMSPages_Slug] ON [CMSPages] ([Slug]) WHERE [Deleted] = 0;

CREATE INDEX [IX_CMSProductCollectionItems_CollectionId_DisplayOrder] ON [CMSProductCollectionItems] ([CollectionId], [DisplayOrder]);

CREATE UNIQUE INDEX [IX_CMSProductCollectionItems_CollectionId_ProductId] ON [CMSProductCollectionItems] ([CollectionId], [ProductId]);

CREATE INDEX [IX_CMSProductCollectionItems_ProductId] ON [CMSProductCollectionItems] ([ProductId]);

CREATE UNIQUE INDEX [IX_CMSProductCollections_Slug] ON [CMSProductCollections] ([Slug]) WHERE [Slug] IS NOT NULL AND [Deleted] = 0;

CREATE INDEX [IX_CMSSearchPageContent_NoResultsBannerId] ON [CMSSearchPageContent] ([NoResultsBannerId]);

CREATE INDEX [IX_CMSSearchPageContent_NoResultsCollectionId] ON [CMSSearchPageContent] ([NoResultsCollectionId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260721073542_AddPhase5Cms', N'9.0.0');

ALTER TABLE [Products] ADD [IsSubscribable] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Products] ADD [SubscriptionIntervals] nvarchar(200) NULL;

ALTER TABLE [Orders] ADD [PointsDiscountAmount] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Orders] ADD [PointsRedeemed] int NOT NULL DEFAULT 0;

ALTER TABLE [Carts] ADD [PointsApplied] int NOT NULL DEFAULT 0;

ALTER TABLE [Carts] ADD [PointsDiscountAmount] decimal(18,2) NOT NULL DEFAULT 0.0;

CREATE TABLE [CustomerPointsBalances] (
    [Id] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [Balance] int NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CustomerPointsBalances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CustomerPointsBalances_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id])
);

CREATE TABLE [PointsTransactions] (
    [Id] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [Points] int NOT NULL,
    [Type] int NOT NULL,
    [Reason] nvarchar(300) NULL,
    [OrderId] uniqueidentifier NULL,
    [BalanceAfter] int NOT NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_PointsTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PointsTransactions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id])
);

CREATE TABLE [Subscriptions] (
    [Id] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductVariantId] uniqueidentifier NULL,
    [Quantity] int NOT NULL,
    [Interval] int NOT NULL,
    [Status] int NOT NULL,
    [NextOrderOnUtc] datetime2 NOT NULL,
    [LastOrderOnUtc] datetime2 NULL,
    [ShippingAddressId] uniqueidentifier NULL,
    [PaymentMethodRef] nvarchar(200) NULL,
    [FailureCount] int NOT NULL,
    [Deleted] bit NOT NULL,
    [DeletedOnUtc] datetime2 NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Subscriptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Subscriptions_Addresses_ShippingAddressId] FOREIGN KEY ([ShippingAddressId]) REFERENCES [Addresses] ([Id]),
    CONSTRAINT [FK_Subscriptions_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]),
    CONSTRAINT [FK_Subscriptions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
);

CREATE UNIQUE INDEX [IX_CustomerPointsBalances_CustomerId] ON [CustomerPointsBalances] ([CustomerId]);

CREATE INDEX [IX_PointsTransactions_CustomerId_CreatedOnUtc] ON [PointsTransactions] ([CustomerId], [CreatedOnUtc]);

CREATE INDEX [IX_Subscriptions_CustomerId] ON [Subscriptions] ([CustomerId]);

CREATE INDEX [IX_Subscriptions_ProductId] ON [Subscriptions] ([ProductId]);

CREATE INDEX [IX_Subscriptions_ShippingAddressId] ON [Subscriptions] ([ShippingAddressId]);

CREATE INDEX [IX_Subscriptions_Status_NextOrderOnUtc] ON [Subscriptions] ([Status], [NextOrderOnUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260721120407_AddPhase5Commerce', N'9.0.0');

ALTER TABLE [RecaptchaConfigs] ADD [ProtectInquiry] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Products] ADD [InquiryButtonLabel] nvarchar(80) NULL;

ALTER TABLE [Products] ADD [IsInquiryOnly] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Orders] ADD [CompanyName] nvarchar(200) NULL;

ALTER TABLE [Orders] ADD [CustomerNote] nvarchar(4000) NULL;

ALTER TABLE [Orders] ADD [InquiryStatus] int NULL;

ALTER TABLE [Orders] ADD [InternalNotes] nvarchar(4000) NULL;

ALTER TABLE [Orders] ADD [IsInquiry] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Orders] ADD [PreferredContact] int NULL;

ALTER TABLE [Orders] ADD [QuotedOnUtc] datetime2 NULL;

ALTER TABLE [Orders] ADD [RequiredByUtc] datetime2 NULL;

CREATE INDEX [IX_Orders_IsInquiry] ON [Orders] ([IsInquiry]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260822072307_AddInquiryCheckout', N'9.0.0');

ALTER TABLE [CMSPages] ADD [ShowInFooter] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [CMSPages] ADD [ShowInTopBar] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE INDEX [IX_CMSPages_Status_DisplayOrder] ON [CMSPages] ([Status], [DisplayOrder]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260824124933_AddCmsPageVisibilityToggles', N'9.0.0');

ALTER TABLE [ProductAttributes] ADD [InputType] int NOT NULL DEFAULT 0;

ALTER TABLE [ProductAttributes] ADD [IsRequired] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [ProductAttributes] ADD [MaxLength] int NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260826130913_AddProductAttributeCustomInput', N'9.0.0');

ALTER TABLE [OrderLineItems] ADD [CustomAttributesJson] nvarchar(max) NULL;

ALTER TABLE [CartItems] ADD [CustomAttributesJson] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260826132918_AddLineItemCustomAttributes', N'9.0.0');

ALTER TABLE [ProductVariants] ADD [Cost] decimal(18,2) NULL;

ALTER TABLE [ProductVariants] ADD [Gtin] nvarchar(50) NULL;

ALTER TABLE [ProductVariants] ADD [Msrp] decimal(18,2) NULL;

ALTER TABLE [ProductVariants] ADD [Weight] decimal(18,4) NULL;

ALTER TABLE [Products] ADD [BackorderMessage] nvarchar(500) NULL;

ALTER TABLE [Products] ADD [Cost] decimal(18,2) NULL;

ALTER TABLE [Products] ADD [FreightClass] nvarchar(10) NULL;

ALTER TABLE [Products] ADD [Gtin] nvarchar(50) NULL;

ALTER TABLE [Products] ADD [H1] nvarchar(300) NULL;

ALTER TABLE [Products] ADD [Height] decimal(18,2) NULL;

ALTER TABLE [Products] ADD [IsFflRequired] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Products] ADD [IsHazardous] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Products] ADD [IsPurchasable] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [Products] ADD [Length] decimal(18,2) NULL;

ALTER TABLE [Products] ADD [MaxOrderQuantity] int NULL;

ALTER TABLE [Products] ADD [MetaCustomTags] nvarchar(max) NULL;

ALTER TABLE [Products] ADD [MetaFooterTags] nvarchar(max) NULL;

ALTER TABLE [Products] ADD [MetaImageMediaId] uniqueidentifier NULL;

ALTER TABLE [Products] ADD [MetaOpenGraph] nvarchar(max) NULL;

ALTER TABLE [Products] ADD [MinOrderQuantity] int NULL;

ALTER TABLE [Products] ADD [Msrp] decimal(18,2) NULL;

ALTER TABLE [Products] ADD [PackagePieces] int NOT NULL DEFAULT 1;

ALTER TABLE [Products] ADD [PackageType] nvarchar(50) NULL;

ALTER TABLE [Products] ADD [SaleEndDateUtc] datetime2 NULL;

ALTER TABLE [Products] ADD [SalePrice] decimal(18,2) NULL;

ALTER TABLE [Products] ADD [SaleStartDateUtc] datetime2 NULL;

ALTER TABLE [Products] ADD [StockStatusOverride] int NULL;

ALTER TABLE [Products] ADD [TaxCode] nvarchar(50) NULL;

ALTER TABLE [Products] ADD [Weight] decimal(18,4) NULL;

ALTER TABLE [Products] ADD [Width] decimal(18,2) NULL;

ALTER TABLE [Categories] ADD [H1] nvarchar(300) NULL;

ALTER TABLE [Categories] ADD [Image2AltText] nvarchar(300) NULL;

ALTER TABLE [Categories] ADD [Image2MediaId] uniqueidentifier NULL;

ALTER TABLE [Categories] ADD [ImageAltText] nvarchar(300) NULL;

ALTER TABLE [Categories] ADD [ImageMediaId] uniqueidentifier NULL;

ALTER TABLE [Categories] ADD [MetaCustomTags] nvarchar(max) NULL;

ALTER TABLE [Categories] ADD [MetaFooterTags] nvarchar(max) NULL;

ALTER TABLE [Categories] ADD [MetaImageMediaId] uniqueidentifier NULL;

ALTER TABLE [Categories] ADD [MetaOpenGraph] nvarchar(max) NULL;

ALTER TABLE [Categories] ADD [ShowBrandFilter] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Categories] ADD [ShowInMenu] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [Categories] ADD [Summary] nvarchar(2000) NULL;

CREATE TABLE [CategoryFilterAttributes] (
    [Id] uniqueidentifier NOT NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [SpecificationAttributeId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_CategoryFilterAttributes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CategoryFilterAttributes_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CategoryFilterAttributes_SpecificationAttributes_SpecificationAttributeId] FOREIGN KEY ([SpecificationAttributeId]) REFERENCES [SpecificationAttributes] ([Id])
);

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

CREATE TABLE [ProductStoreMappings] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [StoreId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProductStoreMappings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductStoreMappings_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProductStoreMappings_Stores_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [Stores] ([Id])
);

CREATE TABLE [ProductCustomFieldValues] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductCustomFieldId] uniqueidentifier NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_ProductCustomFieldValues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductCustomFieldValues_ProductCustomFields_ProductCustomFieldId] FOREIGN KEY ([ProductCustomFieldId]) REFERENCES [ProductCustomFields] ([Id]),
    CONSTRAINT [FK_ProductCustomFieldValues_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_ProductVariants_Gtin] ON [ProductVariants] ([Gtin]) WHERE [Gtin] IS NOT NULL;

CREATE INDEX [IX_Products_Gtin] ON [Products] ([Gtin]) WHERE [Gtin] IS NOT NULL;

CREATE INDEX [IX_Products_MetaImageMediaId] ON [Products] ([MetaImageMediaId]);

CREATE INDEX [IX_Products_Sku] ON [Products] ([Sku]) WHERE [Sku] IS NOT NULL;

ALTER TABLE [Products] ADD CONSTRAINT [CK_Products_OrderQuantityRange] CHECK (([MinOrderQuantity] IS NULL OR [MinOrderQuantity] >= 1) AND ([MaxOrderQuantity] IS NULL OR [MaxOrderQuantity] >= 1) AND ([MinOrderQuantity] IS NULL OR [MaxOrderQuantity] IS NULL OR [MaxOrderQuantity] >= [MinOrderQuantity]));

ALTER TABLE [Products] ADD CONSTRAINT [CK_Products_PackagePieces] CHECK ([PackagePieces] >= 1);

ALTER TABLE [Products] ADD CONSTRAINT [CK_Products_SaleWindow] CHECK ([SaleStartDateUtc] IS NULL OR [SaleEndDateUtc] IS NULL OR [SaleEndDateUtc] >= [SaleStartDateUtc]);

CREATE INDEX [IX_Categories_Image2MediaId] ON [Categories] ([Image2MediaId]);

CREATE INDEX [IX_Categories_ImageMediaId] ON [Categories] ([ImageMediaId]);

CREATE INDEX [IX_Categories_MetaImageMediaId] ON [Categories] ([MetaImageMediaId]);

CREATE UNIQUE INDEX [IX_CategoryFilterAttributes_CategoryId_SpecificationAttributeId] ON [CategoryFilterAttributes] ([CategoryId], [SpecificationAttributeId]);

CREATE INDEX [IX_CategoryFilterAttributes_SpecificationAttributeId] ON [CategoryFilterAttributes] ([SpecificationAttributeId]);

CREATE UNIQUE INDEX [IX_ProductCustomFields_Code] ON [ProductCustomFields] ([Code]) WHERE [Deleted] = 0;

CREATE INDEX [IX_ProductCustomFieldValues_ProductCustomFieldId] ON [ProductCustomFieldValues] ([ProductCustomFieldId]);

CREATE UNIQUE INDEX [IX_ProductCustomFieldValues_ProductId_ProductCustomFieldId] ON [ProductCustomFieldValues] ([ProductId], [ProductCustomFieldId]);

CREATE UNIQUE INDEX [IX_ProductStoreMappings_ProductId_StoreId] ON [ProductStoreMappings] ([ProductId], [StoreId]);

CREATE INDEX [IX_ProductStoreMappings_StoreId] ON [ProductStoreMappings] ([StoreId]);

ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Media_Image2MediaId] FOREIGN KEY ([Image2MediaId]) REFERENCES [Media] ([Id]);

ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Media_ImageMediaId] FOREIGN KEY ([ImageMediaId]) REFERENCES [Media] ([Id]);

ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Media_MetaImageMediaId] FOREIGN KEY ([MetaImageMediaId]) REFERENCES [Media] ([Id]);

ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_Media_MetaImageMediaId] FOREIGN KEY ([MetaImageMediaId]) REFERENCES [Media] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030103310_AddProductCategoryPhpParity', N'9.0.0');

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

CREATE UNIQUE INDEX [IX_SeoMetadata_EntityType_EntityId] ON [SeoMetadata] ([EntityType], [EntityId]);

CREATE INDEX [IX_SeoMetadata_MetaImageMediaId] ON [SeoMetadata] ([MetaImageMediaId]);

EXEC(N'INSERT INTO [SeoMetadata] ([Id],[EntityType],[EntityId],[MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaImageMediaId],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags],[NoIndex],[CreatedOnUtc],[UpdatedOnUtc]) SELECT NEWID(), 1, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], NULL, [H1], [MetaImageMediaId], [MetaOpenGraph], [MetaCustomTags], [MetaFooterTags], 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [Products] WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords],[H1],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags]) IS NOT NULL OR [MetaImageMediaId] IS NOT NULL;');

EXEC(N'INSERT INTO [SeoMetadata] ([Id],[EntityType],[EntityId],[MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaImageMediaId],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags],[NoIndex],[CreatedOnUtc],[UpdatedOnUtc]) SELECT NEWID(), 2, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], [CanonicalUrl], [H1], [MetaImageMediaId], [MetaOpenGraph], [MetaCustomTags], [MetaFooterTags], 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [Categories] WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags]) IS NOT NULL OR [MetaImageMediaId] IS NOT NULL;');

EXEC(N'INSERT INTO [SeoMetadata] ([Id],[EntityType],[EntityId],[MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaImageMediaId],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags],[NoIndex],[CreatedOnUtc],[UpdatedOnUtc]) SELECT NEWID(), 3, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], NULL, NULL, NULL, NULL, NULL, NULL, 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [Manufacturers] WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords]) IS NOT NULL;');

EXEC(N'INSERT INTO [SeoMetadata] ([Id],[EntityType],[EntityId],[MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaImageMediaId],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags],[NoIndex],[CreatedOnUtc],[UpdatedOnUtc]) SELECT NEWID(), 4, [Id], [MetaTitle], [MetaDescription], [MetaKeywords], [CanonicalUrl], NULL, NULL, NULL, NULL, NULL, 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [CMSPages] WHERE COALESCE([MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl]) IS NOT NULL;');

EXEC(N'INSERT INTO [SeoMetadata] ([Id],[EntityType],[EntityId],[MetaTitle],[MetaDescription],[MetaKeywords],[CanonicalUrl],[H1],[MetaImageMediaId],[MetaOpenGraph],[MetaCustomTags],[MetaFooterTags],[NoIndex],[CreatedOnUtc],[UpdatedOnUtc]) SELECT NEWID(), 5, [Id], [MetaTitle], [MetaDescription], NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0, SYSUTCDATETIME(), SYSUTCDATETIME() FROM [CMSBlogPosts] WHERE COALESCE([MetaTitle],[MetaDescription]) IS NOT NULL;');

ALTER TABLE [Categories] DROP CONSTRAINT [FK_Categories_Media_MetaImageMediaId];

ALTER TABLE [Products] DROP CONSTRAINT [FK_Products_Media_MetaImageMediaId];

DROP INDEX [IX_Products_MetaImageMediaId] ON [Products];

DROP INDEX [IX_Categories_MetaImageMediaId] ON [Categories];

DECLARE @var26 sysname;
SELECT @var26 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'H1');
IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var26 + '];');
ALTER TABLE [Products] DROP COLUMN [H1];

DECLARE @var27 sysname;
SELECT @var27 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaCustomTags');
IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var27 + '];');
ALTER TABLE [Products] DROP COLUMN [MetaCustomTags];

DECLARE @var28 sysname;
SELECT @var28 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaDescription');
IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var28 + '];');
ALTER TABLE [Products] DROP COLUMN [MetaDescription];

DECLARE @var29 sysname;
SELECT @var29 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaFooterTags');
IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var29 + '];');
ALTER TABLE [Products] DROP COLUMN [MetaFooterTags];

DECLARE @var30 sysname;
SELECT @var30 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaImageMediaId');
IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var30 + '];');
ALTER TABLE [Products] DROP COLUMN [MetaImageMediaId];

DECLARE @var31 sysname;
SELECT @var31 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaKeywords');
IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var31 + '];');
ALTER TABLE [Products] DROP COLUMN [MetaKeywords];

DECLARE @var32 sysname;
SELECT @var32 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaOpenGraph');
IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var32 + '];');
ALTER TABLE [Products] DROP COLUMN [MetaOpenGraph];

DECLARE @var33 sysname;
SELECT @var33 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'MetaTitle');
IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT [' + @var33 + '];');
ALTER TABLE [Products] DROP COLUMN [MetaTitle];

DECLARE @var34 sysname;
SELECT @var34 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Manufacturers]') AND [c].[name] = N'MetaDescription');
IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [Manufacturers] DROP CONSTRAINT [' + @var34 + '];');
ALTER TABLE [Manufacturers] DROP COLUMN [MetaDescription];

DECLARE @var35 sysname;
SELECT @var35 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Manufacturers]') AND [c].[name] = N'MetaKeywords');
IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [Manufacturers] DROP CONSTRAINT [' + @var35 + '];');
ALTER TABLE [Manufacturers] DROP COLUMN [MetaKeywords];

DECLARE @var36 sysname;
SELECT @var36 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Manufacturers]') AND [c].[name] = N'MetaTitle');
IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [Manufacturers] DROP CONSTRAINT [' + @var36 + '];');
ALTER TABLE [Manufacturers] DROP COLUMN [MetaTitle];

DECLARE @var37 sysname;
SELECT @var37 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSPages]') AND [c].[name] = N'CanonicalUrl');
IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [CMSPages] DROP CONSTRAINT [' + @var37 + '];');
ALTER TABLE [CMSPages] DROP COLUMN [CanonicalUrl];

DECLARE @var38 sysname;
SELECT @var38 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSPages]') AND [c].[name] = N'MetaDescription');
IF @var38 IS NOT NULL EXEC(N'ALTER TABLE [CMSPages] DROP CONSTRAINT [' + @var38 + '];');
ALTER TABLE [CMSPages] DROP COLUMN [MetaDescription];

DECLARE @var39 sysname;
SELECT @var39 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSPages]') AND [c].[name] = N'MetaKeywords');
IF @var39 IS NOT NULL EXEC(N'ALTER TABLE [CMSPages] DROP CONSTRAINT [' + @var39 + '];');
ALTER TABLE [CMSPages] DROP COLUMN [MetaKeywords];

DECLARE @var40 sysname;
SELECT @var40 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSPages]') AND [c].[name] = N'MetaTitle');
IF @var40 IS NOT NULL EXEC(N'ALTER TABLE [CMSPages] DROP CONSTRAINT [' + @var40 + '];');
ALTER TABLE [CMSPages] DROP COLUMN [MetaTitle];

DECLARE @var41 sysname;
SELECT @var41 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSBlogPosts]') AND [c].[name] = N'MetaDescription');
IF @var41 IS NOT NULL EXEC(N'ALTER TABLE [CMSBlogPosts] DROP CONSTRAINT [' + @var41 + '];');
ALTER TABLE [CMSBlogPosts] DROP COLUMN [MetaDescription];

DECLARE @var42 sysname;
SELECT @var42 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CMSBlogPosts]') AND [c].[name] = N'MetaTitle');
IF @var42 IS NOT NULL EXEC(N'ALTER TABLE [CMSBlogPosts] DROP CONSTRAINT [' + @var42 + '];');
ALTER TABLE [CMSBlogPosts] DROP COLUMN [MetaTitle];

DECLARE @var43 sysname;
SELECT @var43 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'CanonicalUrl');
IF @var43 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var43 + '];');
ALTER TABLE [Categories] DROP COLUMN [CanonicalUrl];

DECLARE @var44 sysname;
SELECT @var44 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'H1');
IF @var44 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var44 + '];');
ALTER TABLE [Categories] DROP COLUMN [H1];

DECLARE @var45 sysname;
SELECT @var45 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaCustomTags');
IF @var45 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var45 + '];');
ALTER TABLE [Categories] DROP COLUMN [MetaCustomTags];

DECLARE @var46 sysname;
SELECT @var46 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaDescription');
IF @var46 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var46 + '];');
ALTER TABLE [Categories] DROP COLUMN [MetaDescription];

DECLARE @var47 sysname;
SELECT @var47 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaFooterTags');
IF @var47 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var47 + '];');
ALTER TABLE [Categories] DROP COLUMN [MetaFooterTags];

DECLARE @var48 sysname;
SELECT @var48 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaImageMediaId');
IF @var48 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var48 + '];');
ALTER TABLE [Categories] DROP COLUMN [MetaImageMediaId];

DECLARE @var49 sysname;
SELECT @var49 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaKeywords');
IF @var49 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var49 + '];');
ALTER TABLE [Categories] DROP COLUMN [MetaKeywords];

DECLARE @var50 sysname;
SELECT @var50 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaOpenGraph');
IF @var50 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var50 + '];');
ALTER TABLE [Categories] DROP COLUMN [MetaOpenGraph];

DECLARE @var51 sysname;
SELECT @var51 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'MetaTitle');
IF @var51 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var51 + '];');
ALTER TABLE [Categories] DROP COLUMN [MetaTitle];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031053252_CentralizeSeoMetadata', N'9.0.0');

CREATE TABLE [ProductAttachments] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [MediaId] uniqueidentifier NOT NULL,
    [Title] nvarchar(300) NULL,
    [DisplayOrder] int NOT NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProductAttachments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductAttachments_Media_MediaId] FOREIGN KEY ([MediaId]) REFERENCES [Media] ([Id]),
    CONSTRAINT [FK_ProductAttachments_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_ProductAttachments_MediaId] ON [ProductAttachments] ([MediaId]);

CREATE INDEX [IX_ProductAttachments_ProductId] ON [ProductAttachments] ([ProductId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031092541_AddProductAttachments', N'9.0.0');

CREATE TABLE [InventoryAuditLogs] (
    [Id] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [ProductVariantId] uniqueidentifier NULL,
    [StoreId] uniqueidentifier NULL,
    [OldQty] int NULL,
    [NewQty] int NULL,
    [OldPrice] decimal(18,2) NULL,
    [NewPrice] decimal(18,2) NULL,
    [Action] int NOT NULL,
    [ScannedCode] nvarchar(400) NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedById] uniqueidentifier NULL,
    [CreatedOnUtc] datetime2 NOT NULL,
    [UpdatedById] uniqueidentifier NULL,
    [UpdatedOnUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_InventoryAuditLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InventoryAuditLogs_ProductVariants_ProductVariantId] FOREIGN KEY ([ProductVariantId]) REFERENCES [ProductVariants] ([Id]),
    CONSTRAINT [FK_InventoryAuditLogs_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]),
    CONSTRAINT [FK_InventoryAuditLogs_Stores_StoreId] FOREIGN KEY ([StoreId]) REFERENCES [Stores] ([Id])
);

CREATE INDEX [IX_InventoryAuditLogs_CreatedOnUtc] ON [InventoryAuditLogs] ([CreatedOnUtc]);

CREATE INDEX [IX_InventoryAuditLogs_ProductId_CreatedOnUtc] ON [InventoryAuditLogs] ([ProductId], [CreatedOnUtc]);

CREATE INDEX [IX_InventoryAuditLogs_ProductVariantId] ON [InventoryAuditLogs] ([ProductVariantId]);

CREATE INDEX [IX_InventoryAuditLogs_StoreId] ON [InventoryAuditLogs] ([StoreId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031102250_AddInventoryAuditLog', N'9.0.0');

ALTER TABLE [InventoryAuditLogs] ADD [ClientIp] nvarchar(45) NULL;

ALTER TABLE [InventoryAuditLogs] ADD [MatchedBy] int NULL;

ALTER TABLE [InventoryAuditLogs] ADD [Source] int NOT NULL DEFAULT 1;

ALTER TABLE [InventoryAuditLogs] ADD [UserAgent] nvarchar(300) NULL;

ALTER TABLE [InventoryAuditLogs] ADD CONSTRAINT [CK_InventoryAuditLogs_NonNegative] CHECK (([NewQty] IS NULL OR [NewQty] >= 0) AND ([NewPrice] IS NULL OR [NewPrice] >= 0));

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031113440_AddInventoryAuditScanDetails', N'9.0.0');

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

CREATE TABLE [PriceRuleCategories] (
    [PriceRuleId] uniqueidentifier NOT NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PriceRuleCategories] PRIMARY KEY ([PriceRuleId], [CategoryId]),
    CONSTRAINT [FK_PriceRuleCategories_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]),
    CONSTRAINT [FK_PriceRuleCategories_PriceRules_PriceRuleId] FOREIGN KEY ([PriceRuleId]) REFERENCES [PriceRules] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PriceRuleCustomerGroups] (
    [PriceRuleId] uniqueidentifier NOT NULL,
    [CustomerGroupId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PriceRuleCustomerGroups] PRIMARY KEY ([PriceRuleId], [CustomerGroupId]),
    CONSTRAINT [FK_PriceRuleCustomerGroups_PriceRules_PriceRuleId] FOREIGN KEY ([PriceRuleId]) REFERENCES [PriceRules] ([Id]) ON DELETE CASCADE
);

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

CREATE INDEX [IX_PriceRuleCategories_CategoryId] ON [PriceRuleCategories] ([CategoryId]);

CREATE INDEX [IX_PriceRuleProductPrices_Lookup] ON [PriceRuleProductPrices] ([ProductId], [CustomerGroupId], [StoreId]) INCLUDE ([ProductVariantId], [PriceRuleId], [RulePrice], [StartDate], [EndDate], [Priority], [StopFurtherRules]);

CREATE UNIQUE INDEX [IX_PriceRuleProductPrices_PriceRuleId_ProductId_ProductVariantId_CustomerGroupId] ON [PriceRuleProductPrices] ([PriceRuleId], [ProductId], [ProductVariantId], [CustomerGroupId]);

CREATE INDEX [IX_PriceRules_Name] ON [PriceRules] ([Name]);

CREATE INDEX [IX_PriceRules_StoreId] ON [PriceRules] ([StoreId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261104085622_AddPriceRules', N'9.0.0');

ALTER TABLE [Shipments] ADD [EbayFulfillmentId] nvarchar(50) NULL;

ALTER TABLE [Products] ADD [ChannelEbay] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Orders] ADD [EbayBuyerMessage] nvarchar(max) NULL;

ALTER TABLE [Orders] ADD [EbayOrderId] nvarchar(50) NULL;

ALTER TABLE [OrderLineItems] ADD [EbayLineItemId] nvarchar(50) NULL;

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

CREATE TABLE [EbayCategories] (
    [CategoryId] nvarchar(20) NOT NULL,
    [ParentCategoryId] nvarchar(20) NULL,
    [Name] nvarchar(200) NOT NULL,
    [Level] int NOT NULL,
    [IsLeaf] bit NOT NULL,
    [TreeVersion] nvarchar(20) NULL,
    CONSTRAINT [PK_EbayCategories] PRIMARY KEY ([CategoryId])
);

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

CREATE TABLE [EbayListingAspects] (
    [Id] bigint NOT NULL IDENTITY,
    [EbayListingId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Value] nvarchar(500) NOT NULL,
    CONSTRAINT [PK_EbayListingAspects] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EbayListingAspects_EbayListings_EbayListingId] FOREIGN KEY ([EbayListingId]) REFERENCES [EbayListings] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_Orders_EbayOrderId] ON [Orders] ([EbayOrderId]) WHERE [EbayOrderId] IS NOT NULL;

CREATE INDEX [IX_EbayApiLogs_StoreId_CreatedOnUtc] ON [EbayApiLogs] ([StoreId], [CreatedOnUtc]);

CREATE INDEX [IX_EbayCategories_Name] ON [EbayCategories] ([Name]);

CREATE INDEX [IX_EbayCategories_ParentCategoryId] ON [EbayCategories] ([ParentCategoryId]);

CREATE INDEX [IX_EbayListingAspects_EbayListingId] ON [EbayListingAspects] ([EbayListingId]);

CREATE INDEX [IX_EbayListings_EbayListingIdExternal] ON [EbayListings] ([EbayListingIdExternal]);

CREATE INDEX [IX_EbayListings_Sku] ON [EbayListings] ([Sku]);

CREATE INDEX [IX_EbayListings_StoreId_Status] ON [EbayListings] ([StoreId], [Status]);

CREATE UNIQUE INDEX [UX_EbayListings_Product_NotEnded] ON [EbayListings] ([ProductId]) WHERE [Status] <> 2;

CREATE INDEX [IX_VehicleFitments_ProductId_Year] ON [VehicleFitments] ([ProductId], [Year]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261104092850_AddEbayChannel', N'9.0.0');

ALTER TABLE [Products] ADD [GiftCertificateOnline] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [PaymentRecords] ADD [IsMaster] bit NOT NULL DEFAULT CAST(1 AS bit);

ALTER TABLE [Orders] ADD [GiftCertificateAppliedAmount] decimal(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE [Orders] ADD [GiftCertificateCode] nvarchar(50) NULL;

ALTER TABLE [Orders] ADD [GiftCertificateId] uniqueidentifier NULL;

ALTER TABLE [OrderLineItems] ADD [GcMessage] nvarchar(1000) NULL;

ALTER TABLE [OrderLineItems] ADD [GcRecipientEmail] nvarchar(75) NULL;

ALTER TABLE [OrderLineItems] ADD [GcRecipientName] nvarchar(75) NULL;

ALTER TABLE [OrderLineItems] ADD [GcSenderEmail] nvarchar(75) NULL;

ALTER TABLE [OrderLineItems] ADD [GcSenderName] nvarchar(75) NULL;

ALTER TABLE [EmailQueue] ADD [CcEmail] nvarchar(255) NULL;

ALTER TABLE [EmailQueue] ADD [RelatedEntityId] uniqueidentifier NULL;

ALTER TABLE [EmailQueue] ADD [ReplyToEmail] nvarchar(255) NULL;

ALTER TABLE [Carts] ADD [GiftCertificateId] uniqueidentifier NULL;

ALTER TABLE [CartItems] ADD [GcMessage] nvarchar(1000) NULL;

ALTER TABLE [CartItems] ADD [GcRecipientEmail] nvarchar(75) NULL;

ALTER TABLE [CartItems] ADD [GcRecipientName] nvarchar(75) NULL;

ALTER TABLE [CartItems] ADD [GcSenderEmail] nvarchar(75) NULL;

ALTER TABLE [CartItems] ADD [GcSenderName] nvarchar(75) NULL;

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

CREATE INDEX [IX_Orders_GiftCertificateId] ON [Orders] ([GiftCertificateId]);

CREATE INDEX [IX_GiftCertificates_CreatedOnUtc] ON [GiftCertificates] ([CreatedOnUtc]);

CREATE INDEX [IX_GiftCertificates_SourceOrderId] ON [GiftCertificates] ([SourceOrderId]);

CREATE UNIQUE INDEX [IX_GiftCertificates_SourceOrderItemId_SourceUnitIndex] ON [GiftCertificates] ([SourceOrderItemId], [SourceUnitIndex]) WHERE [SourceOrderItemId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_GiftCertificates_StoreId_Code] ON [GiftCertificates] ([StoreId], [Code]);

CREATE INDEX [IX_GiftCertificateTransactions_GiftCertificateId_CreatedOnUtc] ON [GiftCertificateTransactions] ([GiftCertificateId], [CreatedOnUtc]);

CREATE INDEX [IX_GiftCertificateTransactions_OrderId] ON [GiftCertificateTransactions] ([OrderId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261104103048_AddGiftCertificates', N'9.0.0');

ALTER TABLE [ShippingMethods] ADD [FflFlatRate] decimal(18,2) NULL;

ALTER TABLE [ShippingMethods] ADD [FlatRatePerItem] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [ShippingMethods] ADD [InternationalViaUsps] bit NOT NULL DEFAULT CAST(0 AS bit);

CREATE TABLE [Credentials_Freightview] (
    [Id] uniqueidentifier NOT NULL,
    [BaseUrl] nvarchar(max) NULL,
    [ApiKey] nvarchar(max) NULL,
    [OriginType] nvarchar(50) NULL,
    [ContactName] nvarchar(200) NULL,
    [ContactPhone] nvarchar(50) NULL,
    [ContactEmail] nvarchar(256) NULL,
    [PickupInstructions] nvarchar(1000) NULL,
    [DockOpenTime] nvarchar(20) NULL,
    [DockCloseTime] nvarchar(20) NULL,
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
    CONSTRAINT [PK_Credentials_Freightview] PRIMARY KEY ([Id])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105070225_AddShippingMethodsPhpParity', N'9.0.0');

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

