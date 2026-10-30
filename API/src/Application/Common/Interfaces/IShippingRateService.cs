using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Aggregates the shipping options available for a shipment: enabled custom shipping methods
/// (flat / weight / price / free, honouring zone eligibility) plus every configured live carrier,
/// silently excluding any carrier that fails or is unconfigured (WO-40).
/// </summary>
public interface IShippingRateService
{
    Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct);
}
