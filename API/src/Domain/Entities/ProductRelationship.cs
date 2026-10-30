using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A directional association from one product to another, classified as Related, Cross-Sell or
/// Up-Sell (REQ-CAT-007).
/// </summary>
public class ProductRelationship : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid RelatedProductId { get; set; }
    public Product? RelatedProduct { get; set; }

    public ProductRelationshipType RelationshipType { get; set; }
    public int DisplayOrder { get; set; }
}
