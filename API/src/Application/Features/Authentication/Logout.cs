using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Authentication;

/// <summary>
/// Invalidates all active refresh tokens for the session's user (AC-PLT-002.5). The user is resolved
/// from the presented refresh token if supplied, otherwise from the authenticated caller.
/// </summary>
public record LogoutCommand(string? RefreshToken) : IRequest;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService _current;

    public LogoutCommandHandler(
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

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        Guid? userId = null;

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = _tokens.HashRefreshToken(request.RefreshToken);
            var token = await _db.RefreshTokens
                .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);
            userId = token?.UserId;
        }

        userId ??= _current.UserId;
        if (userId is null)
            return;

        var activeTokens = await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedOnUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
            token.RevokedOnUtc = _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
