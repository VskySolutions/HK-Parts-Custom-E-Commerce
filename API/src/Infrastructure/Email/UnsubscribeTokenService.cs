using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Email;

/// <summary>
/// Data Protection-backed <see cref="IUnsubscribeTokenService"/> (WO-87). Protecting the recipient email with
/// a dedicated, versioned purpose yields a tamper-proof, URL-safe token whose validation is constant-time —
/// satisfying "signed, time-safe, no auth required" without introducing a new HMAC secret to manage. Tokens
/// are intentionally non-expiring: an unsubscribe link must keep working indefinitely so a recipient can
/// always opt out. Bumping <see cref="Purpose"/> invalidates every outstanding link at once.
/// </summary>
public class UnsubscribeTokenService : IUnsubscribeTokenService
{
    private const string Purpose = "VSky.Marketing.Unsubscribe.v1";

    private readonly IDataProtector _protector;

    public UnsubscribeTokenService(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector(Purpose);

    public string Generate(string email)
        => string.IsNullOrWhiteSpace(email) ? string.Empty : _protector.Protect(email.Trim());

    public bool TryValidate(string token, out string email)
    {
        email = string.Empty;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var value = _protector.Unprotect(token);
            if (string.IsNullOrWhiteSpace(value))
                return false;
            email = value;
            return true;
        }
        // CryptographicException = tampered/foreign token; FormatException = malformed base64url input.
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }
    }
}
