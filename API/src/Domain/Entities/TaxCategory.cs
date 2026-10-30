using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A tax classification a product is assigned to. Every product must reference a Tax Category
/// (Catalog Management contract; AC-CAT-001.6). Full tax-rate resolution is owned by a later
/// Tax feature — this is the minimal lookup the catalog depends on.
/// </summary>
public class TaxCategory : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Optional default rate as a percentage (e.g. 20.0 = 20%). Authoritative rates live in the Tax feature.</summary>
    public decimal? DefaultRatePercent { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
