using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>FedEx admin Test button (FX-40, FX-T11).</summary>
public interface IFedExRater
{
    /// <summary>
    /// Rates a sample 5 lb residential shipment 28270 → 10001 with one stored credential, uncached, and
    /// reports the token status (valid until, or the FedEx error).
    /// </summary>
    Task<CarrierTestResult> TestCredentialAsync(Guid credentialId, CancellationToken ct);
}
