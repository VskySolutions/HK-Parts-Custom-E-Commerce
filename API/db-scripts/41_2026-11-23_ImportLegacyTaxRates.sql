-- 41  2026-11-23  ImportLegacyTaxRates
-- Copies the PHP site's tax setup (ecomm_tax_state, ecomm_tax_zip, ecomm_configuration tax_*, customer group
-- tax_method) into TaxStateRates / TaxZipRates / TaxProviderConfigurations / CustomerGroups.
-- Run after 40. Idempotent: rates are upserted, nothing is deleted.
--
-- HOW TO USE
--   1. On the PHP MySQL database (hk_parts) run the four SELECTs in section A. Each one outputs ready-made
--      T-SQL lines; paste each result above the sample row of the matching block in section B (the sample row
--      stays last: it absorbs the trailing comma and is skipped).
--      (MySQL Workbench: run, right-click the result grid, Copy Field (unquoted) / Copy All Rows.)
--   2. Run this script on the SQL Server database.
--
-- ===================================================================================================
-- A. Run on MySQL (PHP database). The legacy tables are per store: change store_id = 1 if the site used another.
-- ===================================================================================================
--
--   -- A1. State rates
--   SELECT CONCAT('(N''', state_abbr, ''', N''', REPLACE(state_name, '''', ''''''), ''', ',
--                 IFNULL(NULLIF(TRIM(rate), ''), 'NULL'), '),')
--   FROM ecomm_tax_state WHERE store_id = 1 ORDER BY state_abbr;
--
--   -- A2. ZIP rates
--   SELECT CONCAT('(N''', REPLACE(TRIM(zip), '''', ''''''), ''', ', TRIM(rate), '),')
--   FROM ecomm_tax_zip WHERE store_id = 1 AND TRIM(rate) <> '' ORDER BY zip;
--
--   -- A3. Settings (tax method + tax shipping + Avalara options). PHP keeps them in the `settings` table (code/value).
--   SELECT CONCAT('(N''', code, ''', N''', REPLACE(IFNULL(value, ''), '''', ''''''), '''),')
--   FROM settings
--   WHERE store_id = 1 AND code IN ('tax_method', 'tax_shipping_amount', 'avalara_document_committing',
--         'avalara_logging', 'avalara_request_timeout', 'avalara_shipping_code');
--
--   -- A4. Customer group tax methods (matched to the new groups by name)
--   SELECT CONCAT('(N''', REPLACE(name, '''', ''''''), ''', N''', IFNULL(tax_method, ''), '''),')
--   FROM ecomm_customer_group;
--
--   -- A5. Avalara account (NOT imported by this script: type these into Admin, Tax, Avalara tab, then Save,
--   --     which also runs the connection test). The license key is Laravel-encrypted (protect = 1), so it reads as
--   --     ciphertext here. Get the plain key from the Avalara portal, or on the PHP server run:
--   --     php artisan tinker --execute="echo \Edreamz\Ecommcore\Classes\EcommConfiguration::getConfig('avalara_license_key', 1);"
--   SELECT code, value FROM settings
--   WHERE store_id = 1 AND code IN ('avalara_account_number', 'avalara_company_code', 'avalara_service_url');
--
-- The account fields go through the app rather than SQL because the license key is stored encrypted with this
-- app's data-protection keys, which a script cannot produce.
-- ===================================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- ===================================================================================================
-- B1. State rates: paste the A1 output between the markers. Keep the sample row as the last line: it absorbs the
--     trailing comma of the pasted rows and is skipped.
-- ===================================================================================================
DECLARE @States TABLE (StateCode nvarchar(3) NOT NULL, StateName nvarchar(100) NOT NULL, RatePercent decimal(9, 4) NULL);
INSERT INTO @States (StateCode, StateName, RatePercent) VALUES
-- >>> paste A1 here >>>
(N'__', N'', NULL)   -- sample row: keep it last, it is skipped
-- <<< end A1 <<<
;

-- Rows the admin grid has not created yet are added; existing ones get the PHP rate. 0 is stored as blank.
MERGE [TaxStateRates] AS t
USING (
    SELECT UPPER(LTRIM(RTRIM(StateCode))) AS StateCode,
           COALESCE(NULLIF(LTRIM(RTRIM(StateName)), N''), UPPER(LTRIM(RTRIM(StateCode)))) AS StateName,
           NULLIF(RatePercent, 0) AS RatePercent
    FROM @States WHERE StateCode <> N'__'
) AS s
ON t.[CountryCode] = N'US' AND t.[StateCode] = s.StateCode
WHEN MATCHED THEN
    UPDATE SET [RatePercent] = s.RatePercent, [UpdatedOnUtc] = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [CountryCode], [StateCode], [StateName], [RatePercent], [CreatedOnUtc], [UpdatedOnUtc])
    VALUES (NEWID(), N'US', s.StateCode, LEFT(s.StateName, 100), s.RatePercent, SYSUTCDATETIME(), SYSUTCDATETIME());
PRINT CONCAT('State rates: ', @@ROWCOUNT, ' row(s) merged.');
GO

-- ===================================================================================================
-- B2. ZIP rates: paste the A2 output (large lists: split into blocks of at most 1000 rows per INSERT).
-- ===================================================================================================
DECLARE @Zips TABLE (PostalCode nvarchar(20) NOT NULL, RatePercent decimal(9, 4) NOT NULL);
INSERT INTO @Zips (PostalCode, RatePercent) VALUES
-- >>> paste A2 here >>>
(N'__', 0)   -- sample row: keep it last, it is skipped
-- <<< end A2 <<<
;

MERGE [TaxZipRates] AS t
USING (
    SELECT UPPER(LTRIM(RTRIM(PostalCode))) AS PostalCode, MAX(RatePercent) AS RatePercent
    FROM @Zips WHERE PostalCode <> N'__'
    GROUP BY UPPER(LTRIM(RTRIM(PostalCode)))
) AS s
ON t.[PostalCode] = s.PostalCode
WHEN MATCHED AND t.[RatePercent] <> s.RatePercent THEN
    UPDATE SET [RatePercent] = s.RatePercent, [UpdatedOnUtc] = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [PostalCode], [RatePercent], [CreatedOnUtc], [UpdatedOnUtc])
    VALUES (NEWID(), s.PostalCode, s.RatePercent, SYSUTCDATETIME(), SYSUTCDATETIME());
PRINT CONCAT('ZIP rates: ', @@ROWCOUNT, ' row(s) merged.');
GO

-- ===================================================================================================
-- B3. Settings: paste the A3 output.
-- ===================================================================================================
DECLARE @Config TABLE ([Key] nvarchar(100) NOT NULL, [Value] nvarchar(400) NULL);
INSERT INTO @Config ([Key], [Value]) VALUES
-- >>> paste A3 here >>>
(N'__', NULL)   -- sample row: keep it last, it is skipped
-- <<< end A3 <<<
;

IF NOT EXISTS (SELECT 1 FROM [TaxProviderConfigurations])
    INSERT INTO [TaxProviderConfigurations] ([Id], [ActiveProvider], [FlatRatePercent], [IsEnabled], [CacheTtlMinutes],
        [TaxShipping], [AvalaraCommitDocuments], [AvalaraLoggingEnabled], [AvalaraTimeoutSeconds], [AvalaraShippingTaxCode],
        [CreatedOnUtc], [UpdatedOnUtc])
    VALUES (NEWID(), 0, 0, 1, 60, 1, 1, 1, 10, N'FR020100', SYSUTCDATETIME(), SYSUTCDATETIME());

-- PHP tax_method none/state/zip/avalara -> TaxProviderType None=3, ByState=4, ByZip=5, Avalara=6.
DECLARE @Method nvarchar(400) = (SELECT TOP 1 LOWER(LTRIM(RTRIM([Value]))) FROM @Config WHERE [Key] = N'tax_method');
DECLARE @TaxShipping nvarchar(400) = (SELECT TOP 1 LTRIM(RTRIM([Value])) FROM @Config WHERE [Key] = N'tax_shipping_amount');
DECLARE @Commit nvarchar(400) = (SELECT TOP 1 LTRIM(RTRIM([Value])) FROM @Config WHERE [Key] = N'avalara_document_committing');
DECLARE @Logging nvarchar(400) = (SELECT TOP 1 LTRIM(RTRIM([Value])) FROM @Config WHERE [Key] = N'avalara_logging');
DECLARE @Timeout nvarchar(400) = (SELECT TOP 1 LTRIM(RTRIM([Value])) FROM @Config WHERE [Key] = N'avalara_request_timeout');
DECLARE @ShipCode nvarchar(400) = (SELECT TOP 1 LTRIM(RTRIM([Value])) FROM @Config WHERE [Key] = N'avalara_shipping_code');

UPDATE [TaxProviderConfigurations] SET
    [ActiveProvider] = CASE @Method WHEN N'none' THEN 3 WHEN N'state' THEN 4 WHEN N'zip' THEN 5 WHEN N'avalara' THEN 6
                                    ELSE [ActiveProvider] END,
    [TaxShipping] = CASE @TaxShipping WHEN N'1' THEN 1 WHEN N'0' THEN 0 ELSE [TaxShipping] END,
    [AvalaraCommitDocuments] = CASE @Commit WHEN N'1' THEN 1 WHEN N'0' THEN 0 ELSE [AvalaraCommitDocuments] END,
    [AvalaraLoggingEnabled] = CASE @Logging WHEN N'1' THEN 1 WHEN N'0' THEN 0 ELSE [AvalaraLoggingEnabled] END,
    [AvalaraTimeoutSeconds] = CASE WHEN TRY_CONVERT(int, @Timeout) BETWEEN 1 AND 120 THEN TRY_CONVERT(int, @Timeout)
                                   ELSE [AvalaraTimeoutSeconds] END,
    [AvalaraShippingTaxCode] = CASE WHEN NULLIF(@ShipCode, N'') IS NOT NULL THEN LEFT(@ShipCode, 50) ELSE [AvalaraShippingTaxCode] END,
    [UpdatedOnUtc] = SYSUTCDATETIME();
PRINT CONCAT('Tax settings: method = ', ISNULL(@Method, N'(unchanged)'), ', tax shipping = ', ISNULL(@TaxShipping, N'(unchanged)'), '.');
GO

-- ===================================================================================================
-- B4. Customer group tax methods: paste the A4 output. Groups are matched by name; blank = store default.
-- ===================================================================================================
DECLARE @Groups TABLE ([Name] nvarchar(200) NOT NULL, [Method] nvarchar(20) NULL);
INSERT INTO @Groups ([Name], [Method]) VALUES
-- >>> paste A4 here >>>
(N'__', NULL)   -- sample row: keep it last, it is skipped
-- <<< end A4 <<<
;

UPDATE g SET
    g.[TaxMethod] = CASE LOWER(LTRIM(RTRIM(s.[Method])))
                        WHEN N'none' THEN 3 WHEN N'state' THEN 4 WHEN N'zip' THEN 5 WHEN N'avalara' THEN 6 ELSE NULL END,
    g.[UpdatedOnUtc] = SYSUTCDATETIME()
FROM [CustomerGroups] g
JOIN @Groups s ON s.[Name] <> N'__' AND LTRIM(RTRIM(g.[Name])) = LTRIM(RTRIM(s.[Name]));
PRINT CONCAT('Customer groups: ', @@ROWCOUNT, ' tax method(s) set.');
GO
