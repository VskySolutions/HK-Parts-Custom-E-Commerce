using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ShippingMethods;

/// <summary>A per-zone rate override for a shipping method (AC-SHP-003.5).</summary>
public class ShippingMethodZoneRateDto
{
    public Guid Id { get; set; }
    public Guid ShippingZoneId { get; set; }
    public string? ShippingZoneName { get; set; }
    public decimal Rate { get; set; }

    public static ShippingMethodZoneRateDto From(ShippingMethodZoneRate r) => new()
    {
        Id = r.Id,
        ShippingZoneId = r.ShippingZoneId,
        ShippingZoneName = r.ShippingZone?.Name,
        Rate = r.Rate,
    };
}

/// <summary>A single per-zone rate override supplied when creating/updating a shipping method.</summary>
public record ShippingMethodZoneRateInput(Guid ShippingZoneId, decimal Rate);

/// <summary>
/// A built-in (non-carrier) shipping method: flat-rate, weight-based, price-based, or free-shipping
/// threshold, with optional per-zone rate overrides (REQ-SHP-003).
/// </summary>
public class ShippingMethodDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ShippingMethodType MethodType { get; set; }
    public decimal? FlatRate { get; set; }
    public decimal? FreeShippingThreshold { get; set; }
    public string? TiersJson { get; set; }
    public bool FlatRatePerItem { get; set; }
    public decimal? FflFlatRate { get; set; }
    public bool InternationalViaUsps { get; set; }
    public int? TransitDays { get; set; }
    public bool IsEnabled { get; set; }
    public int DisplayOrder { get; set; }
    public IReadOnlyList<ShippingMethodZoneRateDto> ZoneRates { get; set; } = new List<ShippingMethodZoneRateDto>();

    public static ShippingMethodDto From(ShippingMethod m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        MethodType = m.MethodType,
        FlatRate = m.FlatRate,
        FreeShippingThreshold = m.FreeShippingThreshold,
        TiersJson = m.TiersJson,
        FlatRatePerItem = m.FlatRatePerItem,
        FflFlatRate = m.FflFlatRate,
        InternationalViaUsps = m.InternationalViaUsps,
        TransitDays = m.TransitDays,
        IsEnabled = m.IsEnabled,
        DisplayOrder = m.DisplayOrder,
        ZoneRates = m.ZoneRates
            .OrderBy(r => r.ShippingZone != null ? r.ShippingZone.Name : string.Empty)
            .Select(ShippingMethodZoneRateDto.From)
            .ToList(),
    };
}

/// <summary>Validation helpers shared by the create/update shipping-method validators.</summary>
public static class ShippingMethodRules
{
    /// <summary>Null/blank, or a JSON array — the rate engine ignores a tier list it cannot parse.</summary>
    public static bool BeValidTierJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return true;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
