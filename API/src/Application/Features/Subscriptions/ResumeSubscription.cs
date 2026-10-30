using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Subscriptions;

/// <summary>The authenticated buyer resumes one of their own paused subscriptions (AC-ORD-005.4).</summary>
public record ResumeSubscriptionCommand(Guid Id) : IRequest<SubscriptionDto>;

public class ResumeSubscriptionCommandHandler : IRequestHandler<ResumeSubscriptionCommand, SubscriptionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptions;
    private readonly ICurrentUserService _current;

    public ResumeSubscriptionCommandHandler(
        IApplicationDbContext db, ISubscriptionService subscriptions, ICurrentUserService current)
    {
        _db = db;
        _subscriptions = subscriptions;
        _current = current;
    }

    public async Task<SubscriptionDto> Handle(ResumeSubscriptionCommand request, CancellationToken cancellationToken)
    {
        await SubscriptionOwnership.EnsureOwnedAsync(_db, _current, request.Id, cancellationToken);

        await _subscriptions.ResumeAsync(request.Id, cancellationToken);

        return await SubscriptionOwnership.LoadDtoAsync(_db, request.Id, cancellationToken);
    }
}
