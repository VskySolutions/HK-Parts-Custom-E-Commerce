using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Ebay;

namespace HKPartsEcommerce.Infrastructure.Ebay;

/// <summary>
/// OAuth <c>state</c> = Data-Protection-signed, 15-minute "storeId|userId|nonce". The nonce is single-use
/// (kept in memory until consumed), so a captured callback URL cannot be replayed. In a multi-instance
/// deployment the Data Protection key ring must be shared and the callback should land on the same instance
/// (or swap the memory cache for a distributed one).
/// </summary>
public sealed class EbayOAuthStateProtector : IEbayOAuthStateProtector
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly IMemoryCache _nonces;

    public EbayOAuthStateProtector(IDataProtectionProvider provider, IMemoryCache nonces)
    {
        _protector = provider.CreateProtector("VSky.Ebay.OAuthState.v1").ToTimeLimitedDataProtector();
        _nonces = nonces;
    }

    public string Create(Guid storeId, Guid? userId)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _nonces.Set(NonceKey(nonce), true, Lifetime);
        return _protector.Protect($"{storeId:N}|{userId?.ToString("N")}|{nonce}", Lifetime);
    }

    public Guid Validate(string state)
    {
        string payload;
        try
        {
            payload = _protector.Unprotect(state);
        }
        catch (CryptographicException)
        {
            throw new ForbiddenAccessException();
        }

        var parts = payload.Split('|');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var storeId))
            throw new ForbiddenAccessException();

        var key = NonceKey(parts[2]);
        if (!_nonces.TryGetValue(key, out _))
            throw new ForbiddenAccessException();
        _nonces.Remove(key);
        return storeId;
    }

    private static string NonceKey(string nonce) => $"ebay:oauth-nonce:{nonce}";
}

/// <summary>Public image URLs and the admin return URL, from <see cref="EbayOptions"/> (falls back to Storefront:PublicBaseUrl).</summary>
public sealed class EbayPublicUrlProvider : IEbayPublicUrlProvider
{
    private readonly EbayOptions _options;
    private readonly string? _storefrontBase;

    public EbayPublicUrlProvider(IOptions<EbayOptions> options, IConfiguration configuration)
    {
        _options = options.Value;
        _storefrontBase = configuration["Storefront:PublicBaseUrl"];
    }

    public string? ToPublicImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("//", StringComparison.Ordinal)) return "https:" + url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
            return absolute.ToString();
        if (string.IsNullOrWhiteSpace(_options.ImageBaseUrl)) return null;
        return _options.ImageBaseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
    }

    public string AdminSettingsUrl(string? query = null)
    {
        var origin = (_options.AdminBaseUrl ?? _storefrontBase ?? string.Empty).TrimEnd('/');
        return $"{origin}/settings/ebay{(string.IsNullOrEmpty(query) ? string.Empty : "?" + query)}";
    }
}
