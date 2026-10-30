using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Shipping.Carriers;

/// <summary>
/// USPS rating adapter (WO-41), built structurally against the modern USPS REST APIs (apis.usps.com).
/// The credential is resolved from the credential vault under the "usps" service-type key and is expected
/// to be a JSON document: { "consumerKey", "consumerSecret", "baseUrl" (optional) }. USPS uses OAuth2
/// client-credentials, so this exchanges the consumer key/secret for a bearer token, then calls the
/// base-rates search endpoint. When no credential is configured — or any call fails — an empty list is
/// returned so the aggregating service simply excludes USPS (AC-SHP-001.3).
/// </summary>
/// <remarks>
/// The request/response mapping is real, but returning live quotes requires valid USPS API credentials
/// and network access to a live USPS endpoint.
/// </remarks>
public class UspsCarrierClient : ICarrierClient
{
    private const string CredentialKey = "usps";
    private const string TokenPath = "/oauth2/v3/token";
    private const string RatePath = "/prices/v3/base-rates/search";
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialVault _vault;
    private readonly ILogger<UspsCarrierClient> _logger;

    public UspsCarrierClient(
        IHttpClientFactory httpClientFactory, ICredentialVault vault, ILogger<UspsCarrierClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _vault = vault;
        _logger = logger;
    }

    public string CarrierName => "USPS";
    public ShippingCarrierType Carrier => ShippingCarrierType.USPS;

    public async Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
    {
        var raw = await _vault.GetCredentialAsync(CredentialKey, ct);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<ShippingRateOption>();

        var credential = UspsCredential.Parse(raw);
        if (string.IsNullOrWhiteSpace(credential.ConsumerKey) || string.IsNullOrWhiteSpace(credential.ConsumerSecret))
            return Array.Empty<ShippingRateOption>();

        // USPS domestic rating is US-origin/US-destination only; skip non-US shipments cleanly.
        if (!IsUnitedStates(request.Origin) || !IsUnitedStates(request.Destination))
            return Array.Empty<ShippingRateOption>();

        // The endpoint is configuration, not a constant: the test environment and live are different hosts,
        // and only the credential knows which account it belongs to. No fallback — guessing sends test keys
        // to the live host, which fails with an opaque 4xx.
        if (string.IsNullOrWhiteSpace(credential.BaseUrl))
            throw new InvalidOperationException(
                "The active USPS credential has no Base URL. Set it on the integration (Integrations → USPS).");

        try
        {
            var client = _httpClientFactory.CreateClient("usps");
            client.Timeout = HttpTimeout;
            var baseUrl = credential.BaseUrl!.TrimEnd('/');

            var token = await GetAccessTokenAsync(client, baseUrl, credential, ct);
            if (string.IsNullOrWhiteSpace(token))
                return Array.Empty<ShippingRateOption>();

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{RatePath}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var payload = BuildRequestBody(request);
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, payload.GetType()), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                await CarrierHttpDiagnostics.LogFailedResponseAsync(_logger, CarrierName, "rate", response, ct);
                return Array.Empty<ShippingRateOption>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return MapResponse(document.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Shipping carrier {Carrier} rate call threw.", CarrierName);
            return Array.Empty<ShippingRateOption>();
        }
    }

