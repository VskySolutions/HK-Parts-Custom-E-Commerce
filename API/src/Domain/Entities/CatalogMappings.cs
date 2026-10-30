using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>Join between <see cref="Product"/> and <see cref="Category"/>; a product may belong to
/// multiple categories, with a per-category display order (AC-CAT-004.2/4.5).</summary>
public class ProductCategory : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Assigns a <see cref="ProductAttribute"/> to a product for variant generation (AC-CAT-002.1).</summary>
public class ProductAttributeMapping : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid ProductAttributeId { get; set; }
    public ProductAttribute? ProductAttribute { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>One attribute-value selection that (together with its siblings) defines a variant combination.</summary>
public class ProductVariantAttributeValue : BaseEntity
{
    public Guid ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public Guid ProductAttributeValueId { get; set; }
    public ProductAttributeValue? ProductAttributeValue { get; set; }
}

/// <summary>A specification value assigned to a product (feeds product detail + faceted nav; REQ-CAT-003).</summary>
public class ProductSpecificationValue : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid SpecificationAttributeOptionId { get; set; }
    public SpecificationAttributeOption? SpecificationAttributeOption { get; set; }
}

/// <summary>Membership of a Simple product within a <see cref="Domain.Enums.ProductType.Grouped"/> product (AC-CAT-001.3).</summary>
public class GroupedProductMember : BaseEntity
{
    public Guid GroupedProductId { get; set; }
    public Product? GroupedProduct { get; set; }
    public Guid MemberProductId { get; set; }
    public Product? MemberProduct { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Lists a product in a <see cref="Store"/>. A product with no rows is listed in every store.</summary>
public class ProductStoreMapping : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }
}

/// <summary>A <see cref="SpecificationAttribute"/> offered as a facet on a category's product listing.</summary>
public class CategoryFilterAttribute : BaseEntity
{
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public Guid SpecificationAttributeId { get; set; }
    public SpecificationAttribute? SpecificationAttribute { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>A downloadable file or URL attached to a Downloadable product (AC-CAT-001.4).</summary>
public class ProductDownload : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>External download URL, or null when <see cref="FileKey"/> references stored content.</summary>
    public string? Url { get; set; }

    /// <summary>File-storage key when the download is an uploaded file rather than a URL.</summary>
    public string? FileKey { get; set; }
    public int DisplayOrder { get; set; }
}
