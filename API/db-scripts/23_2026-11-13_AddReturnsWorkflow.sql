/* =====================================================================================
 Orders: Returns (RMA) workflow — docs/order-return-requirements.bak.md
 Generated from EF migration AddReturnsWorkflow:
   dotnet ef migrations script AddBlacklistedCustomers AddReturnsWorkflow --idempotent

 Run AFTER 22_2026-11-12_AddBlacklistedCustomers.sql. Additive:
   - Rmas: workflow columns (status timestamps, return method/source, FFL dealer, label, refund breakdown,
     refund attempts, RowVersion concurrency token); CustomerId becomes nullable (guest orders).
   - RmaLineItems: received / accepted / restocked quantities, reason, condition, disposition, line refund.
   - New tables RmaReasons, RmaHistory, RmaMessages, RmaAttachments (children cascade from Rmas).
   - Products.IsReturnable (default 1), InventoryAuditLogs.RmaId (+ index).
 Back-fill: returns closed by the old one-step flow (Status 3) are marked received/accepted/restocked in full and
 their refund total is set from RefundedAmount / StoreCreditIssued, so their units are never re-returned or
 restocked twice. Default return reasons are seeded by the app at startup (only when RmaReasons is empty).
 Idempotent: guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Rmas]') AND [c].[name] = N'CustomerId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Rmas] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Rmas] ALTER COLUMN [CustomerId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [Adjustment] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [AdjustmentReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [ApprovedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [ClosedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [CreatedByAdminId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [EligibilityOverrideReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [ExpiryWarningSentOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [FflDealerName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [FflExpiry] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [FflNumber] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [LabelCarrier] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [LabelCost] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [LabelDeduction] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [LabelExpiresAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [LabelService] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [LabelUrl] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [LastRefundError] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [ReceivedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [RefundAttempts] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [RefundMethod] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [RefundReference] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [RefundShipping] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [RefundStartedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [RefundedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [RequestedResolution] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [RequiresFfl] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [RestockFee] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [ReturnInstructions] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [ReturnMethod] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [ShipRestricted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [ShippingRefund] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [Source] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [StatusChangedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [StoreCreditCode] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [StoreCreditGiftCertificateId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [SubtotalRefund] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [TaxRefund] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [TotalRefund] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [TrackingNumber] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Rmas] ADD [WaiveRestockFee] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [ConditionReceived] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [ConditionReported] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [CustomerComment] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [Deduction] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [DeductionReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [Disposition] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [FeeApplies] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [LineRefund] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [MerchantFault] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [QuantityAccepted] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [QuantityReceived] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [QuantityRestocked] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [ReasonId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [RmaLineItems] ADD [UnitRefund] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [Products] ADD [IsReturnable] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    ALTER TABLE [InventoryAuditLogs] ADD [RmaId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    CREATE TABLE [RmaAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [RmaId] uniqueidentifier NOT NULL,
        [RmaLineItemId] uniqueidentifier NULL,
        [StorageKey] nvarchar(500) NOT NULL,
        [OriginalFileName] nvarchar(260) NOT NULL,
        [MimeType] nvarchar(100) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [UploadedByType] int NOT NULL,
        [UploadedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RmaAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RmaAttachments_Rmas_RmaId] FOREIGN KEY ([RmaId]) REFERENCES [Rmas] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    CREATE TABLE [RmaHistory] (
        [Id] uniqueidentifier NOT NULL,
        [RmaId] uniqueidentifier NOT NULL,
        [FromStatus] int NULL,
        [ToStatus] int NOT NULL,
        [ActorType] int NOT NULL,
        [ActorId] uniqueidentifier NULL,
        [ActorName] nvarchar(256) NULL,
        [Note] nvarchar(2000) NULL,
        [IpAddress] nvarchar(45) NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RmaHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RmaHistory_Rmas_RmaId] FOREIGN KEY ([RmaId]) REFERENCES [Rmas] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    CREATE TABLE [RmaMessages] (
        [Id] uniqueidentifier NOT NULL,
        [RmaId] uniqueidentifier NOT NULL,
        [AuthorType] int NOT NULL,
        [AuthorId] uniqueidentifier NULL,
        [AuthorName] nvarchar(256) NULL,
        [Body] nvarchar(4000) NOT NULL,
        [IsInternal] bit NOT NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RmaMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RmaMessages_Rmas_RmaId] FOREIGN KEY ([RmaId]) REFERENCES [Rmas] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    CREATE TABLE [RmaReasons] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [CustomerVisible] bit NOT NULL,
        [FeeApplies] bit NOT NULL,
        [MerchantFault] bit NOT NULL,
        [AutoApprove] bit NOT NULL,
        [RequiresComment] bit NOT NULL,
        [RequiresPhoto] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_RmaReasons] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    CREATE INDEX [IX_Rmas_LabelExpiresAtUtc] ON [Rmas] ([LabelExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    CREATE INDEX [IX_RmaLineItems_OrderLineItemId] ON [RmaLineItems] ([OrderLineItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    CREATE INDEX [IX_InventoryAuditLogs_RmaId] ON [InventoryAuditLogs] ([RmaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    CREATE INDEX [IX_RmaAttachments_RmaId] ON [RmaAttachments] ([RmaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    CREATE INDEX [IX_RmaHistory_RmaId_CreatedOnUtc] ON [RmaHistory] ([RmaId], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    CREATE INDEX [IX_RmaMessages_RmaId_CreatedOnUtc] ON [RmaMessages] ([RmaId], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    CREATE INDEX [IX_RmaReasons_SortOrder] ON [RmaReasons] ([SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    EXEC(N'
    UPDATE [Rmas] SET [RequestedResolution] = CASE WHEN [Resolution] = 0 THEN 1 ELSE [Resolution] END,
                      [StatusChangedOnUtc] = COALESCE([ResolvedOnUtc], [RequestedOnUtc]);
    UPDATE [Rmas] SET [ClosedOnUtc] = [ResolvedOnUtc], [ReceivedOnUtc] = [ResolvedOnUtc], [ApprovedOnUtc] = [ResolvedOnUtc],
                      [TotalRefund] = COALESCE([RefundedAmount], [StoreCreditIssued], 0),
                      [RefundedOnUtc] = CASE WHEN [RefundedAmount] IS NOT NULL OR [StoreCreditIssued] IS NOT NULL THEN [ResolvedOnUtc] END,
                      [RefundMethod] = CASE WHEN [Resolution] = 3 THEN 2 WHEN [RefundedAmount] IS NOT NULL THEN 1 ELSE 0 END
    WHERE [Status] = 3;
    UPDATE l SET l.[QuantityReceived] = l.[Quantity], l.[QuantityAccepted] = l.[Quantity], l.[QuantityRestocked] = l.[Quantity]
    FROM [RmaLineItems] l INNER JOIN [Rmas] r ON r.[Id] = l.[RmaId]
    WHERE r.[Status] = 3;
    ')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113140125_AddReturnsWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113140125_AddReturnsWorkflow', N'9.0.0');
END;

COMMIT;
GO

