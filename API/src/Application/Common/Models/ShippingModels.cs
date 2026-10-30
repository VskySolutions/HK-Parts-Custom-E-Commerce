namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>
/// An origin or destination point for a shipping-rate quote: country/region/postal for zone matching
/// and carrier rating, plus optional geo-coordinates (WO-40).
/// </summary>
/// <param name="City">
/// The town/city. Trails the geo-coordinates because it was added later: carriers that want a city name
/// (DHL) had nothing to read and were being handed <paramref name="Region"/> in its place, which quietly
/// describes Florida as a city. Optional — most carriers rate on country + postal code alone.
/// </param>
public record CarrierAddress(
    string? CountryCode,
    string? Region,
    string? PostalCode,
    double? Latitude,
    double? Longitude,
    string? City = null);

/// <summary>
/// Input to the shipping-rate service: the shipment's origin and destination, the package weight and
/// optional dimensions, and the order subtotal used to evaluate free-shipping and price-based custom
/// methods (WO-40).
/// </summary>
public record CarrierRateRequest(
    CarrierAddress Origin,
    CarrierAddress Destination,
    decimal WeightKg,
    decimal? Length,
    decimal? Width,
    decimal? Height,
    decimal OrderSubtotal);

/// <summary>
/// A single quotable shipping option — either an enabled custom method or a live-carrier product —
/// returned by <see cref="Interfaces.IShippingRateService"/> and the carrier adapters (WO-40).
///
/// <paramref name="EstimatedDeliveryDays"/> is null when the source could not supply an estimate; that
/// means unknown, never instant. <paramref name="IsRecommended"/> is set by
/// <see cref="Interfaces.IShippingOptionSelector"/> under
/// <see cref="Domain.Enums.ShippingSelectionMode.Automatic"/> and is always false under Manual.
/// </summary>
public record ShippingRateOption(
    string MethodId,
    string Name,
    string Carrier,
    int? EstimatedDeliveryDays,
    decimal Rate,
    bool IsRecommended = false);

/// <summary>Input to a carrier label request (WO-42): shipment endpoints, weight, service and a reference.</summary>
public record CarrierLabelRequest(
    CarrierAddress Origin,
    CarrierAddress Destination,
    decimal WeightKg,
    string? ServiceCode,
    string Reference);

/// <summary>Result of a carrier label generation: the tracking number and a downloadable label URL (WO-42).</summary>
public record ShipmentLabelResult(string Carrier, string? TrackingNumber, string? LabelPdfUrl);

/// <summary>A normalized carrier tracking checkpoint (WO-42). <see cref="NormalizedStatus"/> maps onto ShipmentStatus.</summary>
public record CarrierTrackingResult(
    string RawStatus,
    HKPartsEcommerce.Domain.Enums.ShipmentStatus NormalizedStatus,
    string? Description,
    string? Location,
    DateTime CheckpointOnUtc);
