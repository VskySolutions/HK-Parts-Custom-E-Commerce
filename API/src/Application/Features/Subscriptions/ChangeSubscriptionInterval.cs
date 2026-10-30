using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Subscriptions;

/// <summary>The authenticated buyer changes the recurrence interval of one of their own subscriptions (AC-ORD-005.4).</summary>
public record ChangeSubscriptionIntervalCommand(Guid Id, SubscriptionInterval Interval) : IRequest<SubscriptionDto>;

public class ChangeSubscriptionIntervalCommandValidator : AbstractValidator<ChangeSubscriptionIntervalCommand>
{
    public ChangeSubscriptionIntervalCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Interval).IsInEnum();
    }
}

public class ChangeSubscriptionIntervalCommandHandler : IRequestHandler<ChangeSubscriptionIntervalCommand, SubscriptionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptions;
    private readonly ICurrentUserService _current;

    public ChangeSubscriptionIntervalCommandHandler(
        IApplicationDbContext db, ISubscriptionService subscriptions, ICurrentUserService current)
    {
        _db = db;
        _subscriptions = subscriptions;
        _current = current;
    }

    public async Task<SubscriptionDto> Handle(ChangeSubscriptionIntervalCommand request, CancellationToken cancellationToken)
    {
        await SubscriptionOwnership.EnsureOwnedAsync(_db, _current, request.Id, cancellationToken);

        await _subscriptions.ChangeIntervalAsync(request.Id, request.Interval, cancellationToken);

        return await SubscriptionOwnership.LoadDtoAsync(_db, request.Id, cancellationToken);
    }
}
