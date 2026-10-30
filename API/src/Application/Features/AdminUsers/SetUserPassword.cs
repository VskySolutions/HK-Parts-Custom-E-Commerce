using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Validation;

namespace HKPartsEcommerce.Application.Features.AdminUsers;

/// <summary>Lets an administrator set (overwrite) another user's password directly.</summary>
public record SetUserPasswordCommand(Guid Id, string NewPassword) : IRequest;

public class SetUserPasswordCommandValidator : AbstractValidator<SetUserPasswordCommand>
{
    public SetUserPasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword).Password();
    }
}

public class SetUserPasswordCommandHandler : IRequestHandler<SetUserPasswordCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;

    public SetUserPasswordCommandHandler(IApplicationDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task Handle(SetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.User), request.Id);

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
