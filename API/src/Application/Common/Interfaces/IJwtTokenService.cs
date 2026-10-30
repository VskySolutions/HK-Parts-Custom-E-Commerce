using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>Issues signed access tokens and generates/hashes refresh tokens (Authentication blueprint).</summary>
public interface IJwtTokenService
{
    (string token, DateTime expiresAtUtc) CreateAccessToken(
        User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> accessibleModules);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
    int RefreshTokenDays { get; }
}
