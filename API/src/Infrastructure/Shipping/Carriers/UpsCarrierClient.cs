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
/// UPS rating adapter against the UPS REST Rating API with HK Parts' own OAuth client-credentials app
/// (docs/ups-shipping-requirements.md). Settings come from the single active <see cref="UpsCredential"/>
/// row: host (CIE or production), client id/secret, shipper number, API version, allowed and FFL services,
/// negotiated rates, package and pickup type, per-unit package info, handling fee, minimum order, ship-to
/// countries and logging.
/// <para>
/// One "Shoptimeintransit" request returns every service with transit times; the result is filtered to the
/// allowed services (and, for a cart holding FFL items, to the FFL list). Tokens are cached until 60 s
/// before expiry (UP-51), rates for 10 minutes (UP-56), and each call times out after 5 s with one retry on
/// 5xx (UP-55). Any failure yields no options, so the aggregate simply excludes UPS (UP-21).
/// </para>
/// </summary>
/// <remarks>Field shapes follow the UPS Rating API as understood at build time; confirm with the admin Test button.</remarks>
public class UpsCarrierClient : ICarrierClient, IUpsRater
{
    private const string TokenPath = "/security/v1/oauth/token";
    private const string RequestOption = "Shoptimeintransit";
    private const int MaxPackages = 200;
    private const int MaxNameLength = 35;

    /// <summary>Process-wide token cache keyed by host + client id + a hash of the secret (UP-51, UP-T10).</summary>
    private static readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> TokenCache = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UpsCarrierClient> _logger;

    public UpsCarrierClient(
        IHttpClientFactory httpClientFactory, IApplicationDbContext db, IMemoryCache cache, ILogger<UpsCarrierClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public string CarrierName => "UPS";
    public ShippingCarrierType Carrier => ShippingCarrierType.UPS;

    // ------------------------------------------------------------------ checkout

    public async Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
    {
        var entity = await _db.UpsCredentials.AsNoTracking()
            .Where(c => c.Active)
            .OrderByDescending(c => c.UpdatedOnUtc)
            .FirstOrDefaultAsync(ct);
        var settings = entity is null ? null : UpsSettings.From(entity);
        if (settings is null)
            return Array.Empty<ShippingRateOption>();

        if (settings.MinimumOrderAmount is { } min && request.OrderSubtotal < min)
            return Array.Empty<ShippingRateOption>();

        var destinationCountry = NormalizeCountry(request.Destination.CountryCode);
        if (settings.ShipToCountries.Count > 0 && !settings.ShipToCountries.Contains(destinationCountry))
            return Array.Empty<ShippingRateOption>();

        var quotes = await QuoteAsync(settings, request, useCache: true, errors: null, ct);
        return quotes
            .Select(q => new ShippingRateOption(
                MethodId: q.ServiceCode,
                Name: $"{settings.DisplayName} - {UpsCodes.ServiceName(q.ServiceCode)}",
                Carrier: CarrierName,
                EstimatedDeliveryDays: q.TransitDays,
                Rate: Math.Round(q.Price + settings.HandlingFee, 2, MidpointRounding.AwayFromZero),
                HandlingFee: settings.HandlingFee))
            .ToList();
    }

    // ------------------------------------------------------------------ admin test (UP-40, UP-41)

    public async Task<CarrierTestResult> TestCredentialAsync(Guid credentialId, CancellationToken ct)
    {
        var entity = await _db.UpsCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == credentialId, ct)
            ?? throw new NotFoundException(nameof(UpsCredential), credentialId);

        var settings = UpsSettings.From(entity);
        if (settings is null)
            return new CarrierTestResult(false, "Base URL, client ID and client secret are all required.",
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
            return new CarrierTestResult(false, token.Error ?? "UPS refused the client ID / secret.",
                Array.Empty<CarrierTestLine>(), Array.Empty<CarrierTestLine>());

        // UP-T1: 5 lb residential parcel, Charlotte 28270 → New York 10001.
        var request = new CarrierRateRequest(
            new CarrierAddress("US", "NC", "28270", null, null, "Charlotte", Name: "HK Parts"),
            new CarrierAddress("US", "NY", "10001", null, null, "New York", Name: "Test Recipient"),
            5m / PoundsPerKilogram, null, null, null, 100m);

        var errors = new List<CarrierTestLine>();
        var quotes = await QuoteAsync(settings, request, useCache: false, errors, ct);
        var lines = quotes
            .Select(q => new CarrierTestLine(UpsCodes.ServiceName(q.ServiceCode), q.Price, null))
            .Concat(errors)
            .ToList();

        return new CarrierTestResult(true, null, lines, Array.Empty<CarrierTestLine>(),
            "5 lb, residential, 28270 → 10001", null, token.ExpiresAt);
    }

    // ------------------------------------------------------------------ core

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient("ups");
        client.Timeout = HttpTimeout;
        return client;
    }

