using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// A pluggable external tax provider (REQ-TAX-002). Each implementation advertises the
/// <see cref="Provider"/> it serves so <see cref="ITaxCalculationService"/> can resolve the one that
/// matches the active configuration. Implementations MUST throw on any failure (network error,
/// non-success response, or missing credentials) so the calculation service applies its flat-rate
/// fallback (AC-TAX-001.4 / AC-TAX-002.3).
/// </summary>
public interface ITaxProviderClient
{
    /// <summary>The provider this client serves.</summary>
    TaxProviderType Provider { get; }

    /// <summary>
    /// Calculates tax for the request against the live provider. Throws on any failure so the caller
    /// can fall back to the flat rate.
    /// </summary>
    Task<TaxBreakdown> CalculateAsync(TaxCalculationRequest req, CancellationToken ct);

    /// <summary>
    /// Reports a finalized order to the provider for tax reporting/remittance. Only TaxJar performs a
    /// real call; other providers are a no-op.
    /// </summary>
    Task ReportTransactionAsync(Guid orderId, CancellationToken ct);
}
