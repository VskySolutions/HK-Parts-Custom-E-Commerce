using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// A live-carrier rating adapter (e.g. DHL Express, UPS). Implementations call the carrier's REST
/// rating API and map the response to <see cref="ShippingRateOption"/>s (WO-40). A carrier that is
/// unconfigured or that fails is expected to return an empty list rather than throw, so the aggregating
/// service can simply exclude it (AC-SHP-001.3).
/// </summary>
public interface ICarrierClient
{
    /// <summary>Human-readable carrier name (e.g. "DHL Express", "UPS").</summary>
    string CarrierName { get; }

    /// <summary>
    /// Typed discriminator used to match this client against its
    /// <see cref="Domain.Entities.ShippingCarrierSetting"/> row, so a carrier the admin has disabled is
    /// never queried.
    /// </summary>
    ShippingCarrierType Carrier { get; }

    /// <summary>Requests live rate options for a shipment; returns an empty list when unavailable.</summary>
    Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct);

    /// <summary>
    /// Generates a shipping label + tracking number (WO-42, AC-SHP-002.1). Returns null when the carrier is
    /// unconfigured or does not support label generation, so the caller can degrade gracefully. Default:
    /// unsupported.
    /// </summary>
    Task<ShipmentLabelResult?> GenerateLabelAsync(CarrierLabelRequest request, CancellationToken ct)
        => Task.FromResult<ShipmentLabelResult?>(null);

    /// <summary>
    /// Fetches the latest tracking checkpoint for a tracking number (WO-42, AC-SHP-002.3). Returns null when
    /// unavailable. Default: unsupported.
    /// </summary>
    Task<CarrierTrackingResult?> GetTrackingStatusAsync(string trackingNumber, CancellationToken ct)
        => Task.FromResult<CarrierTrackingResult?>(null);
}
