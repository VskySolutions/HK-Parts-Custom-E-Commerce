using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Common;
using static HKPartsEcommerce.Infrastructure.Shipping.Carriers.CarrierRatingSupport;

namespace HKPartsEcommerce.Infrastructure.Shipping.Carriers;

/// <summary>
/// FedEx rating adapter against the FedEx REST Rate API with OAuth 2.0 client credentials
/// (docs/fedex-shipping-requirements.md). Settings come from the single active <see cref="FedExCredential"/>
/// row: host (sandbox or production), client id/secret, account number, rate mode, allowed and FFL
/// services, pickup type, per-unit package info, default package, handling fee, minimum order, ship-to
/// countries and logging.
/// <para>
/// One rate-quotes request returns every service; the result is filtered to the allowed services (and,
/// when an FFL list is set, narrowed for a cart holding FFL items). Only the configured rate type is
/// requested and read, so an ACCOUNT store never shows a LIST price or vice versa (FX-13). Tokens are cached
/// until 60 s before expiry (FX-52), rates for 10 minutes (FX-55), and each call times out after 5 s with one
/// retry on 5xx (FX-53). Any failure yields no options, so the aggregate simply excludes FedEx.
/// </para>
/// </summary>
/// <remarks>Paths and field shapes follow the FedEx REST catalog as understood at build time; confirm with the admin Test button.</remarks>
public class FedExCarrierClient : ICarrierClient, IFedExRater
{
    private const string TokenPath = "/oauth/token";
    private const string RatePath = "/rate/v1/rates/quotes";
    private const int MaxPackages = 99;

    /// <summary>Process-wide token cache keyed by host + client id + a hash of the secret (FX-52, FX-T10).</summary>
    private static readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> TokenCache = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FedExCarrierClient> _logger;

    public FedExCarrierClient(
        IHttpClientFactory httpClientFactory, IApplicationDbContext db, IMemoryCache cache, ILogger<FedExCarrierClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public string CarrierName => "FedEx";
    public ShippingCarrierType Carrier => ShippingCarrierType.FedEx;

    // ------------------------------------------------------------------ checkout

    public async Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
    {
        var entity = await _db.FedExCredentials.AsNoTracking()
            .Where(c => c.Active)
            .OrderByDescending(c => c.UpdatedOnUtc)
            .FirstOrDefaultAsync(ct);
        var settings = entity is null ? null : FedExSettings.From(entity);
        if (settings is null)
            return Array.Empty<ShippingRateOption>();

        // FX-10: common eligibility.
        if (settings.MinimumOrderAmount is { } min && request.OrderSubtotal < min)
            return Array.Empty<ShippingRateOption>();
        var destinationCountry = NormalizeCountry(request.Destination.CountryCode);
        if (settings.ShipToCountries.Count > 0 && !settings.ShipToCountries.Contains(destinationCountry))
            return Array.Empty<ShippingRateOption>();

        var quotes = await QuoteAsync(settings, request, useCache: true, errors: null, ct);

        // FX-14 / FX-15: "<display name> - <service>", FedEx price + handling fee.
        return quotes
            .Select(q => new ShippingRateOption(
                MethodId: q.ServiceType,
                Name: $"{settings.DisplayName} - {FedExCodes.ServiceName(q.ServiceType)}",
                Carrier: CarrierName,
                EstimatedDeliveryDays: q.TransitDays,
                Rate: Math.Round(q.Price + settings.HandlingFee, 2, MidpointRounding.AwayFromZero),
                HandlingFee: settings.HandlingFee))
            .ToList();
    }

    // ------------------------------------------------------------------ admin test (FX-40, FX-T11)

    public async Task<CarrierTestResult> TestCredentialAsync(Guid credentialId, CancellationToken ct)
    {
        var entity = await _db.FedExCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == credentialId, ct)
            ?? throw new NotFoundException(nameof(FedExCredential), credentialId);

        var settings = FedExSettings.From(entity);
        if (settings is null)
            return new CarrierTestResult(false, "Base URL, client ID, client secret and account number are all required.",
                Array.Empty<CarrierTestLine>(), Array.Empty<CarrierTestLine>());

        var client = CreateHttpClient();
        TokenResult token;
        try
        {
            token = await GetAccessTokenAsync(client, settings, ct, forceRefresh: true);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return new CarrierTestResult(false, ex.Message, Array.Empty<CarrierTestLine>(), Array.Empty<CarrierTestLine>());
        }
        if (token.Token is null)
            return new CarrierTestResult(false, token.Error ?? "FedEx refused the client ID / secret.",
                Array.Empty<CarrierTestLine>(), Array.Empty<CarrierTestLine>());

        // FX-T1: 5 lb residential parcel, Charlotte 28270 → New York 10001.
        var request = new CarrierRateRequest(
            new CarrierAddress("US", "NC", "28270", null, null, "Charlotte", "3334 Gray Moss Rd", null, "HK Parts"),
            new CarrierAddress("US", "NY", "10001", null, null, "New York", Name: "Test Recipient"),
            5m / PoundsPerKilogram, null, null, null, 100m);

        var errors = new List<CarrierTestLine>();
        var quotes = await QuoteAsync(settings, request, useCache: false, errors, ct);
        var lines = quotes
            .Select(q => new CarrierTestLine(FedExCodes.ServiceName(q.ServiceType), q.Price, null))
            .Concat(errors)
            .ToList();

        return new CarrierTestResult(true, null, lines, Array.Empty<CarrierTestLine>(),
            $"5 lb, residential, 28270 → 10001 ({settings.RateMode} rates)", null, token.ExpiresAt);
    }

