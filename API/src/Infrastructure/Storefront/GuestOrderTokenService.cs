using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Storefront;

/// <summary>
/// Data Protection-backed <see cref="IGuestOrderTokenService"/>: a time-limited protector seals the order id, so the
/// token is authenticated-encrypted, carries its own expiry and validates in constant time (N-04). Bumping
/// <see cref="Purpose"/> invalidates every outstanding guest order link at once.
/// </summary>
public class GuestOrderTokenService : IGuestOrderTokenService
{
    private const string Purpose = "VSky.GuestOrder.View.v1";

    private readonly ITimeLimitedDataProtector _protector;

    public GuestOrderTokenService(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    public string Generate(Guid orderId, TimeSpan lifetime)
        => _protector.Protect(orderId.ToString("N"), lifetime);

    public bool TryValidate(string? token, out Guid orderId)
    {
        orderId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token))
            return false;
        try
        {
            return Guid.TryParseExact(_protector.Unprotect(token.Trim()), "N", out orderId);
        }
        // CryptographicException = tampered, foreign or expired token; FormatException = malformed input.
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }
    }
}
