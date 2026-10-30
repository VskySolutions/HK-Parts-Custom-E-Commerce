using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Encrypts/decrypts miscellaneous secrets held outside the per-integration credential tables (SMTP
/// passwords, the reCAPTCHA secret) via the .NET Data Protection API, and resolves an integration's
/// active credential at runtime as the raw string / JSON its adapter parses.
/// </summary>
public interface ICredentialVault
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);

    /// <summary>
    /// Resolves the active credential for a runtime service type (e.g. "stripe", "fedex", "azure-blob")
    /// into the exact raw string or JSON its adapter expects, reading the corresponding per-integration
    /// (<c>Credentials_*</c>) table. Returns <c>null</c> when no active row is configured for the service.
    /// </summary>
    Task<string?> GetCredentialAsync(string serviceType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="GetCredentialAsync"/> but also reports whether the active row is a production
    /// (live) credential, so environment-aware adapters (PayPal/Square/Authorize.Net) can pick the sandbox
    /// vs. live endpoint. Returns <c>null</c> when no active, usable row is configured.
    /// </summary>
    Task<ResolvedCredential?> GetResolvedCredentialAsync(string serviceType, CancellationToken cancellationToken = default);
}
