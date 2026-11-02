namespace HKPartsEcommerce.Domain.Enums;

/// <summary>
/// The kind of record a <see cref="Entities.SeoMetadata"/> row describes. Together with the record id it
/// forms the key of the central SEO table, so any module can carry SEO without its own columns.
/// Values are persisted as ints — append new members, never renumber.
/// </summary>
public enum SeoEntityType
{
    Product = 1,
    Category = 2,
    Manufacturer = 3,
    CmsPage = 4,
    BlogPost = 5,
    ProductTag = 6,
    ProductCollection = 7,
}
