using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>UPS admin Test button (UP-40, UP-41).</summary>
public interface IUpsRater
{
    /// <summary>
    /// Rates a sample 5 lb residential shipment 28270 → 10001 with one stored credential, uncached, and
    /// reports the token status (valid until, or the error).
    /// </summary>
    Task<CarrierTestResult> TestCredentialAsync(Guid credentialId, CancellationToken ct);
}
