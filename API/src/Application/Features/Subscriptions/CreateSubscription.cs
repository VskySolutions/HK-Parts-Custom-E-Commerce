using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Subscriptions;

/// <summary>
/// The authenticated buyer subscribes to a product at a chosen recurrence interval (AC-ORD-005.2). The
/// subscription is created for the caller's own customer profile; an optional saved shipping address must
/// belong to that customer.
/// </summary>
public record CreateSubscriptionCommand(
    Guid ProductId,
    Guid? ProductVariantId,
    SubscriptionInterval Interval,
    int Quantity = 1,
    Guid? ShippingAddressId = null) : IRequest<SubscriptionDto>;

public class CreateSubscriptionCommandValidator : AbstractValidator<CreateSubscriptionCommand>
{
    public CreateSubscriptionCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Interval).IsInEnum();
    }
}

public class CreateSubscriptionCommandHandler : IRequestHandler<CreateSubscriptionCommand, SubscriptionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptions;
    private readonly ICurrentUserService _current;

    public CreateSubscriptionCommandHandler(
        IApplicationDbContext db, ISubscriptionService subscriptions, ICurrentUserService current)
    {
        _db = db;
        _subscriptions = subscriptions;
        _current = current;
    }

    public async Task<SubscriptionDto> Handle(CreateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException();

        var customerId = await _db.Customers
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        // A provided shipping address must be one of the caller's own saved addresses.
        if (request.ShippingAddressId is Guid addressId)
        {
            var ownsAddress = await _db.CustomerAddresses
                .AsNoTracking()
                .AnyAsync(ca => ca.CustomerId == customerId && ca.AddressId == addressId, cancellationToken);
            if (!ownsAddress)
                throw new ForbiddenAccessException("The selected address does not belong to the current customer.");
        }

        var id = await _subscriptions.CreateAsync(
            customerId,
            request.ProductId,
            request.ProductVariantId,
            request.Quantity,
            request.Interval,
            request.ShippingAddressId,
            cancellationToken);

        var subscription = await _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Subscription), id);

        return SubscriptionDto.From(subscription);
    }
}
