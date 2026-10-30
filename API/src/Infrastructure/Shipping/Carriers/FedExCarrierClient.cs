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
/// FedEx rating adapter (WO-41), built structurally against the FedEx REST Rate API. The credential is
/// resolved from the credential vault under the "fedex" service-type key and is expected to be a JSON
/// document: { "apiKey" (or "clientId"), "secretKey", "accountNumber", "baseUrl" (optional) }. FedEx uses
/// OAuth2 client-credentials, so this first exchanges the key/secret for a bearer token, then calls the
/// rate endpoint. When no credential is configured — or any call fails — an empty list is returned so the
/// aggregating service simply excludes FedEx (AC-SHP-001.3).
/// </summary>
/// <remarks>
/// The request/response mapping is real, but returning live quotes requires valid FedEx API credentials
/// (key/secret + account number) and network access to a live FedEx endpoint.
/// </remarks>
public class FedExCarrierClient : ICarrierClient
{
    private const string CredentialKey = "fedex";
    private const string TokenPath = "/oauth/token";
    private const string RatePath = "/rate/v1/rates/quotes";
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialVault _vault;
    private readonly ILogger<FedExCarrierClient> _logger;

    public FedExCarrierClient(
        IHttpClientFactory httpClientFactory, ICredentialVault vault, ILogger<FedExCarrierClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _vault = vault;
        _logger = logger;
    }

    public string CarrierName => "FedEx";
    public ShippingCarrierType Carrier => ShippingCarrierType.FedEx;

    public async Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
    {
        var raw = await _vault.GetCredentialAsync(CredentialKey, ct);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<ShippingRateOption>();

        var credential = FedExCredential.Parse(raw);
        if (string.IsNullOrWhiteSpace(credential.ApiKey) || string.IsNullOrWhiteSpace(credential.SecretKey))
            return Array.Empty<ShippingRateOption>();

        // The endpoint is configuration, not a constant: sandbox and live are different hosts, and only the
        // credential knows which account it belongs to. No fallback — guessing sends sandbox keys to the
        // live host, which fails with an opaque 403.
        if (string.IsNullOrWhiteSpace(credential.BaseUrl))
            throw new InvalidOperationException(
                "The active FedEx credential has no Base URL. Set it on the integration (Integrations → FedEx).");

        try
        {
            var client = _httpClientFactory.CreateClient("fedex");
            client.Timeout = HttpTimeout;
            var baseUrl = credential.BaseUrl!.TrimEnd('/');

            var token = await GetAccessTokenAsync(client, baseUrl, credential, ct);
            if (string.IsNullOrWhiteSpace(token))
                return Array.Empty<ShippingRateOption>();

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{RatePath}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var payload = BuildRequestBody(request, credential);
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

    private async Task<string?> GetAccessTokenAsync(HttpClient client, string baseUrl, FedExCredential credential, CancellationToken ct)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}{TokenPath}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = credential.ApiKey!,
                ["client_secret"] = credential.SecretKey!,
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

    private static object BuildRequestBody(CarrierRateRequest request, FedExCredential credential) => new
    {
        accountNumber = new { value = credential.AccountNumber ?? string.Empty },
        // returnTransitTimes populates the `commit` node, which is the only source of a delivery estimate
        // on this endpoint; without it every FedEx option would score as unknown-speed.
        rateRequestControlParameters = new { returnTransitTimes = true },
        requestedShipment = new
        {
            shipper = new { address = BuildAddress(request.Origin) },
            recipient = new { address = BuildAddress(request.Destination) },
            pickupType = "USE_SCHEDULED_PICKUP",
            rateRequestType = new[] { "ACCOUNT", "LIST" },
            requestedPackageLineItems = new[]
            {
                new { weight = new { units = "KG", value = request.WeightKg } },
            },
        },
    };

    /// <summary>
    /// FedEx rates against the 2-letter state code and rejects the display name the address form stores,
    /// so the region is normalized here rather than sent as typed.
    /// </summary>
    private static object BuildAddress(CarrierAddress address) => new
    {
        postalCode = address.PostalCode ?? string.Empty,
        countryCode = address.CountryCode ?? string.Empty,
        stateOrProvinceCode = RegionCodeNormalizer.ToStateCode(address.CountryCode, address.Region) ?? string.Empty,
    };

    private static IReadOnlyList<ShippingRateOption> MapResponse(JsonElement root)
    {
        var options = new List<ShippingRateOption>();
        if (!root.TryGetProperty("output", out var output) ||
            !output.TryGetProperty("rateReplyDetails", out var details) ||
            details.ValueKind != JsonValueKind.Array)
            return options;

        foreach (var detail in details.EnumerateArray())
        {
            var serviceType = detail.TryGetProperty("serviceType", out var st) ? st.GetString() : null;

            decimal? rate = null;
            if (detail.TryGetProperty("ratedShipmentDetails", out var rated) && rated.ValueKind == JsonValueKind.Array)
            {
                foreach (var rs in rated.EnumerateArray())
                {
                    if (rs.TryGetProperty("totalNetCharge", out var charge) &&
                        charge.TryGetDecimal(out var value))
                    {
                        rate = value;
                        break;
                    }
                }
            }

            if (rate is null)
                continue;

            options.Add(new ShippingRateOption(
                MethodId: serviceType ?? "FedEx",
                Name: serviceType is null ? "FedEx" : $"FedEx {serviceType.Replace('_', ' ')}",
                Carrier: "FedEx",
                EstimatedDeliveryDays: ReadTransitDays(detail),
                Rate: rate.Value));
        }

        return options;
    }

    /// <summary>
    /// Reads a transit estimate from a rate reply. FedEx expresses it as a word enum ("TWO_DAYS") on either
    /// <c>commit.transitDays.minimumTransitTime</c> or <c>operationalDetail.transitTime</c>, and repeats it
    /// as prose in <c>commit.transitDays.description</c> ("2 business days"). Returns null when absent —
    /// some services are unrated for transit, and the caller treats null as unknown rather than instant.
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

    /// <summary>Maps a FedEx transit-time enum word ("ONE_DAY", "TWO_DAYS", …) to a day count.</summary>
    private static int? ParseTransitWord(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return null;

        var number = word.Split('_')[0].ToUpperInvariant();
        return number switch
        {
            "ONE" => 1,
            "TWO" => 2,
            "THREE" => 3,
            "FOUR" => 4,
            "FIVE" => 5,
            "SIX" => 6,
            "SEVEN" => 7,
            "EIGHT" => 8,
            "NINE" => 9,
            "TEN" => 10,
            "ELEVEN" => 11,
            "TWELVE" => 12,
            "THIRTEEN" => 13,
            "FOURTEEN" => 14,
            "FIFTEEN" => 15,
            "SIXTEEN" => 16,
            "SEVENTEEN" => 17,
            "EIGHTEEN" => 18,
            "NINETEEN" => 19,
            "TWENTY" => 20,
            _ => null,
        };
    }

    /// <summary>Pulls the leading integer out of prose like "2 business days".</summary>
    private static int? ParseLeadingInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var digits = new string(text.TrimStart().TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private sealed record FedExCredential(string? ApiKey, string? SecretKey, string? AccountNumber, string? BaseUrl)
    {
        public static FedExCredential Parse(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                return new FedExCredential(
                    GetString(root, "apiKey") ?? GetString(root, "clientId"),
                    GetString(root, "secretKey") ?? GetString(root, "clientSecret"),
                    GetString(root, "accountNumber"),
                    GetString(root, "baseUrl"));
            }
            catch (JsonException)
            {
                return new FedExCredential(null, null, null, null);
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
