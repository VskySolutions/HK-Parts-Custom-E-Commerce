using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Common;

namespace HKPartsEcommerce.Infrastructure.Tax.Providers;

/// <summary>
/// Avalara AvaTax tax client: the PHP <c>Avalara</c> / <c>EcommTax</c> 'avalara' method on the current AvaTax
/// <b>v2</b> REST API (the PHP code called the retired 1.0 <c>tax/get</c> / <c>tax/cancel</c> endpoints).
/// <list type="bullet">
/// <item><b>Checkout</b>: <c>POST /api/v2/transactions/create</c> as an uncommitted <c>SalesOrder</c> (an estimate
/// Avalara does not save), one line per item (SKU, the product's tax code, discounted amount) plus a shipping line
/// with the "Default Shipping Tax Code"; the customer's tax-exempt reason goes as <c>exemptionNo</c>.</item>
/// <item><b>Order placed and paid</b>, when "Document Committing" is on: <c>POST /api/v2/transactions/createoradjust</c>
/// as a committed <c>SalesInvoice</c> coded with the order number; the code is kept on the order
/// (PHP avalara_doc_code).</item>
/// <item><b>Refund</b> (admin Refund, Apply credit, return refund) of a committed order: a committed
/// <c>ReturnInvoice</c> coded <c>{doc code}.{unix time}</c> with negative lines and a tax-amount override of the tax
/// refunded (PHP applyCreditAvalara). The codes are kept on the order.</item>
/// <item><b>Order cancelled</b>: <c>POST /api/v2/companies/{company}/transactions/{code}/void</c> with <c>DocVoided</c>,
/// for each return document and then the sale.</item>
/// </list>
/// Credentials (base URL, account id, licence key, company code) come from the active Avalara credential, the same
/// one address verification uses. Every call is written to <see cref="TaxLog"/> when logging is on. Like the other
/// provider clients it throws on any failure so checkout falls back to the flat rate.
/// Field names follow the AvaTax v2 REST reference; this needs a live sandbox account to be exercised.
/// </summary>
public class AvalaraTaxClient : ITaxProviderClient, IAvalaraTaxConnection
{
    private const string ProviderName = "Avalara";
    private const string ClientHeader = "HKPartsEcommerce; 1.0; Tax; 1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationDbContext _db;
    private readonly TaxLogWriter _log;

