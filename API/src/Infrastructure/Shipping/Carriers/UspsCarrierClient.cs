using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Shipping.Carriers;

/// <summary>
/// USPS rating adapter against the USPS REST APIs (apis.usps.com), replacing the Web Tools XML API retired
/// on 2026-01-25 (docs/usps-shipping-requirements.md). Settings come from the single active
/// <see cref="UspsCredential"/> row: OAuth2 client credentials, environment host, price type, the allowed
/// domestic and international mail classes, handling fee, minimum order, ship-to countries, default
/// package and logging.
/// <para>
/// One base-rates search is made per allowed mail class. A class USPS declines (4xx) is skipped; the rest
/// still quote. Any failure yields no options, so the aggregate simply excludes USPS (AC-SHP-001.3).
/// Tokens are cached until 60 s before expiry (US-53), successful rates for 10 minutes (US-55), and each
/// call times out after 5 s with one retry on 5xx (US-54).
/// </para>
/// </summary>
/// <remarks>
/// Paths, mail-class codes and rate indicators follow the USPS v3 catalog as understood at build time;
/// confirm against a real account with the admin Test button (US-40).
/// </remarks>
public class UspsCarrierClient : ICarrierClient, IUspsRater
{
    private const string TokenPath = "/oauth2/v3/token";
    private const string DomesticRatePath = "/prices/v3/base-rates/search";
    private const string InternationalRatePath = "/international-prices/v3/base-rates/search";
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RateCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromSeconds(60);

    private const decimal PoundsPerKilogram = 2.20462m;
    private const decimal CentimetresPerInch = 2.54m;

    // Fallback parcels when neither the cart nor the admin's default package gives dimensions. Domestic:
    // within the 15x18x22" limit that keeps a parcel MACHINABLE. International: the legacy fixed 15x15x15".
    private static readonly (decimal L, decimal W, decimal H) DomesticFallbackBox = (12m, 9m, 6m);
    private static readonly (decimal L, decimal W, decimal H) InternationalFallbackBox = (15m, 15m, 15m);

    /// <summary>Process-wide token cache, keyed by host + consumer key + a hash of the secret.</summary>
    private static readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> TokenCache = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UspsCarrierClient> _logger;

