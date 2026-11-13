/* =====================================================================================
 Shipping settings at order placement — docs/shipping-order-placement-requirements.md
 Generated from EF migration AddOrderShippingSnapshot:
   dotnet ef migrations script AuthorizeNetPaymentUpgrade AddOrderShippingSnapshot --idempotent

 Run AFTER 15_2026-11-06_AuthorizeNetPaymentUpgrade.sql. Additive only.
   OrderShippings        new: one row per shipment, the shipping settings frozen at placement: method id /
                         internal name (OP-14) / display name / carrier, method price, handling, signature and
                         insurance add-ons with their fees, declared value, shipping total, ship-to type, FFL
                         dealer + number (reserved), rules version (OP-10..15, OP-60, section 8)
   ShippingRuleSettings  new singleton: signature fee, insurance bands, restricted states, allowed countries,
                         P.O. box rule, version (AD-20/21, SR-80..94). No row = the legacy defaults.
   Customers             IsShippingRestrictionExempt (SR-92 exemption)
   Carts                 PlacementLockUntilUtc (OP-56 double-submit guard)
 Then back-fills one OrderShippings row for every existing non-inquiry order from its
 ShippingMethodId / ShippingMethodName / ShippingCarrier / ShippingTotal columns (OP-61).

 Idempotent: guarded by __EFMigrationsHistory, safe to re-run.
 Do not hand-edit the migration part. Change the entities/configurations, add a migration, regenerate.
===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106104955_AddOrderShippingSnapshot'
)
BEGIN
    ALTER TABLE [Customers] ADD [IsShippingRestrictionExempt] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106104955_AddOrderShippingSnapshot'
)
BEGIN
    ALTER TABLE [Carts] ADD [PlacementLockUntilUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106104955_AddOrderShippingSnapshot'
)
BEGIN
    CREATE TABLE [OrderShippings] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [ShipmentType] int NOT NULL,
        [ShipToType] int NOT NULL,
        [MethodId] nvarchar(200) NULL,
        [MethodName] nvarchar(200) NULL,
        [DisplayName] nvarchar(300) NULL,
        [Carrier] nvarchar(100) NULL,
        [MethodPrice] decimal(18,2) NOT NULL,
        [HandlingFee] decimal(18,2) NOT NULL,
        [SignatureRequired] bit NOT NULL,
        [SignatureFee] decimal(18,2) NOT NULL,
        [InsuranceSelected] bit NOT NULL,
        [InsuranceFee] decimal(18,2) NOT NULL,
        [DeclaredValue] decimal(18,2) NOT NULL,
        [ShippingTotal] decimal(18,2) NOT NULL,
        [EstimatedDeliveryDays] int NULL,
        [WasRecommended] bit NOT NULL,
        [ItemCount] int NOT NULL,
        [WeightLb] decimal(18,3) NULL,
        [ContainsFflItems] bit NOT NULL,
        [FflDealerName] nvarchar(300) NULL,
        [FflNumber] nvarchar(50) NULL,
        [RulesVersion] int NOT NULL,
        CONSTRAINT [PK_OrderShippings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderShippings_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106104955_AddOrderShippingSnapshot'
)
BEGIN
    CREATE TABLE [ShippingRuleSettings] (
        [Id] uniqueidentifier NOT NULL,
        [SignatureEnabled] bit NOT NULL,
        [SignatureFee] decimal(18,2) NOT NULL,
        [InsuranceEnabled] bit NOT NULL,
        [InsuranceBandsJson] nvarchar(4000) NULL,
        [InsuranceExcessFeePer100] decimal(18,2) NOT NULL,
        [StateRestrictionEnabled] bit NOT NULL,
        [RestrictedStates] nvarchar(1000) NOT NULL,
        [InternationalRestrictionEnabled] bit NOT NULL,
        [AllowedCountryCodes] nvarchar(2000) NULL,
        [RefusePoBoxes] bit NOT NULL,
        [PoBoxMessage] nvarchar(500) NULL,
        [InvalidDestinationMessage] nvarchar(500) NULL,
        [Version] int NOT NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [UpdatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ShippingRuleSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106104955_AddOrderShippingSnapshot'
)
BEGIN
    CREATE INDEX [IX_OrderShippings_OrderId_ShipmentType] ON [OrderShippings] ([OrderId], [ShipmentType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106104955_AddOrderShippingSnapshot'
)
BEGIN
    EXEC(N'
    INSERT INTO [OrderShippings] ([Id], [OrderId], [ShipmentType], [ShipToType], [MethodId], [MethodName], [DisplayName], [Carrier],
        [MethodPrice], [HandlingFee], [SignatureRequired], [SignatureFee], [InsuranceSelected], [InsuranceFee], [DeclaredValue],
        [ShippingTotal], [EstimatedDeliveryDays], [WasRecommended], [ItemCount], [WeightLb], [ContainsFflItems], [FflDealerName],
        [FflNumber], [RulesVersion])
    SELECT NEWID(), o.[Id], 0,
        CASE WHEN o.[IsPickup] = 1 THEN 2 ELSE 0 END,
        COALESCE(o.[ShippingMethodId], CASE WHEN o.[IsPickup] = 1 THEN N''pickup'' WHEN o.[ShippingTotal] = 0 AND o.[ShippingMethodName] IS NULL THEN N''no_shipping'' END),
        COALESCE(o.[ShippingMethodId], CASE WHEN o.[IsPickup] = 1 THEN N''pickup'' WHEN o.[ShippingTotal] = 0 AND o.[ShippingMethodName] IS NULL THEN N''no_shipping'' END),
        COALESCE(o.[ShippingMethodName], CASE WHEN o.[IsPickup] = 1 THEN N''Pickup in store'' WHEN o.[ShippingTotal] = 0 THEN N''No Shipment Required'' END),
        o.[ShippingCarrier],
        o.[ShippingTotal], 0, 0, 0, 0, 0, o.[Subtotal],
        o.[ShippingTotal], NULL, o.[ShippingWasRecommended],
        ISNULL((SELECT SUM(l.[Quantity]) FROM [OrderLineItems] l WHERE l.[OrderId] = o.[Id]), 0),
        NULL, 0, NULL, NULL, 1
    FROM [Orders] o
    WHERE o.[IsInquiry] = 0
      AND NOT EXISTS (SELECT 1 FROM [OrderShippings] s WHERE s.[OrderId] = o.[Id]);
    ')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106104955_AddOrderShippingSnapshot'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106104955_AddOrderShippingSnapshot', N'9.0.0');
END;

COMMIT;
GO

