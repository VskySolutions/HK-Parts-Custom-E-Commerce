using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A delivery zone for a store, expressed as country + optional region + optional postal-code range
/// (Store Management blueprint, WO-50). A buyer address must fall within an active zone to be eligible.
/// </summary>
public class DeliveryZone : AuditableEntity
{
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty; // ISO 3166-1 alpha-2
    public string? Region { get; set; }                     // state / province
    public string? PostalCodeStart { get; set; }
    public string? PostalCodeEnd { get; set; }
    public bool IsActive { get; set; } = true;

    public Store? Store { get; set; }
}
