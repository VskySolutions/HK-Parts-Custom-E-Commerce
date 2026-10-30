using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Common;

namespace HKPartsEcommerce.Infrastructure.Tax.Providers;

/// <summary>
/// Stripe Tax provider client (WO-36). Calculates tax via <c>POST /v1/tax/calculations</c>. Stripe's API
/// is form-encoded and works in the currency's smallest unit (e.g. cents). Authenticates with a Bearer
/// secret key resolved at runtime from the Credential Vault under the key <c>"stripe-tax"</c>.
/// <para>
/// Live Stripe credentials are required — without them, and on any HTTP failure, this client throws so
/// <see cref="ITaxCalculationService"/> applies its flat-rate fallback (AC-TAX-001.4 / AC-TAX-002.3).
/// Transaction reporting is a no-op: only TaxJar performs remittance reporting.
/// </para>
/// </summary>
public class StripeTaxClient : ITaxProviderClient
{
    private const string CredentialKey = "stripe-tax";
    private const string CalculationsPath = "/v1/tax/calculations";
    private const string TransactionsPath = "/v1/tax/transactions/create_from_calculation";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialVault _vault;
    private readonly IApplicationDbContext _db;

    public StripeTaxClient(IHttpClientFactory httpClientFactory, ICredentialVault vault, IApplicationDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _vault = vault;
        _db = db;
    }

    public TaxProviderType Provider => TaxProviderType.StripeTax;

