using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Storefront;

/// <summary>
/// Resolves the public storefront base URL and builds the customer-facing links (verification, reset, guest
/// order view/lookup) against the SPA's <c>/shop</c> routes.
/// <para>
/// Source order: <c>Storefront:PublicBaseUrl</c> when it is set to a real (non-localhost) host; otherwise the
/// SPA origin of the current request (Origin / Referer header), accepted only when it is one of the
/// <c>Cors:AllowedOrigins</c> so a forged header can't plant a foreign link in an email; otherwise the
/// configured value or the dev SPA origin. This keeps emails sent from a deployed server from linking to
/// <c>http://localhost:9000</c> when its appsettings still carries the dev value.
/// </para>
/// </summary>
public class StorefrontUrlBuilder : IStorefrontUrlBuilder
{
    private const string DevBaseUrl = "http://localhost:9000";

    // Same fallback list Program.cs uses for CORS when Cors:AllowedOrigins is not configured.
    private static readonly string[] DefaultAllowedOrigins =
        ["http://localhost:9000", "https://ecommerce.vskyapplications.com", "https://hkparts.vskyapplications.com"];

    private readonly string? _configured;
    private readonly HashSet<string> _allowedOrigins;
    private readonly IHttpContextAccessor? _http;

    public StorefrontUrlBuilder(IConfiguration configuration, IHttpContextAccessor? http = null)
    {
        var configured = configuration["Storefront:PublicBaseUrl"];
        _configured = string.IsNullOrWhiteSpace(configured) ? null : configured.Trim().TrimEnd('/');
        var origins = configuration.GetSection("Cors:AllowedOrigins").GetChildren()
            .Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToArray();
        _allowedOrigins = new HashSet<string>(
            (origins.Length > 0 ? origins : DefaultAllowedOrigins).Select(o => o.Trim().TrimEnd('/')),
            StringComparer.OrdinalIgnoreCase);
        _http = http;
    }

    private string BaseUrl
    {
        get
        {
            if (_configured is not null && !IsLocalhost(_configured))
                return _configured;
            return RequestOrigin() ?? _configured ?? DevBaseUrl;
        }
    }

    /// <summary>The calling SPA's origin, when it is a non-localhost allowed CORS origin.</summary>
    private string? RequestOrigin()
    {
        var request = _http?.HttpContext?.Request;
        if (request is null)
            return null;
        foreach (var candidate in new[] { request.Headers.Origin.ToString(), request.Headers.Referer.ToString() })
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
                continue;
            var origin = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            if (!IsLocalhost(origin) && _allowedOrigins.Contains(origin))
                return origin;
        }
        return null;
    }

    private static bool IsLocalhost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));

    // Tokens are base64 (may contain '+', '/', '='), so they MUST be URL-encoded into the query string —
    // a raw '+' decodes to a space and corrupts the token ("invalid or has expired" on every link).
    public string EmailVerificationUrl(string token) => $"{BaseUrl}/shop/verify-email?token={Uri.EscapeDataString(token)}";

    public string PasswordResetUrl(string token) => $"{BaseUrl}/shop/reset-password?token={Uri.EscapeDataString(token)}";

    public string AdminPasswordResetUrl(string token) => $"{BaseUrl}/auth/reset-password?token={Uri.EscapeDataString(token)}";

    public string GuestOrderUrl(string token) => $"{BaseUrl}/shop/order/view?token={Uri.EscapeDataString(token)}";

    public string GuestOrderLookupUrl() => $"{BaseUrl}/shop/order/lookup";
}
