namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>A single line item to be fulfilled, for routing stock-availability checks.</summary>
public record RoutingLineItem(Guid ProductId, Guid? ProductVariantId, int Quantity);

/// <summary>
/// Input to the order routing engine: the buyer's delivery address (geo + postal) and the order's
/// line items. <see cref="ExcludeStoreIds"/> carries stores already tried in a fallback chain.
/// </summary>
public record RoutingRequest(
    double? Latitude,
    double? Longitude,
    string? CountryCode,
    string? Region,
    string? PostalCode,
    IReadOnlyList<RoutingLineItem> Items,
    IReadOnlyCollection<Guid>? ExcludeStoreIds = null);

/// <summary>The evaluation outcome for one candidate store (part of the routing chain).</summary>
public record StoreEvaluation(
    Guid StoreId,
    string StoreName,
    bool Eligible,
    string? IneligibleReason,
    double? DistanceKm);

/// <summary>
/// Result of a routing evaluation: the selected store (nearest eligible) and its address, plus the
/// full ordered chain of evaluated stores for transparency and fallback (Store Management contract).
/// </summary>
public record RoutingResult(
    bool IsRouted,
    Guid? AssignedStoreId,
    string? StoreAddress,
    IReadOnlyList<StoreEvaluation> RoutingChain);
