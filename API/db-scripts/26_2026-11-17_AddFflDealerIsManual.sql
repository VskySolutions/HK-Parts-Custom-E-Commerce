/* =====================================================================================
 FFL dealer directory: dealers added by hand on the admin FFL Dealers page.
 Generated from EF migration AddFflDealerIsManual:
   dotnet ef migrations script AddFflDealerShipping AddFflDealerIsManual --idempotent

 Run AFTER 24_2026-11-16_AddFflDealerShipping.sql. Additive:
   - FflDealers.IsManual (bit, default 0): the dealer was added by hand, not from the ATF licensee list.
     A full import with "Deactivate dealers not in the file" leaves these active; an import row with the
     same licence key clears the flag.
 Idempotent: guarded by __EFMigrationsHistory, safe to re-run and safe if the app already applied it at startup.
 Do not hand-edit. Change the entities/configurations, add a migration, and regenerate.
 ===================================================================================== */

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117071902_AddFflDealerIsManual'
)
BEGIN
    ALTER TABLE [FflDealers] ADD [IsManual] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117071902_AddFflDealerIsManual'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261117071902_AddFflDealerIsManual', N'9.0.0');
END;

COMMIT;
GO

