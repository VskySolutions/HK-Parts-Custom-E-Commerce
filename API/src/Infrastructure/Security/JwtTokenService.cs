using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Security;

/// <summary>RS256 token issuer and refresh-token generator/hasher.</summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly RsaKeyProvider _keys;

    public JwtTokenService(IOptions<JwtOptions> options, RsaKeyProvider keys)
    {
        _options = options.Value;
        _keys = keys;
    }

    public int RefreshTokenDays => _options.RefreshTokenDays <= 0 ? 30 : _options.RefreshTokenDays;

    public (string token, DateTime expiresAtUtc) CreateAccessToken(
        User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> accessibleModules)
    {
        var now = DateTime.UtcNow;
        var minutes = _options.AccessTokenMinutes <= 0 ? 15 : _options.AccessTokenMinutes;
        var expires = now.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.Username),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(accessibleModules.Select(m => new Claim("accessibleModules", m)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = _keys.SigningCredentials,
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expires);
    }

    public string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string refreshToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(hash);
    }
}
