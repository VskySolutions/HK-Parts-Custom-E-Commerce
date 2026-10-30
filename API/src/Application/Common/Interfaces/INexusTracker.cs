namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Accumulates US economic-nexus totals from completed orders and raises an admin alert as a state
/// approaches its statutory threshold (REQ-TAX-004). Active only when the configured tax provider is
/// TaxJar (AC-TAX-004.4); otherwise a no-op.
/// </summary>
public interface INexusTracker
{
    /// <summary>Records a completed order against its destination state's nexus totals.</summary>
    Task TrackOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
