using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Shipping;

/// <summary>Admin view of the singleton shipping provider configuration (REQ-SHP-005).</summary>
public class ShippingConfigurationDto
{
    public Guid Id { get; set; }
    public bool IsEnabled { get; set; }
    public ShippingSelectionMode SelectionMode { get; set; }
    public int CostVsSpeedWeight { get; set; }
    public int AssumedTransitDays { get; set; }
    public bool PickupEnabled { get; set; }

    /// <summary>How many enabled stores actually offer collection — a platform switch with no store behind
    /// it changes nothing at checkout, so the admin needs to see the count next to the toggle.</summary>
    public int PickupStoreCount { get; set; }

    public List<ShippingCarrierSettingDto> Carriers { get; set; } = new();

    public static ShippingConfigurationDto From(ShippingProviderConfiguration c) => new()
    {
        Id = c.Id,
        IsEnabled = c.IsEnabled,
        SelectionMode = c.SelectionMode,
        CostVsSpeedWeight = c.CostVsSpeedWeight,
        AssumedTransitDays = c.AssumedTransitDays,
        PickupEnabled = c.PickupEnabled,
        Carriers = c.Carriers
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Carrier)
            .Select(ShippingCarrierSettingDto.From)
            .ToList(),
    };
}

/// <summary>One carrier's enablement row, with the credential state the admin needs to act on it.</summary>
public class ShippingCarrierSettingDto
{
    public ShippingCarrierType Carrier { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>
    /// False when this carrier needs credentials and has no active credential row. Manual never requires
    /// credentials. Enabling a carrier without credentials is allowed but yields no rates, so the UI warns.
    /// </summary>
    public bool HasCredentials { get; set; }

    /// <summary>False for <see cref="ShippingCarrierType.Manual"/>, which is evaluated in-process.</summary>
    public bool RequiresCredentials { get; set; }

    public static ShippingCarrierSettingDto From(ShippingCarrierSetting s) => new()
    {
        Carrier = s.Carrier,
        DisplayName = DisplayNameOf(s.Carrier),
        IsEnabled = s.IsEnabled,
        DisplayOrder = s.DisplayOrder,
        RequiresCredentials = s.Carrier != ShippingCarrierType.Manual,
    };

    public static string DisplayNameOf(ShippingCarrierType carrier) => carrier switch
    {
        ShippingCarrierType.Manual => "Manual (custom methods)",
        ShippingCarrierType.FedEx => "FedEx",
        ShippingCarrierType.DHLExpress => "DHL Express",
        ShippingCarrierType.USPS => "USPS",
        ShippingCarrierType.UPS => "UPS",
        _ => carrier.ToString(),
    };
}

/// <summary>One carrier's desired enablement, as submitted by the admin.</summary>
public record ShippingCarrierSettingInput(ShippingCarrierType Carrier, bool IsEnabled, int DisplayOrder);

/// <summary>Shared shaping helpers for the shipping configuration read/write handlers.</summary>
public static class ShippingConfigurationBuilder
{
    /// <summary>
    /// Adds a disabled row for any carrier the enum gained since this configuration was written, so a new
    /// integration shows up in the admin list instead of silently vanishing.
    /// </summary>
    public static void EnsureAllCarriers(ShippingProviderConfiguration config, bool enabledByDefault)
    {
        foreach (var carrier in Enum.GetValues<ShippingCarrierType>())
        {
            if (config.Carriers.All(s => s.Carrier != carrier))
            {
                config.Carriers.Add(new ShippingCarrierSetting
                {
                    Carrier = carrier,
                    IsEnabled = enabledByDefault,
                    DisplayOrder = (int)carrier,
                });
            }
        }
    }

    /// <summary>
    /// Builds the DTO and flags which carriers actually have an active credential row. Enabling a carrier
    /// without credentials yields no rates and is indistinguishable from a disabled one at checkout, so the
    /// admin needs to see this at the point of choosing.
    /// </summary>
    public static async Task<ShippingConfigurationDto> ToDtoAsync(
        IApplicationDbContext db, ShippingProviderConfiguration config, CancellationToken ct)
    {
        var dto = ShippingConfigurationDto.From(config);

        dto.PickupStoreCount = await db.Stores
            .AsNoTracking()
            .CountAsync(s => s.IsEnabled && s.PickupEnabled, ct);

        var configured = new Dictionary<ShippingCarrierType, bool>
        {
            [ShippingCarrierType.FedEx] = await db.FedExCredentials.AsNoTracking().AnyAsync(c => c.Active, ct),
            [ShippingCarrierType.DHLExpress] = await db.DhlExpressCredentials.AsNoTracking().AnyAsync(c => c.Active, ct),
            [ShippingCarrierType.USPS] = await db.UspsCredentials.AsNoTracking().AnyAsync(c => c.Active, ct),
            [ShippingCarrierType.UPS] = await db.UpsCredentials.AsNoTracking().AnyAsync(c => c.Active, ct),
        };

        foreach (var carrier in dto.Carriers)
            carrier.HasCredentials = !carrier.RequiresCredentials || configured.GetValueOrDefault(carrier.Carrier);

        return dto;
    }
}
