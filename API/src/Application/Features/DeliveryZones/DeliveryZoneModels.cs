using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.DeliveryZones;

/// <summary>A delivery zone for a store: country + optional region + optional postal-code range.</summary>
public class DeliveryZoneDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? PostalCodeStart { get; set; }
    public string? PostalCodeEnd { get; set; }
    public bool IsActive { get; set; }

    public static DeliveryZoneDto From(DeliveryZone z) => new()
    {
        Id = z.Id,
        StoreId = z.StoreId,
        Name = z.Name,
        CountryCode = z.CountryCode,
        Region = z.Region,
        PostalCodeStart = z.PostalCodeStart,
        PostalCodeEnd = z.PostalCodeEnd,
        IsActive = z.IsActive,
    };
}
