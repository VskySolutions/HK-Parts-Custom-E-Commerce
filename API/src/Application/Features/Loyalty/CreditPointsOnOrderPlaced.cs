using MediatR;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Checkout;

namespace HKPartsEcommerce.Application.Features.Loyalty;

/// <summary>
/// On order completion, credits the buyer's loyalty points for the order total (WO-27). Only registered
/// customers earn (a guest order carries no CustomerId). Best-effort: a failure is logged but never fails
/// the placed order (which is already paid), matching the other <see cref="OrderPlaced"/> handlers.
/// Inert unless the loyalty program is enabled.
/// </summary>
public class CreditPointsOnOrderPlaced : INotificationHandler<OrderPlaced>
{
    private readonly IRewardPointsService _points;
    private readonly ILogger<CreditPointsOnOrderPlaced> _logger;

    public CreditPointsOnOrderPlaced(IRewardPointsService points, ILogger<CreditPointsOnOrderPlaced> logger)
    {
        _points = points;
        _logger = logger;
    }

    public async Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        if (notification.CustomerId is not Guid customerId)
            return;

        try
        {
            if (!await _points.IsEnabledAsync(cancellationToken))
                return;

            await _points.CreditForOrderAsync(customerId, notification.Total, notification.OrderId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Awarding loyalty points failed for order {OrderId}; the order is unaffected.", notification.OrderId);
        }
    }
}