    public UspsCarrierClient(
        IHttpClientFactory httpClientFactory,
        IApplicationDbContext db,
        IMemoryCache cache,
        ILogger<UspsCarrierClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public string CarrierName => "USPS";
    public ShippingCarrierType Carrier => ShippingCarrierType.USPS;

    // ------------------------------------------------------------------ USPS method (checkout)

    public async Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
    {
        var settings = await LoadActiveSettingsAsync(ct);
        if (settings is null)
            return Array.Empty<ShippingRateOption>();

        // USPS only ships from the US.
        if (!IsUnitedStates(request.Origin.CountryCode))
            return Array.Empty<ShippingRateOption>();

        // US-12: nothing to rate.
        if (request.WeightKg <= 0m)
            return Array.Empty<ShippingRateOption>();

        if (settings.MinimumOrderAmount is { } min && request.OrderSubtotal < min)
            return Array.Empty<ShippingRateOption>();

        var destinationCountry = NormalizeCountry(request.Destination.CountryCode);
        if (settings.ShipToCountries.Count > 0 && !settings.ShipToCountries.Contains(destinationCountry))
            return Array.Empty<ShippingRateOption>();

        var international = destinationCountry != "US";
        var quotes = await QuoteAsync(settings, request, international, useCache: true, errors: null, ct);

        // US-13 / US-14: "<display name> - <service>", priced USPS + handling fee.
        return quotes
            .Select(q => new ShippingRateOption(
                MethodId: q.MailClass,
                Name: $"{settings.DisplayName} - {UspsMailClasses.NameOf(q.MailClass)}",
                Carrier: CarrierName,
                EstimatedDeliveryDays: q.TransitDays,
                Rate: Math.Round(q.Price + settings.HandlingFee, 2, MidpointRounding.AwayFromZero),
                HandlingFee: settings.HandlingFee))
            .ToList();
    }

    // ------------------------------------------------------------------ Custom method (international)

    public async Task<IReadOnlyList<ShippingRateOption>> GetInternationalRatesAsync(CarrierRateRequest request, CancellationToken ct)
    {
        var settings = await LoadActiveSettingsAsync(ct);
        if (settings is null || request.WeightKg <= 0m || !IsUnitedStates(request.Origin.CountryCode))
            return Array.Empty<ShippingRateOption>();
        if (NormalizeCountry(request.Destination.CountryCode) == "US")
            return Array.Empty<ShippingRateOption>();

        var quotes = await QuoteAsync(settings, request, international: true, useCache: true, errors: null, ct);
        return quotes
            .Select(q => new ShippingRateOption(
                q.MailClass, UspsMailClasses.NameOf(q.MailClass), CarrierName, q.TransitDays,
                Math.Round(q.Price, 2, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    // ------------------------------------------------------------------ Admin test (US-40)

    public async Task<CarrierTestResult> TestCredentialAsync(Guid credentialId, CancellationToken ct)
    {
        var entity = await _db.UspsCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == credentialId, ct)
            ?? throw new HKPartsEcommerce.Application.Common.Exceptions.NotFoundException(nameof(UspsCredential), credentialId);

        var settings = UspsSettings.From(entity);
        if (settings is null)
            return new CarrierTestResult(false, "Base URL, consumer key and consumer secret are all required.",
                Array.Empty<CarrierTestLine>(), Array.Empty<CarrierTestLine>());

        var client = CreateHttpClient();
        string? token;
        try
        {
            token = await GetAccessTokenAsync(client, settings, ct, forceRefresh: true);
        }
        catch (Exception ex)
        {
            return new CarrierTestResult(false, ex.Message, Array.Empty<CarrierTestLine>(), Array.Empty<CarrierTestLine>());
        }
        if (token is null)
            return new CarrierTestResult(false, "USPS refused the consumer key / secret (see the API log for the response).",
                Array.Empty<CarrierTestLine>(), Array.Empty<CarrierTestLine>());

        // Sample shipments: 2 lb from 28270 (Charlotte, NC) to 10001 (New York) and to Ottawa, Canada.
        const decimal twoPoundsKg = 2m / PoundsPerKilogram;
        var origin = new CarrierAddress("US", "NC", "28270", null, null, "Charlotte");
        var domesticRequest = new CarrierRateRequest(
            origin, new CarrierAddress("US", "NY", "10001", null, null, "New York"), twoPoundsKg, null, null, null, 100m);
        var internationalRequest = new CarrierRateRequest(
            origin, new CarrierAddress("CA", "ON", "K1A 0B1", null, null, "Ottawa"), twoPoundsKg, null, null, null, 100m);

        var domesticErrors = new List<CarrierTestLine>();
        var domestic = await QuoteAsync(settings, domesticRequest, false, useCache: false, domesticErrors, ct);
        var internationalErrors = new List<CarrierTestLine>();
        var international = await QuoteAsync(settings, internationalRequest, true, useCache: false, internationalErrors, ct);

        static IReadOnlyList<CarrierTestLine> Lines(IEnumerable<UspsQuote> quotes, List<CarrierTestLine> errors)
            => quotes.Select(q => new CarrierTestLine(UspsMailClasses.NameOf(q.MailClass), q.Price, null))
                .Concat(errors)
                .ToList();

        return new CarrierTestResult(true, null, Lines(domestic, domesticErrors), Lines(international, internationalErrors),
            "Domestic — 2 lb, 28270 → 10001", "International — 2 lb, 28270 → Ottawa, Canada");
    }

    // ------------------------------------------------------------------ core

    private async Task<UspsSettings?> LoadActiveSettingsAsync(CancellationToken ct)
    {
        var entity = await _db.UspsCredentials.AsNoTracking()
            .Where(c => c.Active)
            .OrderByDescending(c => c.UpdatedOnUtc)
            .FirstOrDefaultAsync(ct);
        return entity is null ? null : UspsSettings.From(entity);
    }

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient("usps");
        client.Timeout = HttpTimeout;
        return client;
    }

    /// <summary>
    /// Quotes every allowed mail class for one shipment. <paramref name="errors"/>, when supplied (the admin
    /// test), collects a line per class that failed instead of only logging it.
    /// </summary>
    private async Task<IReadOnlyList<UspsQuote>> QuoteAsync(
        UspsSettings settings, CarrierRateRequest request, bool international, bool useCache,
        List<CarrierTestLine>? errors, CancellationToken ct)
    {
        var mailClasses = international ? settings.InternationalServices : settings.DomesticServices;
        if (mailClasses.Count == 0)
            return Array.Empty<UspsQuote>();

        try
        {
            var client = CreateHttpClient();
            var token = await GetAccessTokenAsync(client, settings, ct);
            if (token is null)
            {
                errors?.Add(new CarrierTestLine("OAuth token", null, "USPS refused the consumer key / secret."));
                return Array.Empty<UspsQuote>();
            }

            var path = international ? InternationalRatePath : DomesticRatePath;
            var tasks = mailClasses.Select(async mailClass =>
            {
                var body = international
                    ? BuildInternationalBody(settings, request, mailClass)
                    : BuildDomesticBody(settings, request, mailClass);
                return await QuoteOneAsync(client, settings, token, path, mailClass, body, useCache, errors, ct);
            });

            var results = await Task.WhenAll(tasks);
            return results.Where(q => q is not null).Select(q => q!).OrderBy(q => q.Price).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Shipping carrier {Carrier} rate call threw.", CarrierName);
            errors?.Add(new CarrierTestLine("Request", null, ex.Message));
            return Array.Empty<UspsQuote>();
        }
    }

    private async Task<UspsQuote?> QuoteOneAsync(
        HttpClient client, UspsSettings settings, string token, string path, string mailClass,
        Dictionary<string, object?> body, bool useCache, List<CarrierTestLine>? errors, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body);

        // The mailing date changes daily but does not change a cached price within 10 minutes; leave it out
        // of the key so the cache keys on origin, destination, weight, package and class (US-55).
        var keyBody = new Dictionary<string, object?>(body);
        keyBody.Remove("mailingDate");
        var cacheKey = "usps-rate:" + Hash($"{settings.BaseUrl}|{path}|{JsonSerializer.Serialize(keyBody)}");
        if (useCache && _cache.TryGetValue(cacheKey, out UspsQuote? cached) && cached is not null)
            return cached;

        var url = $"{settings.BaseUrl}{path}";
        var stopwatch = Stopwatch.StartNew();
        using var response = await SendWithRetryAsync(client, () =>
        {
            var message = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return message;
        }, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        stopwatch.Stop();

        if (settings.LoggingEnabled)
        {
            // US-56: request + response bodies carry no secret — the bearer token travels only in the header.
            _logger.LogInformation(
                "USPS {MailClass} rate: HTTP {StatusCode} in {ElapsedMs} ms. Request: {Request} Response: {Response}",
                mailClass, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, json, Truncate(responseBody));
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Shipping carrier {Carrier} rate ({MailClass}) call failed: HTTP {StatusCode}. Response: {Body}",
                CarrierName, mailClass, (int)response.StatusCode, Truncate(responseBody));
            AddError(errors, new CarrierTestLine(UspsMailClasses.NameOf(mailClass), null,
                $"HTTP {(int)response.StatusCode}: {ReadErrorMessage(responseBody)}"));
            return null;
        }

        using var document = JsonDocument.Parse(responseBody);
        var price = ReadPrice(document.RootElement);
        if (price is null)
        {
            AddError(errors, new CarrierTestLine(UspsMailClasses.NameOf(mailClass), null, "Response carried no price."));
            return null;
        }

        var quote = new UspsQuote(mailClass, price.Value, ReadTransitDays(document.RootElement));
        if (useCache)
            _cache.Set(cacheKey, quote, RateCacheDuration);
        return quote;
    }

    /// <summary>US-54: one retry on a 5xx or a timeout; anything else is returned as is.</summary>
    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client, Func<HttpRequestMessage> build, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = build();
            try
            {
                var response = await client.SendAsync(request, ct);
                if ((int)response.StatusCode < 500 || attempt == 2)
                    return response;
                response.Dispose();
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < 2)
            {
                // HttpClient timeout — retry once.
            }
        }
    }

