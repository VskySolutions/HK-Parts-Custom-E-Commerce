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
/// Freightview LTL freight-quote adapter — the legacy site's <c>freightview</c> method
/// (<c>Freightview.php</c>, <c>EcommShipping</c> L298–376). The credential is resolved from the vault under
/// "freightview" as JSON: { baseUrl, apiKey, originType, contactName, contactPhone, contactEmail,
/// pickupInstructions, dockOpenTime, dockCloseTime }.
///
/// Each cart unit is sent as one freight item (weight rounded up to whole pounds, freight class default 50,
/// L×W×H, package type default "Boxes", hazardous flag, pieces default 1); origin is the fulfilling store with
/// the credential's dock details, destination is residential. Every returned rate with a total becomes one
/// option named "&lt;carrier&gt; - &lt;service&gt;".
///
/// Freight needs the cart lines (<see cref="CarrierRateRequest.Lines"/>); without them, or with no credential,
/// or on any failure, an empty list is returned so the aggregate simply excludes Freightview.
/// </summary>
/// <remarks>
/// Built against Freightview's v1.0 REST rates contract (HTTP basic auth, API key as the user name). Returning
/// live quotes needs a valid API key; confirm the current API version with Freightview before go-live.
/// </remarks>
public class FreightviewCarrierClient : ICarrierClient
{
    private const string CredentialKey = "freightview";
    private const string RatesPath = "/rates";
    private const string DefaultFreightClass = "50";
    private const string DefaultPackageType = "Boxes";
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(20);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialVault _vault;
    private readonly ILogger<FreightviewCarrierClient> _logger;

    public FreightviewCarrierClient(
        IHttpClientFactory httpClientFactory, ICredentialVault vault, ILogger<FreightviewCarrierClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _vault = vault;
        _logger = logger;
    }

    public string CarrierName => "Freightview";
    public ShippingCarrierType Carrier => ShippingCarrierType.Freightview;

    public async Task<IReadOnlyList<ShippingRateOption>> GetRatesAsync(CarrierRateRequest request, CancellationToken ct)
    {
        var lines = request.Lines?.Where(l => !l.IsGiftCertificate && l.Quantity > 0).ToList();
        if (lines is null || lines.Count == 0)
            return Array.Empty<ShippingRateOption>();

        var raw = await _vault.GetCredentialAsync(CredentialKey, ct);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<ShippingRateOption>();

        var credential = FreightviewCredential.Parse(raw);
        if (string.IsNullOrWhiteSpace(credential.ApiKey))
            return Array.Empty<ShippingRateOption>();
        if (string.IsNullOrWhiteSpace(credential.BaseUrl))
            throw new InvalidOperationException(
                "The active Freightview credential has no Base URL. Set it on the integration (Integrations → Freightview).");

        try
        {
            var client = _httpClientFactory.CreateClient("freightview");
            client.Timeout = HttpTimeout;

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{credential.BaseUrl!.TrimEnd('/')}{RatesPath}");
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.ApiKey}:"));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(BuildRequestBody(request, lines, credential)), Encoding.UTF8, "application/json");

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

    private static object BuildRequestBody(
        CarrierRateRequest request, IReadOnlyList<ShippingRateLine> lines, FreightviewCredential c)
    {
        // One freight item per unit, as the legacy integration sent it.
        var items = lines
            .SelectMany(l => Enumerable.Repeat(l, l.Quantity))
            .Select(l => new
            {
                weight = (int)Math.Ceiling(Math.Max(1m, l.WeightLb ?? 1m)),
                freightClass = string.IsNullOrWhiteSpace(l.FreightClass) ? DefaultFreightClass : l.FreightClass!.Trim(),
                length = l.LengthIn ?? 0m,
                width = l.WidthIn ?? 0m,
                height = l.HeightIn ?? 0m,
                package = string.IsNullOrWhiteSpace(l.PackageType) ? DefaultPackageType : l.PackageType!.Trim(),
                pieces = l.PackagePieces > 0 ? l.PackagePieces : 1,
                hazardous = l.IsHazardous,
            })
            .ToList();

        return new
        {
            pickupDate = NextBusinessDay(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            originPostalCode = request.Origin.PostalCode,
            originCity = request.Origin.City,
            originState = request.Origin.Region,
            originCountry = request.Origin.CountryCode,
            originType = c.OriginType ?? "business dock",
            originContactName = c.ContactName,
            originContactPhone = c.ContactPhone,
            originContactEmail = c.ContactEmail,
            originPickupInstructions = c.PickupInstructions,
            originDockHoursOpen = c.DockOpenTime,
            originDockHoursClose = c.DockCloseTime,
            destPostalCode = request.Destination.PostalCode,
            destCity = request.Destination.City,
            destState = request.Destination.Region,
            destCountry = request.Destination.CountryCode,
            destType = "residential",
            items,
        };
    }

    private static DateTime NextBusinessDay(DateTime from)
    {
        var d = from.Date.AddDays(1);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            d = d.AddDays(1);
        return d;
    }

    private static IReadOnlyList<ShippingRateOption> MapResponse(JsonElement root)
    {
        var rates = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("rates", out var r) && r.ValueKind == JsonValueKind.Array ? r : default;
        if (rates.ValueKind != JsonValueKind.Array)
            return Array.Empty<ShippingRateOption>();

        var options = new List<ShippingRateOption>();
        foreach (var rate in rates.EnumerateArray())
        {
            // Only rates carrying a total are bookable quotes (the legacy code skipped the rest).
            if (!TryGetDecimal(rate, "total", out var total))
                continue;

            var carrier = GetString(rate, "carrier") ?? GetString(rate, "carrierName") ?? "Freight";
            var service = GetString(rate, "service") ?? GetString(rate, "serviceName");
            var name = string.IsNullOrWhiteSpace(service) ? carrier : $"{carrier} - {service}";
            int? days = TryGetDecimal(rate, "days", out var d) ? (int)Math.Ceiling(d)
                : TryGetDecimal(rate, "transitDays", out var td) ? (int)Math.Ceiling(td) : null;

            options.Add(new ShippingRateOption(
                MethodId: $"freightview:{GetString(rate, "id") ?? name}",
                Name: name,
                Carrier: "Freightview",
                EstimatedDeliveryDays: days,
                Rate: Math.Round(total, 2, MidpointRounding.AwayFromZero)));
        }
        return options;
    }

    private static string? GetString(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                _ => null,
            }
            : null;

    private static bool TryGetDecimal(JsonElement e, string name, out decimal value)
    {
        value = 0m;
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v))
            return false;
        if (v.ValueKind == JsonValueKind.Number)
            return v.TryGetDecimal(out value);
        return v.ValueKind == JsonValueKind.String &&
               decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private sealed record FreightviewCredential(
        string? BaseUrl, string? ApiKey, string? OriginType, string? ContactName, string? ContactPhone,
        string? ContactEmail, string? PickupInstructions, string? DockOpenTime, string? DockCloseTime)
    {
        public static FreightviewCredential Parse(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var r = doc.RootElement;
                return new FreightviewCredential(
                    GetString(r, "baseUrl"), GetString(r, "apiKey"), GetString(r, "originType"),
                    GetString(r, "contactName"), GetString(r, "contactPhone"), GetString(r, "contactEmail"),
                    GetString(r, "pickupInstructions"), GetString(r, "dockOpenTime"), GetString(r, "dockCloseTime"));
            }
            catch (JsonException)
            {
                return new FreightviewCredential(null, null, null, null, null, null, null, null, null);
            }
        }
    }
}
