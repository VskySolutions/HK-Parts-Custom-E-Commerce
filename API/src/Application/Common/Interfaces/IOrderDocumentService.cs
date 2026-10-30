namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Renders order documents (WO-47) as PDF byte streams. Financial values — including the tax
/// breakdown — are read from the order exactly as persisted at placement time and are never
/// recalculated (AC-ORD-003.4).
/// </summary>
public interface IOrderDocumentService
{
    /// <summary>Render a customer invoice: itemised lines, monetary totals and the persisted tax breakdown.</summary>
    Task<byte[]> GenerateInvoiceAsync(Guid orderId, CancellationToken ct);

    /// <summary>Render a warehouse packing slip: items and quantities only, with no prices.</summary>
    Task<byte[]> GeneratePackingSlipAsync(Guid orderId, CancellationToken ct);
}
