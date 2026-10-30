using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Gdpr;

/// <summary>
/// Erases the authenticated customer's account (GDPR right to erasure, WO-23). Resolves the customer from the
/// current login and anonymises their personal data in place — order records are preserved, so this is an
/// anonymisation rather than a hard delete. Idempotent-ish: once run, the login is disabled and the profile
/// soft-deleted, so it cannot be invoked again by the same session.
/// </summary>
public record DeleteMyAccountCommand : IRequest;

public class DeleteMyAccountCommandHandler : IRequestHandler<DeleteMyAccountCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IGdprService _gdpr;

    public DeleteMyAccountCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService current,
        IGdprService gdpr)
    {
        _db = db;
        _current = current;
        _gdpr = gdpr;
    }

    public async Task Handle(DeleteMyAccountCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is null)
            throw new UnauthorizedException();

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == _current.UserId.Value, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        await _gdpr.AnonymizeCustomerAsync(customer.Id, cancellationToken);
    }
}
