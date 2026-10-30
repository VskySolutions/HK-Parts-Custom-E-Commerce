using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Validation;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Authentication;

/// <summary>Consumes an admin password-reset token and sets a new password for the owning user.</summary>
public record AdminResetPasswordCommand(string Token, string NewPassword) : IRequest;

public class AdminResetPasswordCommandValidator : AbstractValidator<AdminResetPasswordCommand>
{
    public AdminResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).Password();
    }
}

public class AdminResetPasswordCommandHandler : IRequestHandler<AdminResetPasswordCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;

    public AdminResetPasswordCommandHandler(
        IApplicationDbContext db,
        IPasswordHasher hasher,
        IJwtTokenService tokens,
        IDateTimeProvider clock)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task Handle(AdminResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var tokenHash = _tokens.HashRefreshToken(request.Token);

        var token = await _db.UserTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash
                    && t.Purpose == UserTokenPurpose.PasswordReset
                    && t.ConsumedOnUtc == null
                    && t.ExpiresOnUtc > now,
                cancellationToken)
            ?? throw new NotFoundException("Reset token is invalid or has expired.");

        token.User!.PasswordHash = _hasher.Hash(request.NewPassword);
        token.ConsumedOnUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
