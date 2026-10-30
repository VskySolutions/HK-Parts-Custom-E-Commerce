using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Validation;

namespace HKPartsEcommerce.Application.Features.Authentication;

/// <summary>Changes the currently signed-in user's password after verifying the current one.</summary>
public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).Password();
    }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPasswordHasher _hasher;

    public ChangePasswordCommandHandler(IApplicationDbContext db, ICurrentUserService current, IPasswordHasher hasher)
    {
        _db = db;
        _current = current;
        _hasher = hasher;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is null)
            throw new UnauthorizedException();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _current.UserId.Value, cancellationToken)
            ?? throw new UnauthorizedException();

        if (!_hasher.Verify(user.PasswordHash, request.CurrentPassword))
            throw new UnauthorizedException("The current password is incorrect.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
