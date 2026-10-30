using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>A quantity of one order line to include in a return request.</summary>
public record RmaLineInput(Guid OrderLineItemId, int Quantity, string? Reason = null);

/// <summary>
/// A registered buyer requests a return for ≥1 item of one of their Delivered orders (AC-ORD-004.1/004.2).
/// Scoped to the caller's own orders; requested items are validated against the order.
/// </summary>
public record SubmitRmaCommand(Guid OrderId, List<RmaLineInput> Lines, string Reason) : IRequest<RmaDto>;

public class SubmitRmaCommandValidator : AbstractValidator<SubmitRmaCommand>
{
    public SubmitRmaCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(i => i.Quantity).GreaterThan(0));
    }
}

public class SubmitRmaCommandHandler : IRequestHandler<SubmitRmaCommand, RmaDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IEmailTemplateSender _templates;
    private readonly IDateTimeProvider _clock;

    public SubmitRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, IEmailTemplateSender templates, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _templates = templates;
        _clock = clock;
    }

    public async Task<RmaDto> Handle(SubmitRmaCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException("Authentication is required to request a return.");

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        // Buyer scoping: not-found masking — a foreign order simply isn't found.
        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.CustomerId == customer.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        if (order.Status != OrderStatus.Delivered)
            throw new ConflictException("Only delivered orders can be returned.");

        var now = _clock.UtcNow;
        var count = await _db.Rmas.IgnoreQueryFilters().CountAsync(r => r.OrderId == order.Id, cancellationToken);
        var rma = new Domain.Entities.Rma
        {
            RmaNumber = $"RMA-{order.OrderNumber}-{count + 1}",
            OrderId = order.Id,
            CustomerId = customer.Id,
            Status = RmaStatus.Requested,
            Reason = request.Reason,
            RequestedOnUtc = now,
        };

        foreach (var input in request.Lines)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == input.OrderLineItemId)
                ?? throw new NotFoundException(nameof(OrderLineItem), input.OrderLineItemId);

            if (input.Quantity > line.Quantity)
                throw new ConflictException(
                    $"Cannot return {input.Quantity} of '{line.ProductName}': the order only contained {line.Quantity}.");

            rma.Lines.Add(new RmaLineItem
            {
                OrderLineItemId = line.Id,
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                Quantity = input.Quantity,
                LineReason = input.Reason,
            });
        }

        _db.Rmas.Add(rma);
        await _db.SaveChangesAsync(cancellationToken);

        await _templates.SendAsync("return.requested", order.ContactEmail ?? string.Empty, order.ContactName,
            new Dictionary<string, string>
            {
                ["customerName"] = string.IsNullOrWhiteSpace(order.ContactName) ? "there" : order.ContactName!,
                ["orderNumber"] = order.OrderNumber,
                ["rmaNumber"] = rma.RmaNumber,
            }, cancellationToken);

        return RmaDto.From(rma);
    }
}
