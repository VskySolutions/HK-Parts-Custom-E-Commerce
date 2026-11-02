/* =====================================================================================
   Hide the "Product Video" custom field from the product General tab   (safe to re-run)

   PHP-parity review (product General tab): "Need to remove Product Video from General Tab, it's
   present in Media tab". The field came over from PHP as a ProductCustomField (code product_video,
   see 2026-10-30_Seed_PhpProductAttributes.sql). Videos belong in the product Media tab (embed URLs).

   The admin product page lists only ACTIVE custom fields, so deactivating the field removes it from
   the General tab. Existing values are KEPT (ProductCustomFieldValues untouched) so nothing is lost;
   to bring the field back, set IsActive = 1 again (or toggle it under Catalog -> Custom fields).
   ===================================================================================== */

SET NOCOUNT ON;

UPDATE dbo.ProductCustomFields
SET    IsActive = 0,
       UpdatedOnUtc = SYSUTCDATETIME()
WHERE  Code = N'product_video'
  AND  IsActive = 1;

SELECT Code, Name, IsActive,
       (SELECT COUNT(*) FROM dbo.ProductCustomFieldValues v
        WHERE v.ProductCustomFieldId = f.Id AND v.Value IS NOT NULL AND v.Value <> N'') AS ProductsWithValue
FROM   dbo.ProductCustomFields f
WHERE  Code = N'product_video';
GO