    private async Task<string?> GetAccessTokenAsync(
        HttpClient client, UspsSettings settings, CancellationToken ct, bool forceRefresh = false)
    {
        var key = $"{settings.BaseUrl}|{settings.ConsumerKey}|{Hash(settings.ConsumerSecret)}";
        if (!forceRefresh && TokenCache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Token;

        var stopwatch = Stopwatch.StartNew();
        using var response = await SendWithRetryAsync(client, () => new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl}{TokenPath}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = settings.ConsumerKey,
                ["client_secret"] = settings.ConsumerSecret,
            }),
        }, ct);
        stopwatch.Stop();

        if (settings.LoggingEnabled)
            _logger.LogInformation("USPS OAuth token: HTTP {StatusCode} in {ElapsedMs} ms.",
                (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            await CarrierHttpDiagnostics.LogFailedResponseAsync(_logger, CarrierName, "OAuth token", response, ct);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        var token = root.TryGetProperty("access_token", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var lifetime = TimeSpan.FromHours(1);
        if (root.TryGetProperty("expires_in", out var e))
        {
            if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var seconds))
                lifetime = TimeSpan.FromSeconds(seconds);
            else if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), out var parsed))
                lifetime = TimeSpan.FromSeconds(parsed);
        }

        // US-53: refresh 60 s before expiry.
        var expiresAt = DateTimeOffset.UtcNow + (lifetime > TokenRefreshMargin ? lifetime - TokenRefreshMargin : lifetime);
        TokenCache[key] = (token!, expiresAt);
        return token;
    }

    // ------------------------------------------------------------------ request bodies

    /// <summary>
    /// US-50. USPS wants the whole parcel descriptor: weight (lb, decimals), all three dimensions, processing
    /// category, rate indicator and entry-facility type are each required, and omitting any of them fails the
    /// call with 400 "One or more required field(s) are missing".
    /// </summary>
    private static Dictionary<string, object?> BuildDomesticBody(UspsSettings settings, CarrierRateRequest request, string mailClass)
    {
        var (l, w, h) = Dimensions(settings, request, DomesticFallbackBox);
        return new Dictionary<string, object?>
        {
            ["originZIPCode"] = FiveDigitZip(request.Origin.PostalCode),
            ["destinationZIPCode"] = FiveDigitZip(request.Destination.PostalCode),
            ["weight"] = WeightLb(request),
            ["length"] = l,
            ["width"] = w,
            ["height"] = h,
            ["mailClass"] = mailClass,
            ["processingCategory"] = "MACHINABLE",
            ["rateIndicator"] = DomesticRateIndicator(mailClass),
            ["destinationEntryFacilityType"] = "NONE",
            ["priceType"] = settings.PriceType,
            ["mailingDate"] = MailingDate(),
        };
    }

    /// <summary>US-51: destination as an ISO-2 country code, not the country name the legacy site sent.</summary>
    private static Dictionary<string, object?> BuildInternationalBody(UspsSettings settings, CarrierRateRequest request, string mailClass)
    {
        var (l, w, h) = Dimensions(settings, request, InternationalFallbackBox);
        return new Dictionary<string, object?>
        {
            ["originZIPCode"] = FiveDigitZip(request.Origin.PostalCode),
            ["foreignPostalCode"] = request.Destination.PostalCode?.Trim() ?? string.Empty,
            ["destinationCountryCode"] = NormalizeCountry(request.Destination.CountryCode),
            ["weight"] = WeightLb(request),
            ["length"] = l,
            ["width"] = w,
            ["height"] = h,
            ["mailClass"] = mailClass,
            ["processingCategory"] = "MACHINABLE",
            ["rateIndicator"] = "SP",
            ["destinationEntryFacilityType"] = "NONE",
            ["priceType"] = settings.PriceType,
            ["mailingDate"] = MailingDate(),
        };
    }

    /// <summary>Single-piece rate indicator per class; Priority Mail Express uses its own "PA" indicator.</summary>
    private static string DomesticRateIndicator(string mailClass)
        => mailClass.Equals("PRIORITY_MAIL_EXPRESS", StringComparison.OrdinalIgnoreCase) ? "PA" : "SP";

    private static decimal WeightLb(CarrierRateRequest request)
        => Math.Max(0.01m, Math.Round(request.WeightKg * PoundsPerKilogram, 2));

    private static string MailingDate()
        => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Cart dimensions (cm → in) when given, else the admin's default package, else the fallback box.</summary>
    private static (decimal L, decimal W, decimal H) Dimensions(
        UspsSettings settings, CarrierRateRequest request, (decimal L, decimal W, decimal H) fallback)
    {
        static decimal In(decimal? cm) => Math.Round(cm!.Value / CentimetresPerInch, 2);
        if (request.Length is > 0m && request.Width is > 0m && request.Height is > 0m)
            return (In(request.Length), In(request.Width), In(request.Height));
        return (settings.DefaultLengthIn ?? fallback.L, settings.DefaultWidthIn ?? fallback.W, settings.DefaultHeightIn ?? fallback.H);
    }

    // ------------------------------------------------------------------ response parsing

    /// <summary>
    /// A base-rates search answers { totalBasePrice, rates: [{ price, ... }] }: the total is the whole price
    /// for the one parcel, and rates[] its components — so this reads one price per mail class, never one
    /// option per component.
    /// </summary>
    private static decimal? ReadPrice(JsonElement root)
    {
        if (root.TryGetProperty("totalBasePrice", out var total) && total.TryGetDecimal(out var t))
            return t;
        if (root.TryGetProperty("rates", out var rates) && rates.ValueKind == JsonValueKind.Array)
        {
            decimal sum = 0m;
            var any = false;
            foreach (var rate in rates.EnumerateArray())
            {
                if (rate.TryGetProperty("price", out var p) && p.TryGetDecimal(out var pv))
                {
                    sum += pv;
                    any = true;
                }
            }
            return any ? sum : null;
        }
        if (root.TryGetProperty("price", out var price) && price.TryGetDecimal(out var v))
            return v;
        return null;
    }

    /// <summary>
    /// Base-rates search is a pricing endpoint and generally carries no delivery estimate (that is the
    /// Service Standards API, US-18 [ENH], not built). Reads one if present, else null.
    /// </summary>
    private static int? ReadTransitDays(JsonElement element)
    {
        foreach (var name in new[] { "deliveryEstimate", "serviceStandard", "businessDaysInTransit" })
        {
            if (!element.TryGetProperty(name, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;
            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return null;
    }

    private static string ReadErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var m))
                    return m.GetString() ?? Truncate(body);
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString() ?? Truncate(body);
            }
        }
        catch (JsonException)
        {
        }
        return Truncate(body);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Mail classes are quoted in parallel, so the test's error list is appended under a lock.</summary>
    private static void AddError(List<CarrierTestLine>? errors, CarrierTestLine line)
    {
        if (errors is null)
            return;
        lock (errors)
            errors.Add(line);
    }

    private static string Truncate(string value) => value.Length > 1000 ? value[..1000] + "…" : value;

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>USPS rates against the 5-digit ZIP; a ZIP+4 or padded value is reduced to its first five digits.</summary>
    private static string FiveDigitZip(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
            return string.Empty;
        var digits = new string(postalCode.Where(char.IsDigit).ToArray());
        return digits.Length >= 5 ? digits[..5] : digits;
    }

    private static string NormalizeCountry(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "US";
        var c = code.Trim().ToUpperInvariant();
        return c is "USA" ? "US" : c;
    }

    private static bool IsUnitedStates(string? code) => NormalizeCountry(code) == "US";

    private sealed record UspsQuote(string MailClass, decimal Price, int? TransitDays);

    /// <summary>The resolved, defaulted settings of one <see cref="UspsCredential"/> row.</summary>
    private sealed record UspsSettings(
        string BaseUrl,
        string ConsumerKey,
        string ConsumerSecret,
        string DisplayName,
        string PriceType,
        IReadOnlyList<string> DomesticServices,
        IReadOnlyList<string> InternationalServices,
        decimal HandlingFee,
        decimal? MinimumOrderAmount,
        IReadOnlyList<string> ShipToCountries,
        decimal? DefaultLengthIn,
        decimal? DefaultWidthIn,
        decimal? DefaultHeightIn,
        bool LoggingEnabled)
    {
        /// <summary>Null when the row cannot authenticate (no host, key or secret).</summary>
        public static UspsSettings? From(UspsCredential c)
        {
            if (string.IsNullOrWhiteSpace(c.BaseUrl) || string.IsNullOrWhiteSpace(c.ConsumerKey) ||
                string.IsNullOrWhiteSpace(c.ConsumerSecret))
                return null;

            // Only known codes are sent, so a stale stored value cannot 400 every quote.
            var domestic = UspsMailClasses.SplitList(c.AllowedDomesticServices)
                .Where(UspsMailClasses.Domestic.ContainsKey).ToList();
            var international = UspsMailClasses.SplitList(c.AllowedInternationalServices)
                .Where(UspsMailClasses.International.ContainsKey).ToList();

            return new UspsSettings(
                c.BaseUrl.Trim().TrimEnd('/'),
                c.ConsumerKey.Trim(),
                c.ConsumerSecret,
                string.IsNullOrWhiteSpace(c.DisplayName) ? "USPS" : c.DisplayName.Trim(),
                string.Equals(c.PriceType, "COMMERCIAL", StringComparison.OrdinalIgnoreCase) ? "COMMERCIAL" : "RETAIL",
                domestic,
                international,
                c.HandlingFee ?? 0m,
                c.MinimumOrderAmount,
                UspsMailClasses.SplitList(c.ShipToCountries).Select(NormalizeCountry).ToList(),
                c.DefaultLengthIn is > 0m ? c.DefaultLengthIn : null,
                c.DefaultWidthIn is > 0m ? c.DefaultWidthIn : null,
                c.DefaultHeightIn is > 0m ? c.DefaultHeightIn : null,
                c.LoggingEnabled);
        }
    }
}
