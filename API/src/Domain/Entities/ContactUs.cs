using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A merchant-defined free-text product field (e.g. "Caliber", "Warranty notes"). Option-valued,
/// filterable properties belong in <see cref="SpecificationAttribute"/>; this covers the free-text ones.
/// </summary>
public class ContactUs : AuditableEntity, ISoftDeletable
{

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Comments { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

}
