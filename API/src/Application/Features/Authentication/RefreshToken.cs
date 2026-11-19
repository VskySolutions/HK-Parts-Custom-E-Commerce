using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Authentication;

/// <summary>
/// Exchanges a valid refresh token for a new access token, rotating the refresh token. An unknown, expired,
/// revoked or orphaned token is a normal outcome (a stale browser session), not an error: the result carries
/// <see cref="RefreshTokenResult.Error"/> instead of throwing, so it is answered with a 401 message without an
/// exception or a stack-trace log entry.
/// </summary>
public record RefreshTokenCommand(string RefreshToken) : IRequest<RefreshTokenResult>;

/// <summary>Either the new token pair (<see cref="Response"/>) or why the refresh was refused (<see cref="Error"/>).</summary>
public class RefreshTokenResult
{
    public const string InvalidTokenMessage = "Invalid or expired refresh token.";

    public AuthResponse? Response { get; init; }
    public string? Error { get; init; }
    public bool Succeeded => Response is not null;

    public static RefreshTokenResult Success(AuthResponse response) => new() { Response = response };
    public static RefreshTokenResult Failure(string error) => new() { Error = error };
}

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService _current;

    public RefreshTokenCommandHandler(
        IApplicationDbContext db,
        IJwtTokenService tokens,
        IDateTimeProvider clock,
        ICurrentUserService current)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _current = current;
    }

    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = _tokens.HashRefreshToken(request.RefreshToken);
        var existing = await _db.RefreshTokens
            .Include(r => r.User).ThenInclude(u => u!.Customer)
            .Include(r => r.User).ThenInclude(u => u!.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);

        if (existing is null || !existing.IsActive)
            return RefreshTokenResult.Failure(RefreshTokenResult.InvalidTokenMessage);

        var user = existing.User;
        if (user is null || !user.IsActive)
            return RefreshTokenResult.Failure(RefreshTokenResult.InvalidTokenMessage);

        // Rotate: revoke the presented token and issue a successor (single-use).
        existing.RevokedOnUtc = _clock.UtcNow;

        var newRefreshToken = _tokens.GenerateRefreshToken();
        var replacement = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokens.HashRefreshToken(newRefreshToken),
            CreatedOnUtc = _clock.UtcNow,
            ExpiresOnUtc = _clock.UtcNow.AddDays(_tokens.RefreshTokenDays),
            CreatedByIp = _current.IpAddress,
        };
        _db.RefreshTokens.Add(replacement);
        existing.ReplacedByTokenId = replacement.Id;

        var (roles, modules) = AccessScope.From(user.UserRoles);
        var (accessToken, expiresAt) = _tokens.CreateAccessToken(user, roles, modules);
        await _db.SaveChangesAsync(cancellationToken);

        return RefreshTokenResult.Success(
            AuthResponseFactory.Create(user, roles, modules, accessToken, expiresAt, newRefreshToken));
    }
}
