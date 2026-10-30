using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Subscriptions;

/// <summary>The authenticated buyer cancels one of their own subscriptions (AC-ORD-005.4). Terminal.</summary>
public record CancelSubscriptionCommand(Guid Id) : IRequest<SubscriptionDto>;

public class CancelSubscriptionCommandHandler : IRequestHandler<CancelSubscriptionCommand, SubscriptionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptions;
    private readonly ICurrentUserService _current;

    public CancelSubscriptionCommandHandler(
        IApplicationDbContext db, ISubscriptionService subscriptions, ICurrentUserService current)
    {
        _db = db;
        _subscriptions = subscriptions;
        _current = current;
    }

    public async Task<SubscriptionDto> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        await SubscriptionOwnership.EnsureOwnedAsync(_db, _current, request.Id, cancellationToken);

        await _subscriptions.CancelAsync(request.Id, cancellationToken);

        return await SubscriptionOwnership.LoadDtoAsync(_db, request.Id, cancellationToken);
    }
}
