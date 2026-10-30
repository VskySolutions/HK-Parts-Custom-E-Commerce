using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Shipping;

/// <summary>
/// Generates a carrier label for a shipment (REQ-SHP-002): resolves the carrier adapter by name, requests
/// a label (store → ship-to), and records the tracking number + label URL on the shipment and its order.
/// </summary>
public class ShippingLabelService : IShippingLabelService
{
    private const decimal DefaultWeightKg = 1.0m;

    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<ICarrierClient> _carriers;
    private readonly IDateTimeProvider _clock;

    public ShippingLabelService(IApplicationDbContext db, IEnumerable<ICarrierClient> carriers, IDateTimeProvider clock)
    {
        _db = db;
        _carriers = carriers;
        _clock = clock;
    }

    public async Task GenerateLabelAsync(Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments
            .FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Shipment), shipmentId);

        if (string.IsNullOrWhiteSpace(shipment.Carrier))
            throw new ConflictException("The shipment has no carrier assigned.");

        var carrier = _carriers.FirstOrDefault(c => c.CarrierName.Equals(shipment.Carrier, StringComparison.OrdinalIgnoreCase))
            ?? throw new ConflictException($"No carrier adapter is registered for '{shipment.Carrier}'.");

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == shipment.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), shipment.OrderId);

        var store = order.AssignedStoreId is Guid storeId
            ? await _db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId, cancellationToken)
            : null;

        var origin = new CarrierAddress(store?.CountryCode, store?.StateProvince, store?.PostalCode, null, null);
        var destination = new CarrierAddress(order.CountryCode, order.Region, order.PostalCode, null, null);
        var request = new CarrierLabelRequest(origin, destination, DefaultWeightKg, shipment.ServiceName, shipment.ShipmentNumber);

        var label = await carrier.GenerateLabelAsync(request, cancellationToken)
            ?? throw new ConflictException(
                $"The carrier '{shipment.Carrier}' did not return a label (it may be unconfigured or not support label generation).");

        var now = _clock.UtcNow;
        shipment.TrackingNumber = label.TrackingNumber ?? shipment.TrackingNumber;
        shipment.LabelPdfUrl = label.LabelPdfUrl;
        shipment.Status = ShipmentStatus.LabelGenerated;
        shipment.LabelGeneratedOnUtc = now;

        // Mirror the tracking number onto the order for buyer visibility (AC-SHP-002.2).
        if (!string.IsNullOrWhiteSpace(shipment.TrackingNumber))
            order.TrackingNumber = shipment.TrackingNumber;
        order.ShippingCarrier ??= shipment.Carrier;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
