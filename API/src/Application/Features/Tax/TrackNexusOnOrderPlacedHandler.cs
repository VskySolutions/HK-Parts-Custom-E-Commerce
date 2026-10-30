using MediatR;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Checkout;

namespace HKPartsEcommerce.Application.Features.Tax;

/// <summary>
/// On order completion, accumulates the order into US economic-nexus totals (AC-TAX-004.4). Best-effort:
/// a tracking failure is logged but never fails the placed order. Inert unless TaxJar is the active provider.
/// </summary>
public class TrackNexusOnOrderPlacedHandler : INotificationHandler<OrderPlaced>
{
    private readonly INexusTracker _nexus;
    private readonly ILogger<TrackNexusOnOrderPlacedHandler> _logger;

    public TrackNexusOnOrderPlacedHandler(INexusTracker nexus, ILogger<TrackNexusOnOrderPlacedHandler> logger)
    {
        _nexus = nexus;
        _logger = logger;
    }

    public async Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        try
        {
            await _nexus.TrackOrderAsync(notification.OrderId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nexus tracking failed for order {OrderId}; the order is unaffected.", notification.OrderId);
        }
    }
}