    private async Task<IReadOnlyList<UpsQuote>> QuoteAsync(
        UpsSettings settings, CarrierRateRequest request, bool useCache, List<CarrierTestLine>? errors, CancellationToken ct)
    {
        // UP-14 / UP-22 / UP-23: allowed services, narrowed by code (never by display text) for FFL carts.
        var hasFfl = request.Lines?.Any(l => l.IsFfl) == true;
        var services = hasFfl
            ? settings.AllowedServices.Where(settings.FflAllowedServices.Contains).ToList()
            : settings.AllowedServices.ToList();
        if (services.Count == 0)
        {
            errors?.Add(new CarrierTestLine("Services", null, "No allowed service (check Allowed / FFL services)."));
            return Array.Empty<UpsQuote>();
        }

        var packages = BuildPackages(settings, request);
        if (packages is null)
            return Array.Empty<UpsQuote>(); // UP-19: nothing to rate

        try
        {
            var client = CreateHttpClient();
            var token = await GetAccessTokenAsync(client, settings, ct);
            if (token.Token is null)
            {
                errors?.Add(new CarrierTestLine("OAuth token", null, token.Error ?? "UPS refused the client ID / secret."));
                return Array.Empty<UpsQuote>();
            }

            // UP-12: an FFL cart ships to the dealer — a business — everything else to a residence.
            var residential = !hasFfl;
            var body = BuildRequestBody(settings, request, packages, residential);
            var json = JsonSerializer.Serialize(body);

            var cacheKey = "ups-rate:" + Hash($"{settings.BaseUrl}|{settings.ApiVersion}|{json}");
            if (!useCache || !_cache.TryGetValue(cacheKey, out List<UpsQuote>? all) || all is null)
            {
                all = await SendRateAsync(client, settings, token.Token, json, errors, ct);
                if (all is null)
                    return Array.Empty<UpsQuote>();
                if (useCache)
                    _cache.Set(cacheKey, all, RateCacheDuration);
            }

            return all.Where(q => services.Contains(q.ServiceCode)).OrderBy(q => q.Price).ToList();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Shipping carrier {Carrier} rate call threw.", CarrierName);
            errors?.Add(new CarrierTestLine("Request", null, ex.Message));
            return Array.Empty<UpsQuote>();
        }
    }

    /// <summary>Sends one Shop request; every rated service, or null on failure (logged).</summary>
    private async Task<List<UpsQuote>?> SendRateAsync(
        HttpClient client, UpsSettings settings, string token, string json, List<CarrierTestLine>? errors, CancellationToken ct)
    {
        var url = $"{settings.BaseUrl}/api/rating/{settings.ApiVersion}/{RequestOption}";
        var stopwatch = Stopwatch.StartNew();
        using var response = await SendWithRetryAsync(client, () =>
        {
            var message = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            // UP-50.
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            message.Headers.TryAddWithoutValidation("transId", Guid.NewGuid().ToString("N"));
            message.Headers.TryAddWithoutValidation("transactionSrc", "HKParts");
            return message;
        }, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        stopwatch.Stop();

        if (settings.LoggingEnabled)
        {
            // UP-57 / UP-T13: the body carries no secret — the bearer token travels only in the header.
            _logger.LogInformation(
                "UPS rate: HTTP {StatusCode} in {ElapsedMs} ms. Request: {Request} Response: {Response}",
                (int)response.StatusCode, stopwatch.ElapsedMilliseconds, json, Truncate(responseBody));
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = ReadError(responseBody);
            _logger.LogWarning(
                "Shipping carrier {Carrier} rate call failed: HTTP {StatusCode}. {Error}",
                CarrierName, (int)response.StatusCode, error);
            errors?.Add(new CarrierTestLine("Rate", null, $"HTTP {(int)response.StatusCode}: {error}"));
            return null;
        }

        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("RateResponse", out var rateResponse) ||
            !rateResponse.TryGetProperty("RatedShipment", out var rated))
        {
            // UP-21: no RateResponse is a failure, not an exception.
            _logger.LogWarning("Shipping carrier {Carrier} returned no RateResponse: {Body}", CarrierName, Truncate(responseBody));
            errors?.Add(new CarrierTestLine("Rate", null, "Response carried no RateResponse."));
            return null;
        }

        // UP-54: RatedShipment is an object for one service and an array for several.
        var shipments = rated.ValueKind == JsonValueKind.Array
            ? rated.EnumerateArray().ToList()
            : rated.ValueKind == JsonValueKind.Object ? new List<JsonElement> { rated } : new List<JsonElement>();

        var quotes = new List<UpsQuote>();
        foreach (var shipment in shipments)
        {
            var code = shipment.TryGetProperty("Service", out var service) && service.TryGetProperty("Code", out var c)
                ? c.GetString()
                : null;
            var price = ReadPrice(shipment, settings.UseNegotiatedRates);
            if (code is not null && price is not null)
                quotes.Add(new UpsQuote(code, price.Value, ReadTransitDays(shipment)));
        }
        return quotes;
    }