    public async Task<TaxBreakdown> CalculateAsync(TaxCalculationRequest req, CancellationToken ct)
    {
        var (client, baseUrl) = await CreateClientAsync(ct);

        using var content = new FormUrlEncodedContent(BuildForm(req));
        using var response = await client.PostAsync(baseUrl + CalculationsPath, content, ct);
        await EnsureStripeSuccessAsync(response, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return MapResponse(document.RootElement);
    }

    /// <summary>
    /// Records a Stripe Tax Transaction from the calculation captured at placement (AC-TAX-004.2). Loads the
    /// order's stored calculation reference and posts <c>create_from_calculation</c>. A no-op when the order
    /// carries no Stripe calculation reference (e.g. tax was flat-rate/fallback or another provider was active).
    /// </summary>
    public async Task ReportTransactionAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.OrderNumber, o.TaxProviderCalculationRef })
            .FirstOrDefaultAsync(ct);

        if (order is null || string.IsNullOrWhiteSpace(order.TaxProviderCalculationRef))
            return;

        var (client, baseUrl) = await CreateClientAsync(ct);

        using var content = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
        {
            new("calculation", order.TaxProviderCalculationRef!),
            new("reference", order.OrderNumber),
        });
        using var response = await client.PostAsync(baseUrl + TransactionsPath, content, ct);
        await EnsureStripeSuccessAsync(response, ct);
    }

    /// <summary>
    /// The authenticated client plus the endpoint its credential belongs to. The URL is the admin's to set
    /// rather than a constant compiled in here — see <see cref="ResolvedCredential.BaseUrl"/>.
    /// </summary>
    private async Task<(HttpClient Client, string BaseUrl)> CreateClientAsync(CancellationToken ct)
    {
        // Live Stripe secret key, stored encrypted in the Credential Vault under "stripe-tax".
        var resolved = await _vault.GetResolvedCredentialAsync(CredentialKey, ct);
        var secretKey = resolved?.Value;
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("Stripe Tax credentials are not configured (Credential Vault key 'stripe-tax').");
        if (string.IsNullOrWhiteSpace(resolved!.BaseUrl))
            throw new InvalidOperationException(
                "The active Stripe Tax credential has no Base URL. Set it on the integration (Integrations → Stripe Tax).");

        var client = _httpClientFactory.CreateClient("stripe-tax");
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        return (client, resolved.BaseUrl!.Trim().TrimEnd('/'));
    }

    private static List<KeyValuePair<string, string>> BuildForm(TaxCalculationRequest req)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("currency", "usd"),
            new("customer_details[address][country]", req.Destination.CountryCode),
            // The app stores state as a display name (e.g. "Florida"); Stripe needs the 2-letter code.
            new("customer_details[address][state]",
                RegionCodeNormalizer.ToStateCode(req.Destination.CountryCode, req.Destination.Region) ?? string.Empty),
            new("customer_details[address][postal_code]", req.Destination.PostalCode ?? string.Empty),
            new("customer_details[address][city]", req.Destination.City ?? string.Empty),
            new("customer_details[address_source]", "shipping"),
            new("shipping_cost[amount]", ToMinorUnits(req.ShippingAmount)),
        };

        for (var i = 0; i < req.Lines.Count; i++)
        {
            var line = req.Lines[i];
            // Stripe line amount is the total for the line, net of the discount allocated to it (never below 0),
            // so tax is charged on the discounted value.
            var lineAmount = Math.Max(0m, line.Amount * line.Quantity - line.DiscountAmount);
            form.Add(new($"line_items[{i}][amount]", ToMinorUnits(lineAmount)));
            form.Add(new($"line_items[{i}][reference]", line.ProductId.ToString()));

            // Stripe requires a registry tax code (txcd_*); the catalog stores a human-readable Tax
            // Category *name* here, which Stripe rejects with 400 ("Invalid tax code") and so forces the
            // flat-rate fallback on every calculation. Forward it only when it is an actual Stripe code —
            // otherwise omit it and let Stripe apply the account's preset (default) product tax code.
            if (IsStripeTaxCode(line.TaxCategoryCode))
                form.Add(new($"line_items[{i}][tax_code]", line.TaxCategoryCode!));
        }

        return form;
    }

    private static string ToMinorUnits(decimal amount)
        => ((long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

    /// <summary>True when the value is an actual Stripe tax code (e.g. <c>txcd_99999999</c>) rather than a
    /// human-readable Tax Category name, and can therefore be forwarded as <c>line_items[][tax_code]</c>.</summary>
    private static bool IsStripeTaxCode(string? code)
        => !string.IsNullOrWhiteSpace(code) && code.StartsWith("txcd_", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Throws with Stripe's actual error detail on a non-success response. Stripe returns a JSON body
    /// (<c>{ "error": { "message", "param", "code" } }</c>) explaining exactly why a request failed;
    /// <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/> discards it and surfaces only the bare
    /// status code. Reading it means the flat-rate fallback admin alert names the real cause (e.g. an
    /// invalid tax code and the offending parameter) instead of an opaque "400 (Bad Request)".
    /// </summary>
    private static async Task EnsureStripeSuccessAsync(HttpResponseMessage response, CancellationToken ct)
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

        var detail = TryExtractStripeError(body);
        throw new HttpRequestException(
            $"Stripe Tax request failed with {(int)response.StatusCode} ({response.StatusCode})" +
            (detail is null ? "." : $": {detail}"));
    }

    /// <summary>Extracts <c>error.message</c> / <c>param</c> / <c>code</c> from a Stripe error body, if present.</summary>
    private static string? TryExtractStripeError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
                return null;

            var parts = new List<string>();
            if (error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                parts.Add(m.GetString()!);
            if (error.TryGetProperty("param", out var p) && p.ValueKind == JsonValueKind.String)
                parts.Add($"param: {p.GetString()}");
            if (error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                parts.Add($"code: {c.GetString()}");

            return parts.Count > 0 ? string.Join(" | ", parts) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Maps a Stripe Tax calculation response onto a <see cref="TaxBreakdown"/>. Shape (amounts in minor
    /// units): <c>{ "tax_amount_exclusive": 825, "tax_breakdown": [ { "amount": 825,
    /// "tax_rate_details": { "percentage_decimal": "8.25", "country": "US", "state": "CA" } } ] }</c>.
    /// </summary>
    private static TaxBreakdown MapResponse(JsonElement root)
    {
        // The calculation id is required later to record the tax transaction (WO-37 / AC-TAX-004.2).
        var calculationId = root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
            ? idEl.GetString()
            : null;

        var totalMinor = root.TryGetProperty("tax_amount_exclusive", out var t) && t.TryGetInt64(out var tv) ? tv : 0L;
        var totalTax = totalMinor / 100m;

        var jurisdictions = new List<TaxJurisdiction>();
        if (root.TryGetProperty("tax_breakdown", out var breakdown) && breakdown.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in breakdown.EnumerateArray())
            {
                var amountMinor = item.TryGetProperty("amount", out var a) && a.TryGetInt64(out var av) ? av : 0L;

                var rate = 0m;
                string? name = null;
                if (item.TryGetProperty("tax_rate_details", out var details) && details.ValueKind == JsonValueKind.Object)
                {
                    if (details.TryGetProperty("percentage_decimal", out var pct) && pct.ValueKind == JsonValueKind.String
                        && decimal.TryParse(pct.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var pctVal))
                        rate = pctVal / 100m;

                    if (details.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.String)
                        name = state.GetString();
                    else if (details.TryGetProperty("country", out var country) && country.ValueKind == JsonValueKind.String)
                        name = country.GetString();
                }

                jurisdictions.Add(new TaxJurisdiction(name ?? "Stripe Tax", "Tax", rate, amountMinor / 100m));
            }
        }

        return new TaxBreakdown(totalTax, jurisdictions, FallbackApplied: false, ProviderReference: calculationId);
    }
}