    public AvalaraTaxClient(IHttpClientFactory httpClientFactory, IApplicationDbContext db, TaxLogWriter log)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _log = log;
    }

    public TaxProviderType Provider => TaxProviderType.Avalara;

    // ---- Checkout estimate --------------------------------------------------------------------------------

    public async Task<TaxBreakdown> CalculateAsync(TaxCalculationRequest req, CancellationToken ct)
    {
        var (credential, config) = await LoadAsync(ct);

        var lines = new List<object>();
        var number = 1;
        foreach (var l in req.Lines)
        {
            lines.Add(new
            {
                number = (number++).ToString(CultureInfo.InvariantCulture),
                quantity = (decimal)l.Quantity,
                // The line's discounted value: the discount engine already spread the order/coupon discount across
                // the lines, so the amount sent is what the buyer pays for it.
                amount = Math.Max(0m, l.Amount * l.Quantity - l.DiscountAmount),
                taxCode = FirstNonEmpty(l.ProviderTaxCode, AvalaraCode(l.TaxCategoryCode)),
                itemCode = Truncate(l.ItemCode, 50),
                description = Truncate(l.Description, 2048),
            });
        }
        if (req.ShippingAmount > 0m)
            lines.Add(ShippingLine(number, req.ShippingAmount, req.ShippingDescription, config));

        var model = new
        {
            type = "SalesOrder",
            companyCode = CompanyCode(credential),
            date = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            customerCode = Truncate(FirstNonEmpty(req.CustomerCode, "NA"), 50),
            commit = false,
            currencyCode = "USD",
            exemptionNo = Truncate(req.ExemptionNumber, 25),
            addresses = new
            {
                shipFrom = ToAvalara(req.Origin),
                shipTo = ToAvalara(req.Destination),
            },
            lines,
        };

        using var doc = await SendAsync(credential, config, HttpMethod.Post, "/api/v2/transactions/create", model,
            "Get Tax", orderNumber: null, ct);
        return MapBreakdown(doc.RootElement);
    }

    // ---- Commit on placement ------------------------------------------------------------------------------

    public async Task ReportTransactionAsync(Guid orderId, CancellationToken ct)
    {
        var config = await _db.TaxProviderConfigurations.AsNoTracking().FirstOrDefaultAsync(ct) ?? new TaxProviderConfiguration();
        if (!config.AvalaraCommitDocuments)
            return;

        var order = await _db.Orders
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        // PHP commits only a paid order. Fallback-taxed orders are left for review (the charged tax didn't come
        // from Avalara), and an already-committed one isn't sent twice.
        if (order is null || order.TaxDocumentCode is not null || order.TaxFlaggedForReview
            || order.PaymentStatus is not (PaymentStatus.Authorized or PaymentStatus.Captured or PaymentStatus.PartiallyRefunded))
            return;

        var (credential, _) = await LoadAsync(ct);
        var origin = await StoreAddressAsync(order.AssignedStoreId, ct);
        var destination = order.IsPickup ? origin : await OrderDestinationAsync(order, ct);

        var productIds = order.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ProductType, p.TaxCode })
            .ToDictionaryAsync(p => p.Id, ct);

        var discounted = order.DiscountTotal > 0m;
        var lines = new List<object>();
        var number = 1;
        foreach (var line in order.Lines)
        {
            products.TryGetValue(line.ProductId, out var product);
            if (product?.ProductType == ProductType.GiftCard)
                continue; // gift certificates are never taxed
            lines.Add(new
            {
                number = (number++).ToString(CultureInfo.InvariantCulture),
                quantity = (decimal)line.Quantity,
                amount = line.UnitPrice * line.Quantity,
                taxCode = string.IsNullOrWhiteSpace(product?.TaxCode) ? null : product!.TaxCode,
                itemCode = Truncate(line.Sku, 50),
                description = Truncate(line.ProductName, 2048),
                discounted = discounted ? true : (bool?)null,
            });
        }
        if (order.ShippingTotal > 0m)
            lines.Add(ShippingLine(number, order.ShippingTotal, order.ShippingMethodName, config));

        var model = new
        {
            type = "SalesInvoice",
            code = order.OrderNumber,
            companyCode = CompanyCode(credential),
            date = order.PlacedOnUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            customerCode = Truncate(FirstNonEmpty(order.ShippingAddress?.Email, order.OrderNumber), 50),
            commit = true,
            currencyCode = "USD",
            discount = discounted ? order.DiscountTotal : (decimal?)null,
            exemptionNo = await ExemptionNumberAsync(order.CustomerId, ct),
            addresses = new { shipFrom = ToAvalara(origin), shipTo = ToAvalara(destination) },
            lines,
        };

        // createoradjust: a retry of the same order updates its document instead of failing as a duplicate.
        using var doc = await SendAsync(credential, config, HttpMethod.Post, "/api/v2/transactions/createoradjust",
            new { createTransactionModel = model }, "Commit Order", order.OrderNumber, ct);
        order.TaxDocumentCode = doc.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
            ? code.GetString()
            : order.OrderNumber;
        await _db.SaveChangesAsync(ct);
    }

    // ---- Void on cancel -----------------------------------------------------------------------------------

    public async Task VoidTransactionAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order?.TaxDocumentCode is null || order.TaxDocumentVoidedOnUtc is not null)
            return;

        var (credential, config) = await LoadAsync(ct);

        // Returns first: once every refund document is voided, voiding the sale leaves nothing behind in Avalara.
        // A return voided earlier is dropped from the list, so a retry after a failure only sends what's left.
        var returns = ReturnCodes(order.TaxReturnDocumentCodes);
        foreach (var returnCode in returns.ToList())
        {
            using var r = await SendAsync(credential, config, HttpMethod.Post, VoidPath(credential, returnCode, "ReturnInvoice"),
                new { code = "DocVoided" }, "Void Return", order.OrderNumber, ct);
            returns.Remove(returnCode);
            order.TaxReturnDocumentCodes = returns.Count == 0 ? null : string.Join(';', returns);
            await _db.SaveChangesAsync(ct);
        }

        using var _ = await SendAsync(credential, config, HttpMethod.Post, VoidPath(credential, order.TaxDocumentCode, "SalesInvoice"),
            new { code = "DocVoided" }, "Void Tax", order.OrderNumber, ct);
        order.TaxDocumentVoidedOnUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static string VoidPath(Credential credential, string code, string documentType)
        => $"/api/v2/companies/{Uri.EscapeDataString(CompanyCode(credential))}/transactions/" +
           $"{Uri.EscapeDataString(code)}/void?documentType={documentType}";

    private static List<string> ReturnCodes(string? codes)
        => (codes ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // ---- Return on refund (PHP EcommOrder::applyCreditAvalara) ---------------------------------------------

    /// <summary>
    /// Posts a committed <c>ReturnInvoice</c> for a refund: the returned lines and shipping as negative amounts, with a
    /// document tax-amount override of the tax actually refunded (so Avalara's liability matches the money given back).
    /// PHP coded it <c>{doc code}.{unix time}</c>; the code is kept on the order so a later cancel voids it too.
    /// </summary>
    public async Task ReturnTransactionAsync(TaxReturnRequest request, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order?.TaxDocumentCode is null || order.TaxDocumentVoidedOnUtc is not null)
            return;

        var (credential, config) = await LoadAsync(ct);
        var origin = await StoreAddressAsync(order.AssignedStoreId, ct);
        var destination = order.IsPickup ? origin : await OrderDestinationAsync(order, ct);

        var linesById = order.Lines.ToDictionary(l => l.Id);
        var productIds = order.Lines.Select(l => l.ProductId).Distinct().ToList();
        var taxCodes = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.TaxCode })
            .ToDictionaryAsync(p => p.Id, p => p.TaxCode, ct);

        var lines = new List<object>();
        var number = 1;
        foreach (var l in request.Lines.Where(l => l.Amount > 0m || l.Quantity > 0m))
        {
            OrderLineItem? line = l.OrderLineItemId is Guid id && linesById.TryGetValue(id, out var found) ? found : null;
            var taxCode = line is not null ? taxCodes.GetValueOrDefault(line.ProductId) : null;
            lines.Add(new
            {
                number = (number++).ToString(CultureInfo.InvariantCulture),
                quantity = l.Quantity > 0m ? l.Quantity : 1m,
                amount = -Math.Round(l.Amount, 2, MidpointRounding.AwayFromZero),
                taxCode = string.IsNullOrWhiteSpace(taxCode) ? null : taxCode.Trim(),
                itemCode = Truncate(line?.Sku ?? "Refund", 50),
                description = Truncate(line?.ProductName ?? request.Reference, 2048),
            });
        }
        if (request.ShippingAmount > 0m)
        {
            lines.Add(new
            {
                number = (number++).ToString(CultureInfo.InvariantCulture),
                quantity = 1m,
                amount = -Math.Round(request.ShippingAmount, 2, MidpointRounding.AwayFromZero),
                taxCode = string.IsNullOrWhiteSpace(config.AvalaraShippingTaxCode) ? null : config.AvalaraShippingTaxCode.Trim(),
                itemCode = "Shipping",
                description = Truncate(FirstNonEmpty(order.ShippingMethodName, "Shipping"), 2048),
            });
        }
        if (lines.Count == 0)
        {
            // Tax-only credit: Avalara needs a line to carry the override.
            lines.Add(new { number = "1", quantity = 1m, amount = 0m, itemCode = "Refund", description = Truncate(request.Reference, 2048) });
        }

        var suffix = "." + DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var code = Truncate(order.TaxDocumentCode, 50 - suffix.Length) + suffix;
        var model = new
        {
            type = "ReturnInvoice",
            code,
            companyCode = CompanyCode(credential),
            date = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            customerCode = Truncate(FirstNonEmpty(order.ShippingAddress?.Email, order.OrderNumber), 50),
            commit = true,
            currencyCode = "USD",
            referenceCode = order.TaxDocumentCode,
            exemptionNo = await ExemptionNumberAsync(order.CustomerId, ct),
            taxOverride = new
            {
                type = "TaxAmount",
                taxAmount = -Math.Round(request.TaxAmount, 2, MidpointRounding.AwayFromZero),
                reason = Truncate("Refund: " + request.Reference, 255),
            },
            addresses = new { shipFrom = ToAvalara(origin), shipTo = ToAvalara(destination) },
            lines,
        };

        using var doc = await SendAsync(credential, config, HttpMethod.Post, "/api/v2/transactions/create", model,
            "Return Tax", order.OrderNumber, ct);
        var posted = doc.RootElement.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()! : code;
        var codes = ReturnCodes(order.TaxReturnDocumentCodes);
        codes.Add(posted);
        order.TaxReturnDocumentCodes = Truncate(string.Join(';', codes), 2000);
        await _db.SaveChangesAsync(ct);
    }

    // ---- Test connection (PHP pingTest) -------------------------------------------------------------------

    public async Task<TaxConnectionTestResult> TestConnectionAsync(CancellationToken ct)
    {
        try
        {
            var (credential, config) = await LoadAsync(ct);
            using var doc = await SendAsync(credential, config, HttpMethod.Get, "/api/v2/utilities/ping", null,
                "Test Connection", orderNumber: null, ct);
            var authenticated = doc.RootElement.TryGetProperty("authenticated", out var a) && a.ValueKind == JsonValueKind.True;
            var version = doc.RootElement.TryGetProperty("version", out var v) ? v.ToString() : null;
            return authenticated
                ? new TaxConnectionTestResult(true, "Connected to Avalara" + (version is null ? "." : $" (AvaTax {version})."))
                : new TaxConnectionTestResult(false, "Avalara answered, but the account id / licence key were not accepted.");
        }
        catch (Exception ex)
        {
            return new TaxConnectionTestResult(false, ex.Message);
        }
    }

    // ---- helpers ------------------------------------------------------------------------------------------

    private sealed record Credential(string BaseUrl, string AccountId, string LicenseKey, string? CompanyCode);

    private async Task<(Credential Credential, TaxProviderConfiguration Config)> LoadAsync(CancellationToken ct)
    {
        var row = await _db.AvalaraCredentials.AsNoTracking()
            .Where(c => c.Active)
            .OrderByDescending(c => c.UpdatedOnUtc)
            .Select(c => new { c.BaseUrl, c.AccountId, c.LicenseKey, c.CompanyCode })
            .FirstOrDefaultAsync(ct);
        if (row is null || string.IsNullOrWhiteSpace(row.BaseUrl) || string.IsNullOrWhiteSpace(row.AccountId)
            || string.IsNullOrWhiteSpace(row.LicenseKey))
            throw new InvalidOperationException(
                "Avalara is not configured. Add an active Avalara credential (base URL, account id, licence key) under Integrations.");

        var config = await _db.TaxProviderConfigurations.AsNoTracking().FirstOrDefaultAsync(ct) ?? new TaxProviderConfiguration();
        return (new Credential(row.BaseUrl!.Trim().TrimEnd('/'), row.AccountId!.Trim(), row.LicenseKey!, row.CompanyCode), config);
    }

    private static string CompanyCode(Credential c) => string.IsNullOrWhiteSpace(c.CompanyCode) ? "DEFAULT" : c.CompanyCode.Trim();

    private static object ShippingLine(int number, decimal amount, string? description, TaxProviderConfiguration config) => new
    {
        number = number.ToString(CultureInfo.InvariantCulture),
        quantity = 1m,
        amount,
        taxCode = string.IsNullOrWhiteSpace(config.AvalaraShippingTaxCode) ? null : config.AvalaraShippingTaxCode.Trim(),
        itemCode = "Shipping",
        description = Truncate(FirstNonEmpty(description, "Shipping"), 2048),
    };

    private static object ToAvalara(TaxAddress a) => new
    {
        line1 = string.IsNullOrWhiteSpace(a.Line1) ? null : a.Line1.Trim(),
        line2 = string.IsNullOrWhiteSpace(a.Line2) ? null : a.Line2.Trim(),
        city = string.IsNullOrWhiteSpace(a.City) ? null : a.City.Trim(),
        region = RegionCodeNormalizer.ToStateCode(a.CountryCode, a.Region),
        country = string.IsNullOrWhiteSpace(a.CountryCode) ? "US" : a.CountryCode.Trim().ToUpperInvariant(),
        postalCode = string.IsNullOrWhiteSpace(a.PostalCode) ? null : a.PostalCode.Trim(),
    };

    private async Task<TaxAddress> StoreAddressAsync(Guid? storeId, CancellationToken ct)
    {
        var store = await _db.Stores.AsNoTracking()
            .Include(s => s.Address)
            .Where(s => storeId == null || s.Id == storeId)
            .FirstOrDefaultAsync(ct);
        var a = store?.Address;
        return new TaxAddress(a?.CountryCode ?? "US", a?.StateProvince, a?.PostalCode, a?.City, a?.AddressLine1, a?.AddressLine2);
    }

    /// <summary>
    /// Where the order was taxed at checkout: the buyer's ship-to, except an order whose only shipment goes to an
    /// FFL dealer, which checkout taxes at the dealer.
    /// </summary>
    private async Task<TaxAddress> OrderDestinationAsync(Order order, CancellationToken ct)
    {
        var rows = await _db.OrderShippings.AsNoTracking()
            .Where(s => s.OrderId == order.Id)
            .Select(s => new { s.ShipToType, s.FflDealerStreet, s.FflDealerCity, s.FflDealerState, s.FflDealerZip })
            .ToListAsync(ct);
        if (rows.Count == 1 && rows[0].ShipToType == ShipToType.FflDealer && !string.IsNullOrWhiteSpace(rows[0].FflDealerZip))
            return new TaxAddress("US", rows[0].FflDealerState, rows[0].FflDealerZip, rows[0].FflDealerCity, rows[0].FflDealerStreet);

        var s = order.ShippingAddress;
        return new TaxAddress(s?.CountryCode ?? "US", s?.StateProvince, s?.PostalCode, s?.City, s?.AddressLine1, s?.AddressLine2);
    }

    /// <summary>PHP: the customer's tax-exempt reason is sent to Avalara as the exemption number.</summary>
    private async Task<string?> ExemptionNumberAsync(Guid? customerId, CancellationToken ct)
    {
        if (customerId is not Guid id)
            return null;
        var reason = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => c.TaxExemptReason)
            .FirstOrDefaultAsync(ct);
        return Truncate(string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), 25);
    }

    /// <summary>Sends one AvaTax call, logs it when logging is on, and throws with Avalara's own message on failure.</summary>
    private async Task<JsonDocument> SendAsync(
        Credential credential, TaxProviderConfiguration config, HttpMethod method, string path, object? body,
        string action, string? orderNumber, CancellationToken ct)
    {
        var url = credential.BaseUrl + path;
        var requestJson = body is null ? null : JsonSerializer.Serialize(body, JsonOptions);
        var client = _httpClientFactory.CreateClient("avalara-tax");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(config.AvalaraTimeoutSeconds, 1, 120));

        using var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.AccountId}:{credential.LicenseKey}")));
        message.Headers.TryAddWithoutValidation("X-Avalara-Client", ClientHeader);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (requestJson is not null)
            message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var watch = Stopwatch.StartNew();
        int? status = null;
        string? responseJson = null;
        try
        {
            using var response = await client.SendAsync(message, ct);
            status = (int)response.StatusCode;
            responseJson = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Avalara {action} failed with {status} ({response.StatusCode})" + (ErrorDetail(responseJson) is { } d ? $": {d}" : "."));

            var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(responseJson) ? "{}" : responseJson);
            await LogAsync(config, action, true, orderNumber, url, status, requestJson, responseJson, null, watch, ct);
            return doc;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var error = ex is TaskCanceledException ? $"Avalara did not answer within {client.Timeout.TotalSeconds:0} seconds." : ex.Message;
            await LogAsync(config, action, false, orderNumber, url, status, requestJson, responseJson, error, watch, ct);
            if (ex is TaskCanceledException)
                throw new HttpRequestException(error, ex);
            throw;
        }
    }

    private Task LogAsync(TaxProviderConfiguration config, string action, bool success, string? orderNumber, string url,
        int? status, string? request, string? response, string? error, Stopwatch watch, CancellationToken ct)
    {
        // The Test Connection button always logs, so its result is visible even with logging off.
        if (!config.AvalaraLoggingEnabled && action != "Test Connection")
            return Task.CompletedTask;
        return _log.WriteAsync(new TaxLog
        {
            Provider = ProviderName,
            Action = action,
            Success = success,
            OrderNumber = orderNumber,
            Url = url,
            HttpStatus = status,
            RequestJson = request,
            ResponseJson = response,
            ErrorMessage = error,
            DurationMs = (int)watch.ElapsedMilliseconds,
            CreatedOnUtc = DateTime.UtcNow,
        }, CancellationToken.None);
    }

    /// <summary>Avalara errors: <c>{ "error": { "message", "details": [ { "message", "description" } ] } }</c>.</summary>
    private static string? ErrorDetail(string? body)
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
            if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                foreach (var d in details.EnumerateArray())
                    if (d.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String
                        && !parts.Contains(desc.GetString()!))
                        parts.Add(desc.GetString()!);
            return parts.Count > 0 ? string.Join(" | ", parts) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Maps <c>totalTax</c> and the <c>summary</c> rows (one per jurisdiction) onto a <see cref="TaxBreakdown"/>.</summary>
    private static TaxBreakdown MapBreakdown(JsonElement root)
    {
        var total = root.TryGetProperty("totalTax", out var t) && t.TryGetDecimal(out var tv) ? tv : 0m;
        var jurisdictions = new List<TaxJurisdiction>();
        if (root.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in summary.EnumerateArray())
            {
                var amount = row.TryGetProperty("tax", out var a) && a.TryGetDecimal(out var av) ? av : 0m;
                if (amount == 0m)
                    continue;
                var name = row.TryGetProperty("jurisName", out var n) ? n.GetString() : null;
                var type = row.TryGetProperty("jurisType", out var ty) ? ty.GetString() : null;
                var rate = row.TryGetProperty("rate", out var r) && r.TryGetDecimal(out var rv) ? rv : 0m;
                jurisdictions.Add(new TaxJurisdiction(name ?? "Avalara", type, rate, amount));
            }
        }
        if (jurisdictions.Count == 0 && total > 0m)
            jurisdictions.Add(new TaxJurisdiction("Avalara", "Total", 0m, total));

        var reference = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
        return new TaxBreakdown(total, jurisdictions, FallbackApplied: false, ProviderReference: reference);
    }

    /// <summary>
    /// A Tax Category name is only an Avalara code when it looks like one (e.g. "P0000000", "PC040100"); a
    /// human-readable name ("Taxable Goods") is not, so send none and let Avalara treat the line as general goods.
    /// </summary>
    private static string? AvalaraCode(string? code)
        => !string.IsNullOrWhiteSpace(code) && code.Length <= 25 && !code.Contains(' ') && code.Any(char.IsDigit)
            ? code.Trim()
            : null;

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? Truncate(string? value, int max)
        => value is null ? null : value.Length > max ? value[..max] : value;
}
