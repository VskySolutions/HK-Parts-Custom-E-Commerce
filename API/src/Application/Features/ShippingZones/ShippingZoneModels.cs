using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ShippingZones;

/// <summary>A geographic shipping zone: country + optional region + optional postal-code range (AC-SHP-003.5).</summary>
public class ShippingZoneDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? PostalCodeStart { get; set; }
    public string? PostalCodeEnd { get; set; }
    public bool IsEnabled { get; set; }

    public static ShippingZoneDto From(ShippingZone z) => new()
    {
        Id = z.Id,
        Name = z.Name,
        CountryCode = z.CountryCode,
        Region = z.Region,
        PostalCodeStart = z.PostalCodeStart,
        PostalCodeEnd = z.PostalCodeEnd,
        IsEnabled = z.IsEnabled,
    };
}
