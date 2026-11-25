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

    /// <summary>
    /// Voids the order's committed tax document when the order is cancelled (PHP Avalara voidTax). Only Avalara
    /// commits documents; every other provider is a no-op.
    /// </summary>
    Task VoidTransactionAsync(Guid orderId, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Records a refund against the order's committed document (Avalara ReturnInvoice). Only Avalara commits
    /// documents; every other provider is a no-op. Throws on failure.
    /// </summary>
    Task ReturnTransactionAsync(TaxReturnRequest request, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Avalara's Test Connection (PHP Avalara Settings page, pingTest): checks the active credential authenticates.</summary>
public interface IAvalaraTaxConnection
{
    Task<TaxConnectionTestResult> TestConnectionAsync(CancellationToken ct);
}