    // ------------------------------------------------------------------ core

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient("fedex");
        client.Timeout = HttpTimeout;
        return client;
    }

    private async Task<IReadOnlyList<FedExQuote>> QuoteAsync(
        FedExSettings settings, CarrierRateRequest request, bool useCache, List<CarrierTestLine>? errors, CancellationToken ct)
    {
        // FX-21 [ENH]: an FFL list, when the admin sets one, narrows an FFL cart; empty means no extra filter.
        var hasFfl = request.Lines?.Any(l => l.IsFfl) == true;
        var services = hasFfl && settings.FflAllowedServices.Count > 0
            ? settings.AllowedServices.Where(s => settings.FflAllowedServices.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList()
            : settings.AllowedServices.ToList();
        if (services.Count == 0)
        {
            errors?.Add(new CarrierTestLine("Services", null, "No allowed service (check Allowed / FFL services)."));
            return Array.Empty<FedExQuote>();
        }

        // FX-11 / FX-16.
        var packages = BuildPackages(request, settings.SendPackageInfo, settings.DefaultBox, MaxPackages);
        if (packages is null)
            return Array.Empty<FedExQuote>();

        try
        {
            var client = CreateHttpClient();
            var token = await GetAccessTokenAsync(client, settings, ct);
            if (token.Token is null)
            {
                errors?.Add(new CarrierTestLine("OAuth token", null, token.Error ?? "FedEx refused the client ID / secret."));
                return Array.Empty<FedExQuote>();
            }

            // FX-17: a customer address is residential; an FFL cart ships to the dealer, a business.
            var residential = !hasFfl;
            var json = JsonSerializer.Serialize(BuildRequestBody(settings, request, packages, residential));

            var cacheKey = "fedex-rate:" + Hash($"{settings.BaseUrl}|{json}");
            if (!useCache || !_cache.TryGetValue(cacheKey, out List<FedExQuote>? all) || all is null)
            {
                all = await SendRateAsync(client, settings, token.Token, json, errors, ct);
                if (all is null)
                    return Array.Empty<FedExQuote>();
                if (useCache)
                    _cache.Set(cacheKey, all, RateCacheDuration);
            }

            // FX-51: ignore services not in the allowed list.
            return all
                .Where(q => services.Contains(q.ServiceType, StringComparer.OrdinalIgnoreCase))
                .OrderBy(q => q.Price)
                .ToList();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Shipping carrier {Carrier} rate call threw.", CarrierName);
            errors?.Add(new CarrierTestLine("Request", null, ex.Message));
            return Array.Empty<FedExQuote>();
        }
    }

    private async Task<List<FedExQuote>?> SendRateAsync(
        HttpClient client, FedExSettings settings, string token, string json, List<CarrierTestLine>? errors, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        using var response = await SendWithRetryAsync(client, () =>
        {
            var message = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl}{RatePath}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            message.Headers.TryAddWithoutValidation("X-locale", "en_US");
            message.Headers.TryAddWithoutValidation("x-customer-transaction-id", Guid.NewGuid().ToString("N"));
            return message;
        }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        stopwatch.Stop();

        if (settings.LoggingEnabled)
        {
            // FX-56 / FX-T12: the body carries the account number but no secret — the token is header-only.
            _logger.LogInformation(
                "FedEx rate: HTTP {StatusCode} in {ElapsedMs} ms. Request: {Request} Response: {Response}",
                (int)response.StatusCode, stopwatch.ElapsedMilliseconds, json, Truncate(body));
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = ReadError(body);
            _logger.LogWarning("Shipping carrier {Carrier} rate call failed: HTTP {StatusCode}. {Error}",
                CarrierName, (int)response.StatusCode, error);
            errors?.Add(new CarrierTestLine("Rate", null, $"HTTP {(int)response.StatusCode}: {error}"));
            return null;
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("output", out var output) ||
            !output.TryGetProperty("rateReplyDetails", out var details) ||
            details.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Shipping carrier {Carrier} returned no rateReplyDetails: {Body}", CarrierName, Truncate(body));
            errors?.Add(new CarrierTestLine("Rate", null, "Response carried no rateReplyDetails."));
            return null;
        }

        var quotes = new List<FedExQuote>();
        foreach (var detail in details.EnumerateArray())
        {
            var serviceType = detail.TryGetProperty("serviceType", out var st) ? st.GetString() : null;
            var price = ReadPrice(detail, settings.RateMode);
            if (serviceType is not null && price is not null)
                quotes.Add(new FedExQuote(serviceType, price.Value, ReadTransitDays(detail)));
        }
        return quotes;
    }

    private sealed record TokenResult(string? Token, DateTimeOffset? ExpiresAt, string? Error);

    private async Task<TokenResult> GetAccessTokenAsync(
        HttpClient client, FedExSettings settings, CancellationToken ct, bool forceRefresh = false)
    {
        var key = $"{settings.BaseUrl}|{settings.ClientId}|{Hash(settings.ClientSecret)}";
        if (!forceRefresh && TokenCache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return new TokenResult(cached.Token, cached.ExpiresAt, null);

        var stopwatch = Stopwatch.StartNew();
        using var response = await SendWithRetryAsync(client, () => new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl}{TokenPath}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
            }),
        }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        stopwatch.Stop();

        if (settings.LoggingEnabled)
            _logger.LogInformation("FedEx OAuth token: HTTP {StatusCode} in {ElapsedMs} ms.",
                (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            var error = ReadError(body);
            _logger.LogWarning("Shipping carrier {Carrier} OAuth token call failed: HTTP {StatusCode}. {Error}",
                CarrierName, (int)response.StatusCode, error);
            return new TokenResult(null, null, $"HTTP {(int)response.StatusCode}: {error}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var token = root.TryGetProperty("access_token", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
            return new TokenResult(null, null, "Token response carried no access_token.");

        var lifetime = TimeSpan.FromHours(1);
        if (root.TryGetProperty("expires_in", out var e))
        {
            if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var seconds))
                lifetime = TimeSpan.FromSeconds(seconds);
            else if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), out var parsed))
                lifetime = TimeSpan.FromSeconds(parsed);
        }

        var expiresAt = DateTimeOffset.UtcNow + lifetime;
        TokenCache[key] = (token!, lifetime > TokenRefreshMargin ? expiresAt - TokenRefreshMargin : expiresAt);
        return new TokenResult(token, expiresAt, null);
    }

    // ------------------------------------------------------------------ request (FX-50)

    private static Dictionary<string, object?> BuildRequestBody(
        FedExSettings settings, CarrierRateRequest request, List<Package> packages, bool residential)
    {
        return new Dictionary<string, object?>
        {
            ["accountNumber"] = new { value = settings.AccountNumber },
            // returnTransitTimes populates `commit`, the only delivery estimate on this endpoint.
            ["rateRequestControlParameters"] = new { returnTransitTimes = true },
            ["requestedShipment"] = new Dictionary<string, object?>
            {
                ["shipper"] = new { address = Address(request.Origin, residential: false) },
                ["recipient"] = new { address = Address(request.Destination, residential) },
                ["pickupType"] = settings.PickupType,
                // FX-13: only the configured rate type, so the price read is never the other one.
                ["rateRequestType"] = new[] { settings.RateMode },
                ["preferredCurrency"] = "USD",
                ["totalPackageCount"] = packages.Count,
                ["requestedPackageLineItems"] = packages.Select(PackageLineItem).ToList(),
            },
        };
    }

    /// <summary>FX-16 / FX-T8: weight in LB; dimensions in whole inches (FedEx takes integers), when known.</summary>
    private static object PackageLineItem(Package p)
    {
        var item = new Dictionary<string, object?>
        {
            ["weight"] = new { units = "LB", value = p.WeightLb },
        };
        if (p.HasDimensions)
            item["dimensions"] = new
            {
                length = (int)Math.Ceiling(p.LengthIn!.Value),
                width = (int)Math.Ceiling(p.WidthIn!.Value),
                height = (int)Math.Ceiling(p.HeightIn!.Value),
                units = "IN",
            };
        return item;
    }

    /// <summary>FX-18: every street line; normalized 2-letter state; the residential flag (FX-17).</summary>
    private static Dictionary<string, object?> Address(CarrierAddress address, bool residential)
    {
        var lines = new[] { address.Line1, address.Line2 }
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l!.Trim())
            .ToArray();

        var result = new Dictionary<string, object?>
        {
            ["postalCode"] = address.PostalCode?.Trim() ?? string.Empty,
            ["countryCode"] = NormalizeCountry(address.CountryCode),
            ["stateOrProvinceCode"] = RegionCodeNormalizer.ToStateCode(address.CountryCode, address.Region) ?? string.Empty,
            ["residential"] = residential,
        };
        if (lines.Length > 0)
            result["streetLines"] = lines;
        if (!string.IsNullOrWhiteSpace(address.City))
            result["city"] = address.City.Trim();
        return result;
    }

    // ------------------------------------------------------------------ response

    /// <summary>
    /// FX-13 / FX-51: the shipment-level totalNetCharge of the detail whose rateType matches the configured
    /// mode. FedEx may label account rates "PREFERRED_ACCOUNT_SHIPMENT" or similar, so an exact match wins,
    /// then a prefix match, then — since only one type was requested — the first detail.
    /// </summary>
    private static decimal? ReadPrice(JsonElement detail, string rateMode)
    {
        if (!detail.TryGetProperty("ratedShipmentDetails", out var rated) || rated.ValueKind != JsonValueKind.Array)
            return null;

        var candidates = rated.EnumerateArray()
            .Select(r => (Type: r.TryGetProperty("rateType", out var t) ? t.GetString() ?? "" : "", Charge: ReadCharge(r)))
            .Where(c => c.Charge is not null)
            .ToList();

        var exact = candidates.FirstOrDefault(c => c.Type.Equals(rateMode, StringComparison.OrdinalIgnoreCase));
        if (exact.Charge is not null)
            return exact.Charge;
        var partial = candidates.FirstOrDefault(c => c.Type.Contains(rateMode, StringComparison.OrdinalIgnoreCase));
        if (partial.Charge is not null)
            return partial.Charge;
        return candidates.Count > 0 ? candidates[0].Charge : null;
    }

    private static decimal? ReadCharge(JsonElement ratedShipment)
    {
        if (!ratedShipment.TryGetProperty("totalNetCharge", out var charge))
            return null;
        if (charge.ValueKind == JsonValueKind.Number && charge.TryGetDecimal(out var n))
            return n;
        return charge.ValueKind == JsonValueKind.String &&
               decimal.TryParse(charge.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s)
            ? s
            : null;
    }

    /// <summary>
    /// FedEx expresses transit as a word enum ("TWO_DAYS") on <c>commit.transitDays.minimumTransitTime</c> or
    /// <c>operationalDetail.transitTime</c>, or as prose in <c>commit.transitDays.description</c>. Null when absent.
    /// </summary>
    private static int? ReadTransitDays(JsonElement detail)
    {
        if (detail.TryGetProperty("commit", out var commit) &&
            commit.TryGetProperty("transitDays", out var transitDays))
        {
            if (transitDays.TryGetProperty("minimumTransitTime", out var minimum) &&
                ParseTransitWord(minimum.GetString()) is { } fromWord)
                return fromWord;

            if (transitDays.TryGetProperty("description", out var description) &&
                ParseLeadingInt(description.GetString()) is { } fromText)
                return fromText;
        }

        if (detail.TryGetProperty("operationalDetail", out var operational) &&
            operational.TryGetProperty("transitTime", out var transitTime) &&
            ParseTransitWord(transitTime.GetString()) is { } fromOperational)
            return fromOperational;

        return null;
    }

    private static readonly string[] TransitWords =
    {
        "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN",
        "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN", "TWENTY",
    };

    /// <summary>Maps "ONE_DAY", "TWO_DAYS", … to a day count.</summary>
    private static int? ParseTransitWord(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return null;
        var index = Array.IndexOf(TransitWords, word.Split('_')[0].ToUpperInvariant());
        return index >= 0 ? index + 1 : null;
    }

    private static int? ParseLeadingInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var digits = new string(text.TrimStart().TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>FedEx errors: { "errors": [ { "code", "message" } ] }.</summary>
    private static string ReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                var messages = errors.EnumerateArray()
                    .Select(e => $"{(e.TryGetProperty("code", out var c) ? c.GetString() : null)} {(e.TryGetProperty("message", out var m) ? m.GetString() : null)}".Trim())
                    .Where(m => m.Length > 0)
                    .ToList();
                if (messages.Count > 0)
                    return string.Join("; ", messages);
            }
        }
        catch (JsonException)
        {
        }
        return Truncate(body);
    }

    private sealed record FedExQuote(string ServiceType, decimal Price, int? TransitDays);

    /// <summary>The resolved, defaulted settings of one <see cref="FedExCredential"/> row.</summary>
    private sealed record FedExSettings(
        string BaseUrl,
        string ClientId,
        string ClientSecret,
        string AccountNumber,
        string DisplayName,
        string RateMode,
        IReadOnlyList<string> AllowedServices,
        IReadOnlyList<string> FflAllowedServices,
        string PickupType,
        bool SendPackageInfo,
        (decimal L, decimal W, decimal H)? DefaultBox,
        decimal HandlingFee,
        decimal? MinimumOrderAmount,
        IReadOnlyList<string> ShipToCountries,
        bool LoggingEnabled)
    {
        /// <summary>
        /// Null when the row cannot quote: no host, client id, secret or account number (FedEx rejects an
        /// account-less rate request with a 403 that reads like bad credentials).
        /// </summary>
        public static FedExSettings? From(FedExCredential c)
        {
            if (string.IsNullOrWhiteSpace(c.BaseUrl) || string.IsNullOrWhiteSpace(c.ApiKey) ||
                string.IsNullOrWhiteSpace(c.ApiSecret) || string.IsNullOrWhiteSpace(c.AccountNumber))
                return null;

            // A row saved before the allowed-services setting existed offered every service; keep that.
            var allowed = c.AllowedServices is null
                ? FedExCodes.Services.Keys.ToList()
                : UspsMailClasses.SplitList(c.AllowedServices).Where(FedExCodes.Services.ContainsKey).ToList();
            var ffl = UspsMailClasses.SplitList(c.FflAllowedServices).Where(FedExCodes.Services.ContainsKey).ToList();

            var rateMode = c.RateMode?.Trim().ToUpperInvariant();
            var pickup = c.PickupType?.Trim().ToUpperInvariant();
            (decimal, decimal, decimal)? box = c.DefaultLengthIn is > 0m && c.DefaultWidthIn is > 0m && c.DefaultHeightIn is > 0m
                ? (c.DefaultLengthIn.Value, c.DefaultWidthIn.Value, c.DefaultHeightIn.Value)
                : null;

            return new FedExSettings(
                c.BaseUrl.Trim().TrimEnd('/'),
                c.ApiKey.Trim(),
                c.ApiSecret,
                c.AccountNumber.Trim(),
                string.IsNullOrWhiteSpace(c.DisplayName) ? "FedEx" : c.DisplayName.Trim(),
                rateMode is not null && FedExCodes.RateModes.Contains(rateMode) ? rateMode : FedExCodes.DefaultRateMode,
                allowed,
                ffl,
                pickup is not null && FedExCodes.PickupTypes.Contains(pickup) ? pickup : FedExCodes.DefaultPickupType,
                c.SendPackageInfo,
                box,
                c.HandlingFee ?? 0m,
                c.MinimumOrderAmount,
                UspsMailClasses.SplitList(c.ShipToCountries).Select(NormalizeCountry).ToList(),
                c.LoggingEnabled);
        }
    }
}
