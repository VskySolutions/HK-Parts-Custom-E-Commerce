using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Evaluates active stores against capability checks (stock, active/maintenance, delivery zone,
/// capacity), selects the nearest eligible store by geo-distance, and supports a fallback chain via
/// <see cref="RoutingRequest.ExcludeStoreIds"/> (Store Management blueprint, REQ-STR-003).
/// Called synchronously by the checkout orchestrator once that feature exists.
/// </summary>
public interface IOrderRoutingEngine
{
    Task<RoutingResult> RouteAsync(RoutingRequest request, CancellationToken cancellationToken = default);
}
