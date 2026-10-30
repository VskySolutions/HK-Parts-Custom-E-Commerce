using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerAuth;

/// <summary>Consumes an email-verification token and marks the owning user's email verified (AC-CUS-001.3).</summary>
public record VerifyEmailCommand(string Token) : IRequest;

public class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;

    public VerifyEmailCommandHandler(IApplicationDbContext db, IJwtTokenService tokens, IDateTimeProvider clock)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var tokenHash = _tokens.HashRefreshToken(request.Token);

        var token = await _db.UserTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash
                    && t.Purpose == UserTokenPurpose.EmailVerification
                    && t.ConsumedOnUtc == null
                    && t.ExpiresOnUtc > now,
                cancellationToken)
            ?? throw new NotFoundException("Verification token is invalid or has expired.");

        token.User!.EmailVerified = true;
        token.ConsumedOnUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
