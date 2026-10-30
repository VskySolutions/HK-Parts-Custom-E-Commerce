using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Authentication;

/// <summary>Exchanges a valid refresh token for a new access token, rotating the refresh token.</summary>
public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
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

    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = _tokens.HashRefreshToken(request.RefreshToken);
        var existing = await _db.RefreshTokens
            .Include(r => r.User).ThenInclude(u => u!.Customer)
            .Include(r => r.User).ThenInclude(u => u!.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);

        if (existing is null || !existing.IsActive)
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var user = existing.User;
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("Invalid or expired refresh token.");

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

        return AuthResponseFactory.Create(user, roles, modules, accessToken, expiresAt, newRefreshToken);
    }
}
