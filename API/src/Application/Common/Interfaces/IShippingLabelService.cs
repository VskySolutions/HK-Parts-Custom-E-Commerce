namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Generates a carrier shipping label for a shipment and records the tracking number + label URL on the
/// shipment and its order (REQ-SHP-002). Resolves the carrier adapter by the shipment's carrier name.
/// </summary>
public interface IShippingLabelService
{
    /// <summary>Generates and stores a label for the shipment. Throws if the carrier is unconfigured/unsupported.</summary>
    Task GenerateLabelAsync(Guid shipmentId, CancellationToken cancellationToken = default);
}
