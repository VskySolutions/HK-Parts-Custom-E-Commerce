using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Inquiries;

/// <summary>
/// Turns an accepted inquiry into a real, payable order (REQ-INQ-001). Converts <b>in place</b>: the row
/// keeps its id, lines, address, assigned store and full status history, and simply stops being an inquiry —
/// so the trail from "they asked" to "they bought" survives, and every existing order screen picks it up.
///
/// Only meaningful in <see cref="CommerceMode.Standard"/>. An inquiry-only tenant has no online order to
/// convert into: the request is closed out offline, which is the point of that mode.
///
/// Pricing: the snapshot the buyer was quoted is honoured by default. <paramref name="TotalOverride"/> lets
/// the sales team set the agreed figure (the negotiated price is the whole reason the item is quote-only).
/// A full re-run of discounts/shipping/tax is deliberately not attempted here — the source cart is gone, and
/// silently repricing an agreed quote is worse than leaving the number the team actually agreed on.
/// </summary>
public record ConvertInquiryToOrderCommand(Guid Id, decimal? TotalOverride, string? Note) : IRequest<OrderDto>;

public class ConvertInquiryToOrderCommandValidator : AbstractValidator<ConvertInquiryToOrderCommand>
{
    public ConvertInquiryToOrderCommandValidator()
    {
        RuleFor(x => x.TotalOverride).GreaterThanOrEqualTo(0).When(x => x.TotalOverride.HasValue);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public class ConvertInquiryToOrderCommandHandler : IRequestHandler<ConvertInquiryToOrderCommand, OrderDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICommerceModeService _commerce;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;

    public ConvertInquiryToOrderCommandHandler(
        IApplicationDbContext db, ICommerceModeService commerce, ICurrentUserService current, IDateTimeProvider clock)
    {
        _db = db;
        _commerce = commerce;
        _current = current;
        _clock = clock;
    }

    public async Task<OrderDto> Handle(ConvertInquiryToOrderCommand request, CancellationToken cancellationToken)
    {
        var commerce = await _commerce.GetAsync(cancellationToken);
        if (commerce.IsInquiryOnly)
            throw new ConflictException(
                "This store runs in inquiry-only mode, so there is no online order to convert to. " +
                "Close the inquiry out once it has been handled.");

        var inquiry = await _db.Orders
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .Include(o => o.AssignedStore)
            .Include(o => o.StatusHistory)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.IsInquiry, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.Id);

        if (inquiry.AssignedStoreId is null)
            throw new ConflictException(
                "This inquiry is not assigned to a store, so it cannot be fulfilled. Assign a store first.");

        var now = _clock.UtcNow;

        if (request.TotalOverride is decimal agreed)
            inquiry.TotalAmount = agreed;

        inquiry.IsInquiry = false;
        inquiry.InquiryStatus = InquiryStatus.Converted;
        inquiry.Status = OrderStatus.Pending;
        // Awaiting payment, not pending: the money has never been attempted, and the existing retry/collect
        // paths key off this to offer the buyer a way to pay.
        inquiry.PaymentStatus = PaymentStatus.AwaitingPayment;
        inquiry.PlacedOnUtc = now;

        inquiry.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = OrderStatus.Inquiry,
            ToStatus = OrderStatus.Pending,
            ChangedById = _current.UserId,
            ChangedOnUtc = now,
            Note = string.IsNullOrWhiteSpace(request.Note)
                ? "Inquiry converted to an order."
                : $"Inquiry converted to an order. {request.Note.Trim()}",
        });

        await _db.SaveChangesAsync(cancellationToken);
        return OrderDto.From(inquiry);
    }
}
