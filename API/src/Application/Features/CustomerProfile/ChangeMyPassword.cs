using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Validation;

namespace HKPartsEcommerce.Application.Features.CustomerProfile;

/// <summary>
/// Changes the authenticated customer's password after verifying the current one (AC-CUS-002.1).
/// </summary>
public record ChangeMyPasswordCommand(string CurrentPassword, string NewPassword) : IRequest;

public class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).Password();
    }
}

public class ChangeMyPasswordCommandHandler : IRequestHandler<ChangeMyPasswordCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPasswordHasher _hasher;

    public ChangeMyPasswordCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService current,
        IPasswordHasher hasher)
    {
        _db = db;
        _current = current;
        _hasher = hasher;
    }

    public async Task Handle(ChangeMyPasswordCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is null)
            throw new UnauthorizedException();

        var customer = await _db.Customers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == _current.UserId.Value, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var user = customer.User!;

        if (!_hasher.Verify(user.PasswordHash, request.CurrentPassword))
            throw new UnauthorizedException("The current password is incorrect.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
