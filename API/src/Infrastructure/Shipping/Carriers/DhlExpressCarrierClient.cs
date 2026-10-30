using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Shipping.Carriers;

/// <summary>
/// DHL Express rating adapter (WO-40), built structurally against the MyDHL API "rates" endpoint.
/// Credentials are resolved from the credential vault under the "dhl" service-type key and are expected
/// to be a JSON document: { "apiKey", "apiSecret", "accountNumber", "baseUrl" (optional) }; a bare
/// string is treated as the API key. When no credential is configured — or the live call fails for any
/// reason — an empty list is returned so the aggregating service simply excludes DHL (AC-SHP-001.3).
/// </summary>
/// <remarks>
/// This performs the real MyDHL request/response mapping, but returning actual quotes requires valid
/// DHL Express API credentials and network access to a live MyDHL endpoint.
/// </remarks>
public class DhlExpressCarrierClient : ICarrierClient
{
    private const string CredentialKey = "dhl";
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialVault _vault;
    private readonly ILogger<DhlExpressCarrierClient> _logger;

    public DhlExpressCarrierClient(
        IHttpClientFactory httpClientFactory, ICredentialVault vault, ILogger<DhlExpressCarrierClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _vault = vault;
        _logger = logger;
    }

    public string CarrierName => "DHL Express";
    public ShippingCarrierType Carrier => ShippingCarrierType.DHLExpress;

    public async Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
    {
        var raw = await _vault.GetCredentialAsync(CredentialKey, ct);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<ShippingRateOption>();

        var credential = DhlCredential.Parse(raw);
        if (string.IsNullOrWhiteSpace(credential.ApiKey))
            return Array.Empty<ShippingRateOption>();

        // The endpoint is configuration, not a constant: sandbox and live are different hosts, and only the
        // credential knows which account it belongs to. No fallback — guessing sends sandbox keys to the
        // live host, which fails with an opaque 4xx.
        if (string.IsNullOrWhiteSpace(credential.BaseUrl))
            throw new InvalidOperationException(
                "The active DHL Express credential has no Base URL. Set it on the integration (Integrations → DHL Express).");

        try
        {
            var client = _httpClientFactory.CreateClient("dhl-express");
            client.Timeout = HttpTimeout;

            var baseUrl = credential.BaseUrl!.TrimEnd('/');
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/rates");

            // MyDHL API uses HTTP Basic auth (apiKey:apiSecret).
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.ApiKey}:{credential.ApiSecret}"));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);

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
            // Network / auth / parse failures exclude DHL from the aggregate rather than fail the quote.
            _logger.LogWarning(ex, "Shipping carrier {Carrier} rate call threw.", CarrierName);
            return Array.Empty<ShippingRateOption>();
        }
    }

    private static object BuildRequestBody(CarrierRateRequest request, DhlCredential credential) => new
    {
        customerDetails = new
        {
            shipperDetails = new
            {
                postalCode = request.Origin.PostalCode,
                cityName = request.Origin.City,
                countryCode = request.Origin.CountryCode,
            },
            receiverDetails = new
            {
                postalCode = request.Destination.PostalCode,
                cityName = request.Destination.City,
                countryCode = request.Destination.CountryCode,
            },
        },
        accounts = new[]
        {
            new { typeCode = "shipper", number = credential.AccountNumber ?? string.Empty },
        },
        plannedShippingDateAndTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss 'GMT+00:00'"),
        unitOfMeasurement = "metric",
        isCustomsDeclarable = false,
        packages = new[]
        {
            new
            {
                weight = request.WeightKg,
                dimensions = new
                {
                    length = request.Length ?? 1m,
                    width = request.Width ?? 1m,
                    height = request.Height ?? 1m,
                },
            },
        },
    };

    private static IReadOnlyList<ShippingRateOption> MapResponse(JsonElement root)
    {
        var options = new List<ShippingRateOption>();
        if (!root.TryGetProperty("products", out var products) || products.ValueKind != JsonValueKind.Array)
            return options;

        foreach (var product in products.EnumerateArray())
        {
            var name = product.TryGetProperty("productName", out var pn) ? pn.GetString() : null;
            var code = product.TryGetProperty("productCode", out var pc) ? pc.GetString() : null;

            decimal? price = null;
            if (product.TryGetProperty("totalPrice", out var totals) && totals.ValueKind == JsonValueKind.Array)
            {
                foreach (var total in totals.EnumerateArray())
                {
                    if (total.TryGetProperty("price", out var p) &&
                        p.ValueKind == JsonValueKind.Number &&
                        p.TryGetDecimal(out var value))
                    {
                        price = value;
                        break;
                    }
                }
            }

            int? transitDays = null;
            if (product.TryGetProperty("deliveryCapabilities", out var caps) &&
                caps.TryGetProperty("totalTransitDays", out var days) &&
                days.ValueKind == JsonValueKind.Number &&
                days.TryGetInt32(out var d))
                transitDays = d;

            if (price is null)
                continue;

            options.Add(new ShippingRateOption(
                MethodId: code ?? name ?? "DHL",
                Name: name ?? "DHL Express",
                Carrier: "DHL Express",
                EstimatedDeliveryDays: transitDays,
                Rate: price.Value));
        }

        return options;
    }

    private sealed record DhlCredential(string? ApiKey, string? ApiSecret, string? AccountNumber, string? BaseUrl)
    {
        public static DhlCredential Parse(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                return new DhlCredential(
                    GetString(root, "apiKey") ?? GetString(root, "username"),
                    GetString(root, "apiSecret") ?? GetString(root, "password"),
                    GetString(root, "accountNumber"),
                    GetString(root, "baseUrl"));
            }
            catch (JsonException)
            {
                // Not JSON: treat the whole credential value as the API key.
                return new DhlCredential(raw, null, null, null);
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
