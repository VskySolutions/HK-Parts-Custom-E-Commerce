using HKPartsEcommerce.Application.Common.Authorization;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Issues and validates API keys for machine-to-machine callers. Generation returns the plaintext key
/// exactly once; only its SHA-256 hash is ever persisted.
/// </summary>
public interface IApiKeyService
{
    /// <summary>Generates a fresh cryptographically-random key, returning the plaintext plus its hash and prefix.</summary>
    ApiKeyMaterial Generate();

    /// <summary>Computes the storage hash (base64 SHA-256) for a plaintext key.</summary>
    string Hash(string plaintextKey);

    /// <summary>
    /// Validates a presented <c>X-Api-Key</c> value against stored hashes. Returns the caller identity
    /// on success, or <c>null</c> when the key is unknown, revoked, or expired.
    /// </summary>
    Task<ApiCallerIdentity?> AuthenticateAsync(string presentedKey, CancellationToken cancellationToken = default);
}

/// <summary>Freshly-generated key material: the one-time plaintext, its persisted hash, and display prefix.</summary>
public sealed record ApiKeyMaterial(string PlainTextKey, string KeyHash, string Prefix);
