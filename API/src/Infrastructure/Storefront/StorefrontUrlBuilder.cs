using Microsoft.Extensions.Configuration;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Storefront;

/// <summary>
/// Resolves the public storefront base URL from configuration ("Storefront:PublicBaseUrl") and builds the
/// customer-facing verification/reset links against the SPA's <c>/shop</c> account routes. Falls back to the
/// dev SPA origin when unset.
/// </summary>
public class StorefrontUrlBuilder : IStorefrontUrlBuilder
{
    private readonly string _baseUrl;

    public StorefrontUrlBuilder(IConfiguration configuration)
    {
        var configured = configuration["Storefront:PublicBaseUrl"];
        _baseUrl = string.IsNullOrWhiteSpace(configured) ? "http://localhost:9000" : configured.TrimEnd('/');
    }

    // Tokens are base64 (may contain '+', '/', '='), so they MUST be URL-encoded into the query string —
    // a raw '+' decodes to a space and corrupts the token ("invalid or has expired" on every link).
    public string EmailVerificationUrl(string token) => $"{_baseUrl}/shop/verify-email?token={Uri.EscapeDataString(token)}";

    public string PasswordResetUrl(string token) => $"{_baseUrl}/shop/reset-password?token={Uri.EscapeDataString(token)}";

    public string AdminPasswordResetUrl(string token) => $"{_baseUrl}/auth/reset-password?token={Uri.EscapeDataString(token)}";
}
