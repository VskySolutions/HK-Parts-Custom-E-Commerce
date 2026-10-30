using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Common;

namespace HKPartsEcommerce.Infrastructure.Shipping.Carriers;

/// <summary>
/// UPS rating adapter (WO-40), built structurally against the UPS Rating REST API (/api/rating). The
/// credential is resolved from the credential vault under the "ups" service-type key and is expected to be
/// a JSON document: { "clientId", "clientSecret", "merchantId" (shipper number), "baseUrl" (optional) }.
/// UPS uses OAuth2 client-credentials, so this exchanges the client id/secret for a bearer token, then
/// calls the rate endpoint. When no credential is configured — or the live call fails for any reason — an
/// empty list is returned so the aggregating service simply excludes UPS (AC-SHP-001.3).
/// </summary>
/// <remarks>
/// This performs the real UPS OAuth + Rate request/response mapping, but returning actual quotes requires
/// valid UPS API credentials (client id/secret + merchant number) and network access to a live UPS endpoint.
/// </remarks>
public class UpsCarrierClient : ICarrierClient
{
    private const string CredentialKey = "ups";
    private const string TokenPath = "/security/v1/oauth/token";
    private const string RatingPath = "/api/rating/v2409/Rate";
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialVault _vault;
    private readonly ILogger<UpsCarrierClient> _logger;

    public UpsCarrierClient(
        IHttpClientFactory httpClientFactory, ICredentialVault vault, ILogger<UpsCarrierClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _vault = vault;
        _logger = logger;
    }

    public string CarrierName => "UPS";
    public ShippingCarrierType Carrier => ShippingCarrierType.UPS;

    public async Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
    {
        var raw = await _vault.GetCredentialAsync(CredentialKey, ct);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<ShippingRateOption>();

        var credential = UpsCredential.Parse(raw);
        if (string.IsNullOrWhiteSpace(credential.ClientId) || string.IsNullOrWhiteSpace(credential.ClientSecret))
            return Array.Empty<ShippingRateOption>();

        // The endpoint is configuration, not a constant: the CIE test environment and live are different
        // hosts, and only the credential knows which account it belongs to. No fallback — guessing sends
        // test keys to the live host, which fails with an opaque 4xx.
        if (string.IsNullOrWhiteSpace(credential.BaseUrl))
            throw new InvalidOperationException(
                "The active UPS credential has no Base URL. Set it on the integration (Integrations → UPS).");

        try
        {
            var client = _httpClientFactory.CreateClient("ups");
            client.Timeout = HttpTimeout;
            var baseUrl = credential.BaseUrl!.TrimEnd('/');

            var token = await GetAccessTokenAsync(client, baseUrl, credential, ct);
            if (string.IsNullOrWhiteSpace(token))
                return Array.Empty<ShippingRateOption>();

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{RatingPath}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = BuildRequestBody(request, credential);
            var body = JsonSerializer.Serialize(payload, payload.GetType());
            httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

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
            // Network / auth / parse failures exclude UPS from the aggregate rather than fail the quote.
            _logger.LogWarning(ex, "Shipping carrier {Carrier} rate call threw.", CarrierName);
            return Array.Empty<ShippingRateOption>();
        }
    }