    private sealed record TokenResult(string? Token, DateTimeOffset? ExpiresAt, string? Error);

    /// <summary>UP-03: client-credentials grant with HTTP Basic auth. Cached until 60 s before expiry (UP-51).</summary>
    private async Task<TokenResult> GetAccessTokenAsync(
        HttpClient client, UpsSettings settings, CancellationToken ct, bool forceRefresh = false)
    {
        var key = $"{settings.BaseUrl}|{settings.ClientId}|{Hash(settings.ClientSecret)}";
        if (!forceRefresh && TokenCache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return new TokenResult(cached.Token, cached.ExpiresAt, null);

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.ClientSecret}"));
        var stopwatch = Stopwatch.StartNew();
        using var response = await SendWithRetryAsync(client, () =>
        {
            var message = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl}{TokenPath}")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }),
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            return message;
        }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        stopwatch.Stop();

        if (settings.LoggingEnabled)
            _logger.LogInformation("UPS OAuth token: HTTP {StatusCode} in {ElapsedMs} ms.",
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

        var lifetime = TimeSpan.FromHours(4);
        if (root.TryGetProperty("expires_in", out var e))
        {
            // UPS sends expires_in as a string of seconds.
            if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var seconds))
                lifetime = TimeSpan.FromSeconds(seconds);
            else if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), out var parsed))
                lifetime = TimeSpan.FromSeconds(parsed);
        }

        var expiresAt = DateTimeOffset.UtcNow + lifetime;
        var refreshAt = lifetime > TokenRefreshMargin ? expiresAt - TokenRefreshMargin : expiresAt;
        TokenCache[key] = (token!, refreshAt);
        return new TokenResult(token, expiresAt, null);
    }

    // ------------------------------------------------------------------ request

    /// <summary>
    /// UP-13 / UP-19 / UP-20: per-unit packages when "send package info" is on, else one package of the total
    /// weight; weights rounded up to 0.1 lb; null when the shipment weighs nothing.
    /// </summary>
    private static List<Package>? BuildPackages(UpsSettings settings, CarrierRateRequest request)
        => CarrierRatingSupport.BuildPackages(request, settings.SendPackageInfo, defaultBox: null, MaxPackages);

    private static Dictionary<string, object?> BuildRequestBody(
        UpsSettings settings, CarrierRateRequest request, List<Package> packages, bool residential)
    {
        var shipment = new Dictionary<string, object?>
        {
            ["Shipper"] = Party(request.Origin, "Shipper", settings.ShipperNumber),
            ["ShipTo"] = Party(request.Destination, "Recipient", null, residential),
            ["ShipFrom"] = Party(request.Origin, "Shipper", null),
            // "Shoptimeintransit" needs these for the transit estimate.
            ["DeliveryTimeInformation"] = new { PackageBillType = "03" },
            ["ShipmentTotalWeight"] = new
            {
                UnitOfMeasurement = new { Code = "LBS" },
                Weight = Money(packages.Sum(p => p.WeightLb), 1),
            },
            ["NumOfPieces"] = packages.Count.ToString(CultureInfo.InvariantCulture),
            ["Package"] = packages.Select(p => BuildPackage(settings, p)).ToList(),
        };

        // UP-52: transportation charges billed to the shipper account.
        if (!string.IsNullOrWhiteSpace(settings.ShipperNumber))
            shipment["PaymentDetails"] = new
            {
                ShipmentCharge = new[] { new { Type = "01", BillShipper = new { AccountNumber = settings.ShipperNumber } } },
            };

        // UP-53: only when negotiated rates are on.
        if (settings.UseNegotiatedRates)
            shipment["ShipmentRatingOptions"] = new { NegotiatedRatesIndicator = "" };

        // International time-in-transit needs the declared value.
        if (NormalizeCountry(request.Origin.CountryCode) != NormalizeCountry(request.Destination.CountryCode))
            shipment["InvoiceLineTotal"] = new { CurrencyCode = "USD", MonetaryValue = Money(request.OrderSubtotal, 2) };

        return new Dictionary<string, object?>
        {
            ["RateRequest"] = new Dictionary<string, object?>
            {
                ["Request"] = new { RequestOption },
                ["PickupType"] = new { Code = settings.PickupType },
                ["Shipment"] = shipment,
            },
        };
    }

    private static object BuildPackage(UpsSettings settings, Package p)
    {
        var package = new Dictionary<string, object?>
        {
            ["PackagingType"] = new { Code = settings.PackageType },
            ["PackageWeight"] = new { UnitOfMeasurement = new { Code = "LBS" }, Weight = Money(p.WeightLb, 1) },
        };
        if (p.LengthIn is not null && p.WidthIn is not null && p.HeightIn is not null)
            package["Dimensions"] = new
            {
                UnitOfMeasurement = new { Code = "IN" },
                Length = Money(p.LengthIn.Value, 1),
                Width = Money(p.WidthIn.Value, 1),
                Height = Money(p.HeightIn.Value, 1),
            };
        return package;
    }

    /// <summary>
    /// UP-18: real name, each address line once, city, normalized state code. A residential ship-to carries
    /// the (empty-valued) ResidentialAddressIndicator — UPS reads its presence, not its value.
    /// </summary>
    private static Dictionary<string, object?> Party(CarrierAddress address, string fallbackName, string? shipperNumber, bool residential = false)
    {
        var lines = new[] { address.Line1, address.Line2 }
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l!.Trim())
            .ToArray();

        var addr = new Dictionary<string, object?>
        {
            ["PostalCode"] = address.PostalCode?.Trim() ?? string.Empty,
            ["StateProvinceCode"] = RegionCodeNormalizer.ToStateCode(address.CountryCode, address.Region) ?? string.Empty,
            ["CountryCode"] = NormalizeCountry(address.CountryCode),
        };
        if (lines.Length > 0)
            addr["AddressLine"] = lines;
        if (!string.IsNullOrWhiteSpace(address.City))
            addr["City"] = address.City.Trim();
        if (residential)
            addr["ResidentialAddressIndicator"] = "";

        var name = string.IsNullOrWhiteSpace(address.Name) ? fallbackName : address.Name.Trim();
        var party = new Dictionary<string, object?>
        {
            ["Name"] = name.Length > MaxNameLength ? name[..MaxNameLength] : name,
            ["Address"] = addr,
        };
        if (!string.IsNullOrWhiteSpace(shipperNumber))
            party["ShipperNumber"] = shipperNumber;
        return party;
    }

    // ------------------------------------------------------------------ response

    /// <summary>
    /// UP-15 / UP-17: the shipment total (never a package's), negotiated when on and returned, else published.
    /// </summary>
    private static decimal? ReadPrice(JsonElement shipment, bool negotiated)
    {
        if (negotiated &&
            shipment.TryGetProperty("NegotiatedRateCharges", out var neg) &&
            neg.TryGetProperty("TotalCharge", out var negTotal) &&
            ReadMoney(negTotal) is { } negotiatedPrice)
            return negotiatedPrice;

        return shipment.TryGetProperty("TotalCharges", out var total) ? ReadMoney(total) : null;
    }

    private static decimal? ReadMoney(JsonElement charge)
    {
        if (!charge.TryGetProperty("MonetaryValue", out var value))
            return null;
        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s))
            return s;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n) ? n : null;
    }

    /// <summary>GuaranteedDelivery for guaranteed services, else the TimeInTransit estimate "Shoptimeintransit" adds.</summary>
    private static int? ReadTransitDays(JsonElement shipment)
    {
        if (shipment.TryGetProperty("GuaranteedDelivery", out var guaranteed) &&
            guaranteed.TryGetProperty("BusinessDaysInTransit", out var transit) &&
            ParseInt(transit) is { } guaranteedDays)
            return guaranteedDays;

        if (shipment.TryGetProperty("TimeInTransit", out var timeInTransit) &&
            timeInTransit.TryGetProperty("ServiceSummary", out var summary) &&
            summary.TryGetProperty("EstimatedArrival", out var arrival) &&
            arrival.TryGetProperty("BusinessDaysInTransit", out var businessDays) &&
            ParseInt(businessDays) is { } estimatedDays)
            return estimatedDays;

        return null;
    }

    private static int? ParseInt(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String when int.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s) => s,
        JsonValueKind.Number when element.TryGetInt32(out var n) => n,
        _ => null,
    };

    /// <summary>UPS errors: { "response": { "errors": [ { "code", "message" } ] } }.</summary>
    private static string ReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("response", out var response) &&
                response.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array)
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

    // ------------------------------------------------------------------ helpers

    private static string Money(decimal value, int decimals) => Fixed(value, decimals);

    private sealed record UpsQuote(string ServiceCode, decimal Price, int? TransitDays);

    /// <summary>The resolved, defaulted settings of one <see cref="UpsCredential"/> row.</summary>
    private sealed record UpsSettings(
        string BaseUrl,
        string ClientId,
        string ClientSecret,
        string? ShipperNumber,
        string ApiVersion,
        string DisplayName,
        IReadOnlyList<string> AllowedServices,
        IReadOnlyList<string> FflAllowedServices,
        bool UseNegotiatedRates,
        string PackageType,
        bool SendPackageInfo,
        string PickupType,
        decimal HandlingFee,
        decimal? MinimumOrderAmount,
        IReadOnlyList<string> ShipToCountries,
        bool LoggingEnabled)
    {
        /// <summary>Null when the row cannot authenticate (no host, client id or secret).</summary>
        public static UpsSettings? From(UpsCredential c)
        {
            if (string.IsNullOrWhiteSpace(c.BaseUrl) || string.IsNullOrWhiteSpace(c.ClientId) ||
                string.IsNullOrWhiteSpace(c.ClientSecret))
                return null;

            // A row saved before the allowed-services setting existed offered every service; keep that.
            var allowed = c.AllowedServices is null
                ? UpsCodes.Services.Keys.ToList()
                : UpsCodes.SplitList(c.AllowedServices).Where(UpsCodes.Services.ContainsKey).ToList();
            var ffl = c.FflAllowedServices is null
                ? UpsCodes.DefaultFflServices.ToList()
                : UpsCodes.SplitList(c.FflAllowedServices).Where(UpsCodes.Services.ContainsKey).ToList();

            return new UpsSettings(
                c.BaseUrl.Trim().TrimEnd('/'),
                c.ClientId.Trim(),
                c.ClientSecret,
                string.IsNullOrWhiteSpace(c.MerchantId) ? null : c.MerchantId.Trim(),
                string.IsNullOrWhiteSpace(c.ApiVersion) ? UpsCodes.DefaultApiVersion : c.ApiVersion.Trim(),
                string.IsNullOrWhiteSpace(c.DisplayName) ? "UPS" : c.DisplayName.Trim(),
                allowed,
                ffl,
                c.UseNegotiatedRates,
                c.PackageType is { } pt && UpsCodes.PackageTypes.ContainsKey(pt) ? pt : UpsCodes.DefaultPackageType,
                c.SendPackageInfo,
                c.PickupType is { } pk && UpsCodes.PickupTypes.ContainsKey(pk) ? pk : UpsCodes.DefaultPickupType,
                c.HandlingFee ?? 0m,
                c.MinimumOrderAmount,
                UpsCodes.SplitList(c.ShipToCountries).Select(NormalizeCountry).ToList(),
                c.LoggingEnabled);
        }
    }
}
