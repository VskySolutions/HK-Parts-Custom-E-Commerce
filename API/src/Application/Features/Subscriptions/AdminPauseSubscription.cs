using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Subscriptions;

/// <summary>An admin pauses any customer's subscription on their behalf (WO-49 admin management note).</summary>
public record AdminPauseSubscriptionCommand(Guid Id) : IRequest<SubscriptionDto>;

public class AdminPauseSubscriptionCommandHandler : IRequestHandler<AdminPauseSubscriptionCommand, SubscriptionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISubscriptionService _subscriptions;

    public AdminPauseSubscriptionCommandHandler(IApplicationDbContext db, ISubscriptionService subscriptions)
    {
        _db = db;
        _subscriptions = subscriptions;
    }

    public async Task<SubscriptionDto> Handle(AdminPauseSubscriptionCommand request, CancellationToken cancellationToken)
    {
        await _subscriptions.PauseAsync(request.Id, cancellationToken);

        var subscription = await _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Subscription), request.Id);

        return SubscriptionDto.From(subscription);
    }
}
