using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Shipments;

/// <summary>A quantity of one order line to include in a shipment.</summary>
public record ShipmentLineInput(Guid OrderLineItemId, int Quantity);

/// <summary>
/// Creates a shipment for a subset of an order's lines (AC-ORD-002.1). Increments each line's shipped
/// quantity; the order only advances to Shipped once every line is fully shipped, otherwise it moves to
/// Processing and remains partially fulfilled (AC-ORD-002.2). The buyer is notified.
/// </summary>
public record CreateShipmentCommand(
    Guid OrderId,
    List<ShipmentLineInput> Lines,
    string Carrier,
    string? TrackingNumber = null,
    string? ServiceName = null) : IRequest<ShipmentDto>;

public class CreateShipmentCommandValidator : AbstractValidator<CreateShipmentCommand>
{
    public CreateShipmentCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Carrier).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(i => i.Quantity).GreaterThan(0));
    }
}

public class CreateShipmentCommandHandler : IRequestHandler<CreateShipmentCommand, ShipmentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IEmailEnqueuer _emails;
    private readonly IDateTimeProvider _clock;

    public CreateShipmentCommandHandler(IApplicationDbContext db, ICurrentUserService current, IEmailEnqueuer emails, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _emails = emails;
        _clock = clock;
    }

    public async Task<ShipmentDto> Handle(CreateShipmentCommand request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        // An inquiry was never a sale — nothing was paid for and nothing is owed to the buyer, so it
        // cannot be fulfilled until it is converted into a real order (REQ-INQ-001).
        if (order.IsInquiry)
            throw new ConflictException("An inquiry cannot be shipped. Convert it to an order first.");

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Delivered)
            throw new ConflictException($"An order in status '{order.Status}' cannot be shipped.");

        // Merge duplicate line inputs, then validate each against the remaining (unshipped) quantity.
        var requested = request.Lines
            .GroupBy(l => l.OrderLineItemId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var now = _clock.UtcNow;
        var shipmentCount = await _db.Shipments.CountAsync(s => s.OrderId == order.Id, cancellationToken);
        var shipment = new Shipment
        {
            OrderId = order.Id,
            ShipmentNumber = $"{order.OrderNumber}-S{shipmentCount + 1}",
            Carrier = request.Carrier,
            ServiceName = request.ServiceName,
            TrackingNumber = request.TrackingNumber,
            Status = ShipmentStatus.Shipped,
            ShippedOnUtc = now,
        };

        foreach (var (lineId, qty) in requested)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == lineId)
                ?? throw new NotFoundException(nameof(OrderLineItem), lineId);

            var remaining = line.Quantity - line.QuantityShipped;
            if (qty > remaining)
                throw new ConflictException(
                    $"Cannot ship {qty} of '{line.ProductName}': only {remaining} unit(s) remain unshipped.");

            line.QuantityShipped += qty;
            shipment.Lines.Add(new ShipmentLineItem
            {
                OrderLineItemId = line.Id,
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                Quantity = qty,
            });
        }

        _db.Shipments.Add(shipment);

        // Advance the order: fully shipped → Shipped; otherwise begin fulfilment (Processing).
        var fullyShipped = order.Lines.All(l => l.QuantityShipped >= l.Quantity);
        var from = order.Status;
        OrderStatus? to = null;
        if (fullyShipped && order.Status is OrderStatus.Pending or OrderStatus.Processing)
            to = OrderStatus.Shipped;
        else if (!fullyShipped && order.Status == OrderStatus.Pending)
            to = OrderStatus.Processing;

        if (to is OrderStatus target)
        {
            order.Status = target;
            if (target == OrderStatus.Shipped)
            {
                order.ShippedOnUtc = now;
                order.TrackingNumber ??= request.TrackingNumber;
                order.ShippingCarrier ??= request.Carrier;
            }

            _db.OrderStatusHistory.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = from,
                ToStatus = target,
                ChangedById = _current.UserId,
                ChangedOnUtc = now,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(order.ContactEmail))
        {
            await _emails.EnqueueAsync(
                "OrderShipped",
                order.ContactEmail!,
                order.ContactName,
                $"A shipment for your order {order.OrderNumber} is on its way",
                $"Hi {order.ContactName},\n\n" +
                $"Shipment {shipment.ShipmentNumber} for order {order.OrderNumber} has been dispatched.\n" +
                (string.IsNullOrWhiteSpace(request.Carrier) ? string.Empty : $"Carrier: {request.Carrier}\n") +
                (string.IsNullOrWhiteSpace(request.TrackingNumber) ? string.Empty : $"Tracking number: {request.TrackingNumber}\n") +
                "\nThank you for shopping with us.",
                cancellationToken: cancellationToken);
        }

        var saved = await _db.Shipments
            .AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.TrackingEvents)
            .FirstAsync(s => s.Id == shipment.Id, cancellationToken);
        return ShipmentDto.From(saved);
    }
}
