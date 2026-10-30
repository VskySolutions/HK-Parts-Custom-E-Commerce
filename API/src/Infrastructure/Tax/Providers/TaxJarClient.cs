using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Common;

namespace HKPartsEcommerce.Infrastructure.Tax.Providers;

/// <summary>
/// TaxJar provider client (WO-36). Calculates tax via <c>POST /v2/taxes</c> and reports finalized orders
/// via <c>POST /v2/transactions/orders</c> for remittance. Authenticates with a Bearer API token resolved
/// at runtime from the Credential Vault under the key <c>"taxjar"</c>.
/// <para>
/// Live TaxJar credentials are required — without them, and on any HTTP failure, this client throws so
/// <see cref="ITaxCalculationService"/> applies its flat-rate fallback (AC-TAX-001.4 / AC-TAX-002.3).
/// </para>
/// </summary>
public class TaxJarClient : ITaxProviderClient
{
    private const string CredentialKey = "taxjar";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialVault _vault;
    private readonly IApplicationDbContext _db;

    public TaxJarClient(IHttpClientFactory httpClientFactory, ICredentialVault vault, IApplicationDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _vault = vault;
        _db = db;
    }

    public TaxProviderType Provider => TaxProviderType.TaxJar;

    public async Task<TaxBreakdown> CalculateAsync(TaxCalculationRequest req, CancellationToken ct)
    {
        var (client, baseUrl) = await CreateClientAsync(ct);

        var payload = new
        {
            from_country = req.Origin.CountryCode,
            from_zip = req.Origin.PostalCode,
            // The app stores state as a display name (e.g. "Florida"); TaxJar needs the 2-letter code.
            from_state = RegionCodeNormalizer.ToStateCode(req.Origin.CountryCode, req.Origin.Region),
            from_city = req.Origin.City,
            to_country = req.Destination.CountryCode,
            to_zip = req.Destination.PostalCode,
            to_state = RegionCodeNormalizer.ToStateCode(req.Destination.CountryCode, req.Destination.Region),
            to_city = req.Destination.City,
            // Order subtotal (less shipping and tax), net of any allocated discounts.
            amount = req.Lines.Sum(l => Math.Max(0m, l.Amount * l.Quantity - l.DiscountAmount)),
            shipping = req.ShippingAmount,
            line_items = req.Lines.Select((l, i) => new
            {
                id = (i + 1).ToString(CultureInfo.InvariantCulture),
                quantity = l.Quantity,
                // Only a real TaxJar product tax code (numeric, e.g. "31000") is valid here; the catalog
                // stores a human-readable Tax Category name, so forward it only when it looks like a code
                // and otherwise omit it (null is dropped by WhenWritingNull) so the line is fully taxable.
                product_tax_code = TaxJarProductCode(l.TaxCategoryCode),
                unit_price = l.Amount,
                // The order/product/coupon discount allocated to this line; TaxJar taxes (unit_price × qty − discount).
                // Omitted (null → dropped) when there's nothing to discount.
                discount = l.DiscountAmount > 0m ? l.DiscountAmount : (decimal?)null,
            }).ToArray(),
        };

        using var response = await client.PostAsJsonAsync($"{baseUrl}/v2/taxes", payload, JsonOptions, ct);
        await EnsureTaxJarSuccessAsync(response, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return MapResponse(document.RootElement);
    }

    public async Task ReportTransactionAsync(Guid orderId, CancellationToken ct)
    {
        // Records the finalized order with TaxJar for reporting/remittance (AC-TAX-004.2). Loads the order's
        // amounts, addresses and line items and maps them onto the /v2/transactions/orders payload. A no-op
        // when the order is missing, uncharged, or its tax came from the flat-rate fallback (nothing to remit).
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order is null || order.TaxFlaggedForReview || order.TaxTotal <= 0m)
            return;

        var (client, baseUrl) = await CreateClientAsync(ct);

        var payload = new
        {
            transaction_id = order.OrderNumber,
            transaction_date = order.PlacedOnUtc.ToString("O"),
            to_country = order.CountryCode,
            to_zip = order.PostalCode,
            to_state = RegionCodeNormalizer.ToStateCode(order.CountryCode, order.Region ?? order.StateProvince),
            to_city = order.City,
            to_street = order.AddressLine1,
            // TaxJar `amount` is the order total incl. shipping but EXCLUDING sales tax.
            amount = order.TotalAmount - order.TaxTotal,
            shipping = order.ShippingTotal,
            sales_tax = order.TaxTotal,
            line_items = order.Lines.Select((l, i) => new
            {
                id = (i + 1).ToString(CultureInfo.InvariantCulture),
                quantity = l.Quantity,
                product_identifier = l.Sku ?? l.ProductId.ToString(),
                description = l.ProductName,
                unit_price = l.UnitPrice,
            }).ToArray(),
        };

        using var response = await client.PostAsJsonAsync($"{baseUrl}/v2/transactions/orders", payload, JsonOptions, ct);
        await EnsureTaxJarSuccessAsync(response, ct);
    }

