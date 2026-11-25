using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Resolves the configured tax provider and returns a tax breakdown for a request (REQ-TAX-001).
/// Caches identical origin/destination/line results for the configured TTL (AC-TAX-001.5) and applies
/// a flat-rate fallback — raising an admin alert — when the active provider is unavailable
/// (AC-TAX-001.4 / AC-TAX-002.3).
/// </summary>
public interface ITaxCalculationService
{
    /// <summary>
    /// Calculates tax for the request. Never throws for provider failures: on error it returns a
    /// flat-rate <see cref="TaxBreakdown"/> with <see cref="TaxBreakdown.FallbackApplied"/> set.
    /// </summary>
    Task<TaxBreakdown> CalculateAsync(TaxCalculationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports a finalized order to the active tax provider for reporting/remittance (AC-TAX-004.2).
    /// A no-op when the active provider does not perform reporting (e.g. flat-rate).
    /// </summary>
    Task ReportTransactionAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids the order's committed tax document (Avalara) when the order is cancelled. A no-op for every other
    /// provider and for an order with nothing committed.
    /// </summary>
    Task VoidTransactionAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports a refund of an order whose sale was committed to Avalara as a committed ReturnInvoice (PHP
    /// applyCreditAvalara). A no-op when the order has no committed, unvoided document. Never throws: the money has
    /// already moved, so a provider failure is logged and raised as an admin alert instead.
    /// </summary>
    Task ReturnTransactionAsync(TaxReturnRequest request, CancellationToken cancellationToken = default);
}
