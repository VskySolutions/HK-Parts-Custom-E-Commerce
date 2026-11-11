using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// USPS-specific rating beyond <see cref="ICarrierClient"/>: the international quote the legacy "Custom"
/// method charges (US-20..US-24) and the admin Test button (US-40).
/// </summary>
public interface IUspsRater
{
    /// <summary>
    /// International rates for the allowed international services, without the USPS method's own handling
    /// fee, minimum order or ship-to filters — the Custom method applies its own rules. Empty on failure.
    /// </summary>
    Task<IReadOnlyList<ShippingRateOption>> GetInternationalRatesAsync(CarrierRateRequest request, CancellationToken ct);

    /// <summary>Rates a sample domestic and international shipment with one stored credential, uncached.</summary>
    Task<CarrierTestResult> TestCredentialAsync(Guid credentialId, CancellationToken ct);
}
