/* =====================================================================================
 Authorize.Net payment upgrade — docs/authorize-net-payment-requirements.md
 Generated from EF migration AuthorizeNetPaymentUpgrade:
   dotnet ef migrations script AddFedExRestSettings AuthorizeNetPaymentUpgrade --idempotent

 Run AFTER 14_2026-11-05_AddFedExRestSettings.sql. Additive only.
   PaymentRecords            AuthorizedAmount (AN-21), AuthCode, AvsResultCode, CvvResultCode,
                             NetworkTransactionId, CardType, CardLastFour (AN-18), GatewayResponseCode,
                             GatewayReasonCode, GatewayResponseText (AN-19), CustomerProfileId,
                             CustomerPaymentProfileId (AN-30/31)
   PaymentTransactions       new: one row per gateway operation (sale, capture, void, refund) (AN-25/26/42)
   Credentials_AuthorizeNet  TransactionType (AN-13), CustomerProfilesEnabled (AN-30), LoggingEnabled (AN-55),
                             AcceptedCardTypes, DisplayName, MinOrderAmount, MaxOrderAmount,
                             AllowedCustomerGroupIds, DefaultOrderStatus (§4)
 Then back-fills existing Authorize.Net rows with the legacy values: TransactionType 'AuthCapture',
 DisplayName 'Credit Card'.
 New enum values (stored as int, no schema change): PaymentStatus.HeldForReview = 9, OrderStatus.PaymentReview = 13.

 Idempotent: guarded by __EFMigrationsHistory, safe to re-run.
 Do not hand-edit the migration part. Change the entities/configurations, add a migration, regenerate.
===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [AuthCode] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [AuthorizedAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [AvsResultCode] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [CardLastFour] nvarchar(4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [CardType] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [CustomerPaymentProfileId] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [CustomerProfileId] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [CvvResultCode] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [GatewayReasonCode] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [GatewayResponseCode] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [GatewayResponseText] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [PaymentRecords] ADD [NetworkTransactionId] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [Credentials_AuthorizeNet] ADD [AcceptedCardTypes] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [Credentials_AuthorizeNet] ADD [AllowedCustomerGroupIds] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [Credentials_AuthorizeNet] ADD [CustomerProfilesEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [Credentials_AuthorizeNet] ADD [DefaultOrderStatus] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [Credentials_AuthorizeNet] ADD [DisplayName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [Credentials_AuthorizeNet] ADD [LoggingEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [Credentials_AuthorizeNet] ADD [MaxOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [Credentials_AuthorizeNet] ADD [MinOrderAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    ALTER TABLE [Credentials_AuthorizeNet] ADD [TransactionType] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    CREATE TABLE [PaymentTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentRecordId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Success] bit NOT NULL,
        [TransactionId] nvarchar(200) NULL,
        [RefTransactionId] nvarchar(200) NULL,
        [ResponseCode] nvarchar(10) NULL,
        [ReasonCode] nvarchar(20) NULL,
        [Message] nvarchar(1000) NULL,
        [CreatedById] uniqueidentifier NULL,
        [CreatedOnUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentTransactions_PaymentRecords_PaymentRecordId] FOREIGN KEY ([PaymentRecordId]) REFERENCES [PaymentRecords] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_PaymentRecordId] ON [PaymentTransactions] ([PaymentRecordId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    EXEC(N'UPDATE [Credentials_AuthorizeNet] SET [TransactionType] = N''AuthCapture'' WHERE [TransactionType] IS NULL; UPDATE [Credentials_AuthorizeNet] SET [DisplayName] = N''Credit Card'' WHERE [DisplayName] IS NULL;')
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261106091636_AuthorizeNetPaymentUpgrade'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261106091636_AuthorizeNetPaymentUpgrade', N'9.0.0');
END;

COMMIT;
GO

