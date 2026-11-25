using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// Guest checkout A-22: fix a typo in a guest order's email, optionally resending the order confirmation to the
/// corrected address. The order moves to the guest record of the new email, and the change is written to the
/// order's history with the admin and time (N-12). Only guest orders: an account's email is edited on the account.
/// </summary>
public record UpdateGuestOrderEmailCommand(Guid Id, string Email, bool ResendConfirmation = false) : IRequest<OrderDto>;

public class UpdateGuestOrderEmailCommandValidator : AbstractValidator<UpdateGuestOrderEmailCommand>
{
    public UpdateGuestOrderEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(254)
            .Must(GuestEmail.IsValid).WithMessage("Enter a valid email address.");
    }
}

public class UpdateGuestOrderEmailCommandHandler : IRequestHandler<UpdateGuestOrderEmailCommand, OrderDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailTemplateSender _templates;

    public UpdateGuestOrderEmailCommandHandler(
        IApplicationDbContext db, ICurrentUserService current, IDateTimeProvider clock, IEmailTemplateSender templates)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _templates = templates;
    }

    public async Task<OrderDto> Handle(UpdateGuestOrderEmailCommand request, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .Include(o => o.BillingAddress)
            .FirstOrDefaultAsync(o => o.Id == request.Id, ct)
            ?? throw new NotFoundException(nameof(Order), request.Id);
        if (order.CustomerId is not null || order.EbayOrderId is not null || order.IsInquiry)
            throw new ConflictException("Only a guest order's email can be changed here. Edit a registered customer's email on their account.");
        if (order.ShippingAddress is null)
            throw new ConflictException("This order has no contact address to update.");

        var now = _clock.UtcNow;
        var email = Guest.NormalizeEmail(request.Email);
        var previous = order.ShippingAddress.Email;
        if (!string.Equals(Guest.NormalizeEmail(previous), email, StringComparison.Ordinal))
        {
            order.ShippingAddress.Email = email;
            if (order.BillingAddress is not null && !string.IsNullOrWhiteSpace(order.BillingAddress.Email))
                order.BillingAddress.Email = email;

            // The order now belongs to the guest of the corrected email (edge case "Admin fixes a typo").
            var ship = order.ShippingAddress;
            var guest = await GuestRecords.UpsertAsync(_db, email, ship.FirstName, ship.LastName, ship.PhoneNumber,
                marketingOptIn: false, order.PlacedOnUtc, ct);
            order.Guest = guest;

            order.StatusHistory.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = order.Status,
                ToStatus = order.Status,
                ChangedById = _current.UserId,
                ChangedOnUtc = now,
                Note = $"Guest email changed from {previous} to {email}.",
            });
        }

        if (request.ResendConfirmation)
        {
            var name = $"{order.ShippingAddress.FirstName} {order.ShippingAddress.LastName}".Trim();
            await _templates.SendAsync(
                "order.confirmation",
                email,
                string.IsNullOrWhiteSpace(name) ? null : name,
                new Dictionary<string, string>
                {
                    ["customerName"] = string.IsNullOrWhiteSpace(name) ? "there" : name,
                    ["orderNumber"] = order.OrderNumber,
                    ["orderDate"] = order.PlacedOnUtc.ToString("MMM d, yyyy"),
                    ["orderTotal"] = $"{order.CurrencyCode} {order.TotalAmount:0.00}",
                },
                ct);
            order.StatusHistory.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = order.Status,
                ToStatus = order.Status,
                ChangedById = _current.UserId,
                ChangedOnUtc = now,
                Note = $"Order confirmation resent to {email}.",
            });
        }

        await _db.SaveChangesAsync(ct);
        return OrderDto.From(order);
    }
}
