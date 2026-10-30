using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>
/// Prices a cart for a delivery address without placing an order (REQ-CHK-003). A thin MediatR wrapper
/// that delegates to the <see cref="ICheckoutOrchestrator"/>, so the controller stays free of the
/// orchestration dependency.
/// </summary>
public record QuoteCheckoutQuery(CheckoutQuoteRequest Request) : IRequest<CheckoutQuote>;

public class QuoteCheckoutQueryHandler : IRequestHandler<QuoteCheckoutQuery, CheckoutQuote>
{
    private readonly ICheckoutOrchestrator _orchestrator;

    public QuoteCheckoutQueryHandler(ICheckoutOrchestrator orchestrator) => _orchestrator = orchestrator;

    public Task<CheckoutQuote> Handle(QuoteCheckoutQuery request, CancellationToken cancellationToken)
        => _orchestrator.QuoteAsync(request.Request, cancellationToken);
}
