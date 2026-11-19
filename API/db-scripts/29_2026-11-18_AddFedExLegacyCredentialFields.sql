BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118133935_AddFedExLegacyCredentialFields'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [MeterNumber] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118133935_AddFedExLegacyCredentialFields'
)
BEGIN
    ALTER TABLE [Credentials_FedEx] ADD [Password] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118133935_AddFedExLegacyCredentialFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261118133935_AddFedExLegacyCredentialFields', N'9.0.0');
END;

COMMIT;
GO