    private async Task<string?> GetAccessTokenAsync(HttpClient client, string baseUrl, UspsCredential credential, CancellationToken ct)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{TokenPath}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = credential.ConsumerKey!,
                ["client_secret"] = credential.ConsumerSecret!,
            }),
        };

        using var tokenResponse = await client.SendAsync(tokenRequest, ct);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            await CarrierHttpDiagnostics.LogFailedResponseAsync(_logger, CarrierName, "OAuth token", tokenResponse, ct);
            return null;
        }

        await using var stream = await tokenResponse.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.TryGetProperty("access_token", out var token) ? token.GetString() : null;
    }

    private const decimal PoundsPerKilogram = 2.20462m;
    private const decimal CentimetresPerInch = 2.54m;

    // The catalog carries no per-product dimensions, so the checkout passes none and every quote would
    // arrive dimensionless. These stand in for a modest parcel: within the 15x18x22" limit that keeps a
    // shipment MACHINABLE, so the fallback never itself makes an otherwise ratable shipment unratable.
    private const decimal DefaultLengthInches = 12m;
    private const decimal DefaultWidthInches = 9m;
    private const decimal DefaultHeightInches = 6m;

    /// <summary>
    /// Builds the base-rates search body. USPS wants the whole parcel descriptor, not just the endpoints:
    /// weight, all three dimensions, processing category, rate indicator and entry-facility type are each
    /// required, and omitting any of them fails the call with 400 "One or more required field(s) are
    /// missing from the request" rather than returning a partial quote. It also rates in pounds and inches
    /// while <see cref="CarrierRateRequest"/> carries kilograms and centimetres.
    /// </summary>
    private static object BuildRequestBody(CarrierRateRequest request) => new
    {
        originZIPCode = FiveDigitZip(request.Origin.PostalCode),
        destinationZIPCode = FiveDigitZip(request.Destination.PostalCode),
        weight = Math.Round(request.WeightKg * PoundsPerKilogram, 2),
        length = ToInches(request.Length, DefaultLengthInches),
        width = ToInches(request.Width, DefaultWidthInches),
        height = ToInches(request.Height, DefaultHeightInches),
        mailClass = "USPS_GROUND_ADVANTAGE",
        // One retail parcel handed over at no particular entry facility — the combination that describes
        // an ordinary storefront shipment.
        processingCategory = "MACHINABLE",
        rateIndicator = "SP",
        destinationEntryFacilityType = "NONE",
        priceType = "RETAIL",
    };

    private static decimal ToInches(decimal? centimetres, decimal fallbackInches)
        => centimetres is > 0m ? Math.Round(centimetres.Value / CentimetresPerInch, 2) : fallbackInches;

    /// <summary>
    /// USPS rates against the 5-digit ZIP and rejects anything else, so a ZIP+4 ("93722-1234") or a
    /// space-padded value is reduced to its first five digits.
    /// </summary>
    private static string FiveDigitZip(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
            return string.Empty;

        var digits = new string(postalCode.Where(char.IsDigit).ToArray());
        return digits.Length >= 5 ? digits[..5] : digits;
    }

    private static IReadOnlyList<ShippingRateOption> MapResponse(JsonElement root)
    {
        var options = new List<ShippingRateOption>();

        // A base-rates search returns either a single rate object or a { rates: [...] } collection.
        if (root.TryGetProperty("rates", out var rates) && rates.ValueKind == JsonValueKind.Array)
        {
            foreach (var rate in rates.EnumerateArray())
                AddOption(rate, options);
        }
        else
        {
            AddOption(root, options);
        }

        return options;
    }

    private static void AddOption(JsonElement element, List<ShippingRateOption> options)
    {
        var mailClass = element.TryGetProperty("mailClass", out var mc) ? mc.GetString() : null;

        decimal? price = null;
        if (element.TryGetProperty("totalBasePrice", out var tbp) && tbp.TryGetDecimal(out var t))
            price = t;
        else if (element.TryGetProperty("price", out var p) && p.TryGetDecimal(out var pv))
            price = pv;

        if (price is null)
            return;

        options.Add(new ShippingRateOption(
            MethodId: mailClass ?? "USPS",
            Name: mailClass is null ? "USPS" : $"USPS {mailClass.Replace('_', ' ')}",
            Carrier: "USPS",
            EstimatedDeliveryDays: ReadTransitDays(element),
            Rate: price.Value));
    }

    /// <summary>
    /// Reads a transit estimate if the rate carries one.
    ///
    /// Caveat: the base-rates search is a pricing endpoint and generally does NOT return a delivery
    /// estimate — that lives on the separate Service Standards API (<c>/service-standards/v3/estimates</c>),
    /// which would need its own call keyed by origin/destination ZIP and mail class. This reads the fields a
    /// rate may optionally carry and otherwise returns null, so USPS options usually score as unknown-speed
    /// and fall back to the configured assumed transit days.
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

    private static bool IsUnitedStates(CarrierAddress address)
        => string.IsNullOrWhiteSpace(address.CountryCode)
           || address.CountryCode!.Equals("US", StringComparison.OrdinalIgnoreCase)
           || address.CountryCode!.Equals("USA", StringComparison.OrdinalIgnoreCase);

    private sealed record UspsCredential(string? ConsumerKey, string? ConsumerSecret, string? BaseUrl)
    {
        public static UspsCredential Parse(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                return new UspsCredential(
                    GetString(root, "consumerKey") ?? GetString(root, "clientId"),
                    GetString(root, "consumerSecret") ?? GetString(root, "clientSecret"),
                    GetString(root, "baseUrl"));
            }
            catch (JsonException)
            {
                return new UspsCredential(null, null, null);
            }
        }

        private static string? GetString(JsonElement root, string name)
            => root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty(name, out var el) &&
               el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;
    }
}
