using MediatR;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Checkout;

namespace HKPartsEcommerce.Application.Features.Tax;

/// <summary>
/// On order completion, reports the order to the active tax provider for reporting/remittance
/// (AC-TAX-004.2) — e.g. records a Stripe Tax transaction from the calculation captured at placement.
/// Best-effort: a reporting failure is logged but never fails the placed order (which is already paid).
/// </summary>
public class ReportOrderTaxTransactionHandler : INotificationHandler<OrderPlaced>
{
    private readonly ITaxCalculationService _tax;
    private readonly ILogger<ReportOrderTaxTransactionHandler> _logger;

    public ReportOrderTaxTransactionHandler(ITaxCalculationService tax, ILogger<ReportOrderTaxTransactionHandler> logger)
    {
        _tax = tax;
        _logger = logger;
    }

    public async Task Handle(OrderPlaced notification, CancellationToken cancellationToken)
    {
        try
        {
            await _tax.ReportTransactionAsync(notification.OrderId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tax transaction reporting failed for order {OrderId}; the order is unaffected.", notification.OrderId);
        }
    }
}