    private async Task<string?> GetAccessTokenAsync(HttpClient client, string baseUrl, UpsCredential credential, CancellationToken ct)
    {
        // UPS OAuth2 client-credentials: HTTP Basic auth (clientId:clientSecret) + grant_type body.
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{TokenPath}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }),
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.ClientId}:{credential.ClientSecret}"));
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

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

    private static object BuildRequestBody(CarrierRateRequest request, UpsCredential credential) => new
    {
        RateRequest = new
        {
            // "Shoptimeintransit" is "Shop" plus delivery estimates. Plain "Shop" only populates
            // GuaranteedDelivery, so non-guaranteed services (Ground) would come back unknown-speed.
            Request = new { RequestOption = "Shoptimeintransit" },
            Shipment = new
            {
                Shipper = new
                {
                    ShipperNumber = credential.MerchantId ?? string.Empty,
                    Address = BuildAddress(request.Origin),
                },
                ShipFrom = new { Address = BuildAddress(request.Origin) },
                ShipTo = new { Address = BuildAddress(request.Destination) },
                DeliveryTimeInformation = new { PackageBillType = "03" },
                Package = new
                {
                    PackagingType = new { Code = "02" },
                    PackageWeight = new
                    {
                        UnitOfMeasurement = new { Code = "KGS" },
                        Weight = request.WeightKg.ToString(CultureInfo.InvariantCulture),
                    },
                },
            },
        },
    };

    /// <summary>
    /// UPS rates against the 2-letter state code and rejects the display name the address form stores, so
    /// the region is normalized here rather than sent as typed.
    /// </summary>
    private static object BuildAddress(CarrierAddress address) => new
    {
        PostalCode = address.PostalCode ?? string.Empty,
        StateProvinceCode = RegionCodeNormalizer.ToStateCode(address.CountryCode, address.Region) ?? string.Empty,
        CountryCode = address.CountryCode ?? string.Empty,
    };

    private static IReadOnlyList<ShippingRateOption> MapResponse(JsonElement root)
    {
        var options = new List<ShippingRateOption>();
        if (!root.TryGetProperty("RateResponse", out var rateResponse) ||
            !rateResponse.TryGetProperty("RatedShipment", out var rated))
            return options;

        // UPS returns RatedShipment as an object for a single service or an array for several.
        if (rated.ValueKind == JsonValueKind.Array)
        {
            foreach (var shipment in rated.EnumerateArray())
                AddOption(shipment, options);
        }
        else if (rated.ValueKind == JsonValueKind.Object)
        {
            AddOption(rated, options);
        }

        return options;
    }

    private static void AddOption(JsonElement shipment, List<ShippingRateOption> options)
    {
        string? serviceCode = null;
        if (shipment.TryGetProperty("Service", out var service) &&
            service.TryGetProperty("Code", out var code))
            serviceCode = code.GetString();

        decimal? rate = null;
        if (shipment.TryGetProperty("TotalCharges", out var charges) &&
            charges.TryGetProperty("MonetaryValue", out var monetary) &&
            monetary.ValueKind == JsonValueKind.String &&
            decimal.TryParse(monetary.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            rate = value;

        if (rate is null)
            return;

        options.Add(new ShippingRateOption(
            MethodId: serviceCode ?? "UPS",
            Name: serviceCode is null ? "UPS" : $"UPS {serviceCode}",
            Carrier: "UPS",
            EstimatedDeliveryDays: ReadTransitDays(shipment),
            Rate: rate.Value));
    }

    /// <summary>
    /// Reads a transit estimate from a rated shipment. <c>GuaranteedDelivery.BusinessDaysInTransit</c> is
    /// only present on guaranteed services, so fall back to the TimeInTransit node that
    /// RequestOption "Shoptimeintransit" adds for the rest. Returns null when neither is present.
    /// </summary>
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

    /// <summary>UPS returns numbers as JSON strings; tolerate both.</summary>
    private static int? ParseInt(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String when int.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s) => s,
        JsonValueKind.Number when element.TryGetInt32(out var n) => n,
        _ => null,
    };

    private sealed record UpsCredential(string? ClientId, string? ClientSecret, string? MerchantId, string? BaseUrl)
    {
        public static UpsCredential Parse(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                return new UpsCredential(
                    GetString(root, "clientId"),
                    GetString(root, "clientSecret"),
                    GetString(root, "merchantId") ?? GetString(root, "accountNumber"),
                    GetString(root, "baseUrl"));
            }
            catch (JsonException)
            {
                return new UpsCredential(null, null, null, null);
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