    /// <summary>
    /// The authenticated client plus the endpoint its credential belongs to. The URL is the admin's to set:
    /// TaxJar's sandbox is a different host from live, so a constant here would happily bill sandbox keys
    /// against production.
    /// </summary>
    private async Task<(HttpClient Client, string BaseUrl)> CreateClientAsync(CancellationToken ct)
    {
        // Live TaxJar API token (Bearer), stored encrypted in the Credential Vault under "taxjar".
        var resolved = await _vault.GetResolvedCredentialAsync(CredentialKey, ct);
        var token = resolved?.Value;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("TaxJar credentials are not configured (Credential Vault key 'taxjar').");
        if (string.IsNullOrWhiteSpace(resolved!.BaseUrl))
            throw new InvalidOperationException(
                "The active TaxJar credential has no Base URL. Set it on the integration (Integrations → TaxJar).");

        var client = _httpClientFactory.CreateClient("taxjar");
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // TaxJar performs content negotiation and returns 406 (Not Acceptable) unless the request
        // explicitly accepts JSON — HttpClient sends no Accept header by default.
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return (client, resolved.BaseUrl!.Trim().TrimEnd('/'));
    }

    /// <summary>Only a real TaxJar product tax code (numeric, e.g. <c>"31000"</c>) is valid; a human-readable
    /// Tax Category name is not, so return it only when it looks like a code and otherwise <c>null</c>.</summary>
    private static string? TaxJarProductCode(string? code)
        => !string.IsNullOrWhiteSpace(code) && code.All(char.IsDigit) ? code : null;

    /// <summary>
    /// Throws with TaxJar's actual error detail on a non-success response. TaxJar returns a JSON body
    /// (<c>{ "error", "detail", "status" }</c>) describing the failure; <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>
    /// discards it and surfaces only the bare status code. Reading it means the flat-rate fallback admin
    /// alert names the real cause instead of an opaque status such as "406 (Not Acceptable)".
    /// </summary>
    private static async Task EnsureTaxJarSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            body = string.Empty;
        }

        var detail = TryExtractTaxJarError(body);
        throw new HttpRequestException(
            $"TaxJar request failed with {(int)response.StatusCode} ({response.StatusCode})" +
            (detail is null ? "." : $": {detail}"));
    }

    /// <summary>Extracts <c>detail</c> / <c>error</c> from a TaxJar error body, if present.</summary>
    private static string? TryExtractTaxJarError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var parts = new List<string>();
            if (doc.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                parts.Add(d.GetString()!);
            if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                parts.Add($"error: {e.GetString()}");
            return parts.Count > 0 ? string.Join(" | ", parts) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Maps a TaxJar <c>/v2/taxes</c> response onto a <see cref="TaxBreakdown"/>. Shape:
    /// <c>{ "tax": { "amount_to_collect": 1.23, "rate": 0.0825, "breakdown": { ...*_tax_collectable, *_tax_rate } } }</c>.
    /// </summary>
    private static TaxBreakdown MapResponse(JsonElement root)
    {
        if (!root.TryGetProperty("tax", out var tax) || tax.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("TaxJar response did not contain a 'tax' object.");

        var totalTax = tax.TryGetProperty("amount_to_collect", out var amt) && amt.TryGetDecimal(out var a) ? a : 0m;

        var jurisdictions = new List<TaxJurisdiction>();
        if (tax.TryGetProperty("breakdown", out var breakdown) && breakdown.ValueKind == JsonValueKind.Object)
        {
            AddJurisdiction(jurisdictions, breakdown, "country_tax_collectable", "country_tax_rate", "Country", "Country");
            AddJurisdiction(jurisdictions, breakdown, "state_tax_collectable", "state_tax_rate", "State", "State");
            AddJurisdiction(jurisdictions, breakdown, "county_tax_collectable", "county_tax_rate", "County", "County");
            AddJurisdiction(jurisdictions, breakdown, "city_tax_collectable", "city_tax_rate", "City", "City");
            AddJurisdiction(jurisdictions, breakdown, "special_district_tax_collectable", "special_tax_rate", "Special District", "Special");
        }

        // No itemized breakdown but tax is owed: synthesize a single total jurisdiction.
        if (jurisdictions.Count == 0 && totalTax > 0m)
        {
            var rate = tax.TryGetProperty("rate", out var r) && r.TryGetDecimal(out var rv) ? rv : 0m;
            jurisdictions.Add(new TaxJurisdiction("TaxJar", "Total", rate, totalTax));
        }

        return new TaxBreakdown(totalTax, jurisdictions, FallbackApplied: false);
    }

    private static void AddJurisdiction(
        List<TaxJurisdiction> list, JsonElement breakdown, string amountProp, string rateProp, string name, string type)
    {
        var amount = breakdown.TryGetProperty(amountProp, out var a) && a.TryGetDecimal(out var av) ? av : 0m;
        if (amount <= 0m)
            return;

        var rate = breakdown.TryGetProperty(rateProp, out var r) && r.TryGetDecimal(out var rv) ? rv : 0m;
        list.Add(new TaxJurisdiction(name, type, rate, amount));
    }
}
