/* =====================================================================================
   Seed: PHP product attributes -> .NET   (run once on HKPartsPro; safe to re-run)

   Source: PHP DB `hk_parts`, tables ecomm_custom_attribute / ecomm_custom_attribute_choice
   (local copy, read 2026-10-30).

   1. The 4 dropdown attributes (what the PHP category "Filters" tab lists) become filterable
      SpecificationAttributes + options, so they show as checkboxes on the category Filters tab.
      ("Brand" is built in; "Test" exists only on the live PHP site, not in the local copy.)
   2. The 16 text attributes become ProductCustomFields (product General tab ->
      "Custom product information"). WYSIWYG -> TextArea.

   Matching is by SpecificationAttribute.Name / option Value / ProductCustomField.Code, so
   nothing is duplicated and existing rows are left untouched.
   ===================================================================================== */

SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2 = SYSUTCDATETIME();

/* ---- 1. Filterable specification attributes ------------------------------------------- */
DECLARE @spec TABLE (Name nvarchar(200), DisplayOrder int, OptionValue nvarchar(400), OptionOrder int);
INSERT INTO @spec (Name, DisplayOrder, OptionValue, OptionOrder) VALUES
    (N'Allow International Shipping',           1, N'No',            1),
    (N'Allow International Shipping',           1, N'Yes',           2),
    (N'Restrict Non High Capacity Mags States', 2, N'No',            1),
    (N'Restrict Non High Capacity Mags States', 2, N'Yes',           2),
    (N'Shipping Package Size',                  3, N'Small',         1),
    (N'Shipping Package Size',                  3, N'Medium',        2),
    (N'Shipping Package Size',                  3, N'Large',         3),
    (N'Shipping Package Size',                  3, N'Extra Large',   4),
    (N'Shipping Package Size',                  3, N'Free',          5),
    (N'Condition',                              4, N'New',           1),
    (N'Condition',                              4, N'Used',          2),
    (N'Condition',                              4, N'New Old Stock', 3);

INSERT INTO dbo.SpecificationAttributes (Id, Name, IsFilterable, DisplayOrder, Deleted, CreatedOnUtc, UpdatedOnUtc)
SELECT NEWID(), s.Name, 1, s.DisplayOrder, 0, @now, @now
FROM (SELECT DISTINCT Name, DisplayOrder FROM @spec) s
WHERE NOT EXISTS (SELECT 1 FROM dbo.SpecificationAttributes a WHERE a.Name = s.Name AND a.Deleted = 0);

INSERT INTO dbo.SpecificationAttributeOptions (Id, SpecificationAttributeId, Value, DisplayOrder, CreatedOnUtc, UpdatedOnUtc)
SELECT NEWID(), a.Id, s.OptionValue, s.OptionOrder, @now, @now
FROM @spec s
JOIN dbo.SpecificationAttributes a ON a.Name = s.Name AND a.Deleted = 0
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.SpecificationAttributeOptions o
    WHERE o.SpecificationAttributeId = a.Id AND o.Value = s.OptionValue);

/* ---- 2. Free-text product custom fields (FieldType 0 = Text, 1 = TextArea) ------------- */
DECLARE @cf TABLE (Code nvarchar(100), Name nvarchar(200), FieldType int, DisplayOrder int);
INSERT INTO @cf (Code, Name, FieldType, DisplayOrder) VALUES
    (N'manufacturer_model_no', N'Manufacturer Model Number', 0,  1),
    (N'model',                 N'Model',                     0,  2),
    (N'caliber',               N'Caliber',                   0,  3),
    (N'manufacturer',          N'Manufacturer',              0,  4),
    (N'type',                  N'Type',                      0,  5),
    (N'barrel_length',         N'Barrel Length',             0,  6),
    (N'capacity',              N'Capacity',                  0,  7),
    (N'sights',                N'Sights',                    0,  8),
    (N'magazine',              N'Magazine',                  0,  9),
    (N'steel',                 N'Steel',                     0, 10),
    (N'country_of_origin',     N'Country of Origin',         0, 11),
    (N'upc',                   N'UPC',                       0, 12),
    (N'color',                 N'Color',                     0, 13),
    (N'dimensions',            N'Dimensions',                0, 14),
    (N'safety',                N'Safety',                    0, 15),
    (N'product_video',         N'Product Video',             1, 16);

INSERT INTO dbo.ProductCustomFields (Id, Name, Code, FieldType, IsRequired, IsActive, DisplayOrder, Deleted, CreatedOnUtc, UpdatedOnUtc)
SELECT NEWID(), c.Name, c.Code, c.FieldType, 0, 1, c.DisplayOrder, 0, @now, @now
FROM @cf c
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductCustomFields f WHERE f.Code = c.Code AND f.Deleted = 0);

COMMIT TRANSACTION;

/* Check */
SELECT a.Name, a.IsFilterable, COUNT(o.Id) AS Options
FROM dbo.SpecificationAttributes a
LEFT JOIN dbo.SpecificationAttributeOptions o ON o.SpecificationAttributeId = a.Id
WHERE a.Deleted = 0
GROUP BY a.Name, a.IsFilterable, a.DisplayOrder
ORDER BY a.DisplayOrder;

SELECT Code, Name, FieldType FROM dbo.ProductCustomFields WHERE Deleted = 0 ORDER BY DisplayOrder;
