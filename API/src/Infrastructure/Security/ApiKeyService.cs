using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Security;

/// <summary>
/// Generates and validates API keys for machine-to-machine callers. Keys are minted with 32 bytes of
/// cryptographic randomness and stored only as a base64 SHA-256 hash; the plaintext is returned once at
/// creation and never persisted (Authentication and Authorization blueprint, API Key ADR-002).
/// </summary>
public class ApiKeyService : IApiKeyService
{
    private const string KeyPrefix = "vsk_";
    private const int PrefixDisplayLength = 12; // "vsk_" + 8 chars of the secret

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ApiKeyService(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public ApiKeyMaterial Generate()
    {
        var secret = Base64Url(RandomNumberGenerator.GetBytes(32));
        var plaintext = KeyPrefix + secret;
        var prefix = plaintext[..Math.Min(PrefixDisplayLength, plaintext.Length)];
        return new ApiKeyMaterial(plaintext, Hash(plaintext), prefix);
    }

    public string Hash(string plaintextKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextKey));
        return Convert.ToBase64String(hash);
    }

    public async Task<ApiCallerIdentity?> AuthenticateAsync(string presentedKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentedKey))
            return null;

        var hash = Hash(presentedKey.Trim());

        // The query filter already excludes soft-deleted keys; IsActive excludes revoked keys.
        var key = await _db.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive, cancellationToken);

        if (key is null)
            return null;

        if (key.ExpiresAtUtc is not null && key.ExpiresAtUtc.Value <= _clock.UtcNow)
            return null;

        return new ApiCallerIdentity(key.Id, key.Name, key.Scopes.ToList());
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
