using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Decides which of the offered shipping options to recommend (REQ-SHP-006).
///
/// Under <see cref="Domain.Enums.ShippingSelectionMode.Manual"/> nothing is recommended and the customer
/// chooses unaided. Under <see cref="Domain.Enums.ShippingSelectionMode.Automatic"/> the best-value option
/// is scored on cost and delivery time and flagged as the default — the customer can still override it.
/// </summary>
public interface IShippingOptionSelector
{
    /// <summary>
    /// Returns the offered options with <see cref="ShippingRateOption.IsRecommended"/> set on the winner (if
    /// any), plus that winner. An empty input yields an empty result and a null recommendation.
    /// </summary>
    Task<ShippingSelectionResult> SelectAsync(IReadOnlyList<ShippingRateOption> options, CancellationToken ct);
}

/// <summary>The offered options (winner flagged) and the recommended option, null under Manual.</summary>
public record ShippingSelectionResult(IReadOnlyList<ShippingRateOption> Options, ShippingRateOption? Recommended);
