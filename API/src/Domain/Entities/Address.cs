using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A centrally-managed postal address (mirrors the Media-table pattern): every feature that needs an
/// address references an <see cref="Address"/> row — via a single <c>AddressId</c> FK (Order, Store) or
/// a mapping table (<see cref="CustomerAddress"/> for the customer address book) — rather than storing
/// address columns of its own. Name/company/phone/email are carried here too so a row is self-contained.
/// </summary>
public class Address : AuditableEntity, ISoftDeletable
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Company { get; set; }

    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }

    /// <summary>Optional nearby landmark to aid delivery (e.g. "opposite City Mall").</summary>
    public string? Landmark { get; set; }

    public string City { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string CountryCode { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    /// <summary>Optional geo-coordinates, used to improve nearest-store routing / carrier rating.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
}
