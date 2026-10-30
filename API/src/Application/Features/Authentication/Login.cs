using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Authentication;

/// <summary>Authenticates a user and issues an access + refresh token pair.</summary>
public record LoginCommand(string Email, string Password) : IRequest<AuthResponse>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService _current;

    public LoginCommandHandler(
        IApplicationDbContext db,
        IPasswordHasher hasher,
        IJwtTokenService tokens,
        IDateTimeProvider clock,
        ICurrentUserService current)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
        _current = current;
    }

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Normalise the submitted email the same way CustomerLogin does. Without this, an autofilled
        // or pasted address carrying stray whitespace misses the lookup and is reported as bad
        // credentials — indistinguishable, to the buyer, from a wrong password.
        var email = request.Email.Trim();

        var user = await _db.Users
            .Include(u => u.Customer)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken);

        // Generic message + password verification even on miss to avoid revealing which failed.
        if (user is null || !_hasher.Verify(user.PasswordHash, request.Password))
            throw new UnauthorizedException("Invalid email or password.");

        var (roles, modules) = AccessScope.From(user.UserRoles);

        // Unified login (WO-112): a pure customer (Customer profile, no assigned role) must verify their
        // email before signing in (AC-CUS-001.3). Staff/admin accounts carry roles and are exempt — they
        // may be created unverified — so enforcing this here never locks an admin out.
        if (!user.EmailVerified && user.Customer is not null && roles.Count == 0)
            throw new UnauthorizedException("Please verify your email address before signing in.");

        user.LastLoginOnUtc = _clock.UtcNow;

        var (accessToken, expiresAt) = _tokens.CreateAccessToken(user, roles, modules);
        var refreshToken = _tokens.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokens.HashRefreshToken(refreshToken),
            CreatedOnUtc = _clock.UtcNow,
            ExpiresOnUtc = _clock.UtcNow.AddDays(_tokens.RefreshTokenDays),
            CreatedByIp = _current.IpAddress,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return AuthResponseFactory.Create(user, roles, modules, accessToken, expiresAt, refreshToken);
    }
}
