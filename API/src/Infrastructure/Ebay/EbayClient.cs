using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Ebay;

/// <summary>
/// eBay REST Sell API client. Registered as a typed HttpClient with a resilience pipeline (exponential retry,
/// 3 attempts on 429/5xx honouring Retry-After; 30-second attempt timeout). Every call resolves the store
/// session via <see cref="IEbayAuthService"/> (auto token refresh) and is logged to <c>EbayApiLogs</c> when
/// the store's logging is on — URL and bodies redacted, the Authorization header never recorded.
/// </summary>
public sealed class EbayClient : IEbayClient
{
    private const string ContentLanguage = "en-US";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Metadata API numeric condition ids → Inventory API ConditionEnum.</summary>
    private static readonly IReadOnlyDictionary<string, string> ConditionEnum = new Dictionary<string, string>
    {
        ["1000"] = "NEW",
        ["1500"] = "NEW_OTHER",
        ["1750"] = "NEW_WITH_DEFECTS",
        ["2000"] = "CERTIFIED_REFURBISHED",
        ["2010"] = "EXCELLENT_REFURBISHED",
        ["2020"] = "VERY_GOOD_REFURBISHED",
        ["2030"] = "GOOD_REFURBISHED",
        ["2500"] = "SELLER_REFURBISHED",
        ["2750"] = "LIKE_NEW",
        ["2990"] = "PRE_OWNED_EXCELLENT",
        ["3000"] = "USED_EXCELLENT",
        ["3010"] = "PRE_OWNED_FAIR",
        ["4000"] = "USED_VERY_GOOD",
        ["5000"] = "USED_GOOD",
        ["6000"] = "USED_ACCEPTABLE",
        ["7000"] = "FOR_PARTS_OR_NOT_WORKING",
    };

    private readonly HttpClient _http;
    private readonly IEbayAuthService _auth;
    private readonly IEbayApiLogSink _log;
    private readonly IDateTimeProvider _clock;

    public EbayClient(HttpClient http, IEbayAuthService auth, IEbayApiLogSink log, IDateTimeProvider clock)
    {
        _http = http;
        _auth = auth;
        _log = log;
        _clock = clock;
    }

    // ---- Identity -------------------------------------------------------------------------------------

    public async Task<string?> GetUsernameAsync(Guid storeId, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, allowDisabled: true, ct);
        var url = $"{EbayOptions.IdentityApiBase(session.Environment)}/commerce/identity/v1/user/";
        var node = await SendAsync(session, "identity.getUser", HttpMethod.Get, url, null, ct);
        return node?["username"]?.GetValue<string>();
    }

    // ---- Account --------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<EbayPolicy>> GetPoliciesAsync(Guid storeId, EbayPolicyType type, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, allowDisabled: true, ct);
        var (path, collection, idField) = type switch
        {
            EbayPolicyType.Payment => ("payment_policy", "paymentPolicies", "paymentPolicyId"),
            EbayPolicyType.Fulfillment => ("fulfillment_policy", "fulfillmentPolicies", "fulfillmentPolicyId"),
            _ => ("return_policy", "returnPolicies", "returnPolicyId"),
        };
        var url = $"{Api(session)}/sell/account/v1/{path}?marketplace_id={Uri.EscapeDataString(session.MarketplaceId)}";
        var node = await SendAsync(session, $"account.get{collection}", HttpMethod.Get, url, null, ct);
        return (node?[collection]?.AsArray() ?? new JsonArray())
            .Where(p => p is not null)
            .Select(p => new EbayPolicy(p![idField]!.GetValue<string>(), p["name"]?.GetValue<string>() ?? p[idField]!.GetValue<string>()))
            .OrderBy(p => p.Name)
            .ToList();
    }

    public async Task CreateOrReplaceLocationAsync(
        Guid storeId, string merchantLocationKey, EbayAddress address, string name, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, allowDisabled: true, ct);
        var key = Uri.EscapeDataString(merchantLocationKey);
        var addressNode = new JsonObject
        {
            ["addressLine1"] = address.AddressLine1,
            ["addressLine2"] = address.AddressLine2,
            ["city"] = address.City,
            ["stateOrProvince"] = address.StateOrProvince,
            ["postalCode"] = address.PostalCode,
            ["country"] = address.CountryCode,
        };

        try
        {
            var create = new JsonObject
            {
                ["location"] = new JsonObject { ["address"] = addressNode },
                ["name"] = name,
                ["merchantLocationStatus"] = "ENABLED",
                ["locationTypes"] = new JsonArray("WAREHOUSE"),
                ["phone"] = address.Phone,
            };
            await SendAsync(session, "inventory.createInventoryLocation", HttpMethod.Post,
                $"{Api(session)}/sell/inventory/v1/location/{key}", create, ct);
        }
        catch (EbayApiException ex) when (ex.StatusCode == (int)HttpStatusCode.Conflict
                                          || ex.Errors.Any(e => e.Code == "25803"))
        {
            // Already exists → update its details instead.
            var update = new JsonObject
            {
                ["location"] = new JsonObject { ["address"] = addressNode.DeepClone() },
                ["name"] = name,
                ["phone"] = address.Phone,
            };
            await SendAsync(session, "inventory.updateInventoryLocation", HttpMethod.Post,
                $"{Api(session)}/sell/inventory/v1/location/{key}/update_location_details", update, ct);
        }
    }

    // ---- Taxonomy -------------------------------------------------------------------------------------

    public async Task<string> GetDefaultCategoryTreeIdAsync(Guid storeId, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, allowDisabled: true, ct);
        var url = $"{Api(session)}/commerce/taxonomy/v1/get_default_category_tree_id?marketplace_id={Uri.EscapeDataString(session.MarketplaceId)}";
        var node = await SendAsync(session, "taxonomy.getDefaultCategoryTreeId", HttpMethod.Get, url, null, ct);
        return node!["categoryTreeId"]!.GetValue<string>();
    }

    public async Task<EbayCategoryTree> GetCategoryTreeAsync(Guid storeId, string treeId, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, allowDisabled: true, ct);
        var url = $"{Api(session)}/commerce/taxonomy/v1/category_tree/{Uri.EscapeDataString(treeId)}";
        // The full tree is tens of MB: never log its body.
        var node = await SendAsync(session, "taxonomy.getCategoryTree", HttpMethod.Get, url, null, ct, logResponseBody: false);

        var categories = new List<EbayCategoryNode>();
        void Walk(JsonNode? treeNode, string? parentId)
        {
            if (treeNode is null) return;
            var id = treeNode["category"]?["categoryId"]?.GetValue<string>();
            var level = treeNode["categoryTreeNodeLevel"]?.GetValue<int>() ?? 0;
            // Level 0 is the synthetic root; its children are the top-level categories (no parent).
            if (id is not null && level > 0)
            {
                categories.Add(new EbayCategoryNode(
                    id, parentId, treeNode["category"]?["categoryName"]?.GetValue<string>() ?? id, level,
                    treeNode["leafCategoryTreeNode"]?.GetValue<bool>() ?? false));
            }

            foreach (var child in treeNode["childCategoryTreeNodes"]?.AsArray() ?? new JsonArray())
                Walk(child, level > 0 ? id : null);
        }

        Walk(node?["rootCategoryNode"], null);
        return new EbayCategoryTree(
            node?["categoryTreeId"]?.GetValue<string>() ?? treeId,
            node?["categoryTreeVersion"]?.GetValue<string>() ?? string.Empty,
            categories);
    }

    public async Task<IReadOnlyList<EbayAspectDefinition>> GetItemAspectsAsync(
        Guid storeId, string treeId, string categoryId, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, allowDisabled: true, ct);
        var url = $"{Api(session)}/commerce/taxonomy/v1/category_tree/{Uri.EscapeDataString(treeId)}"
                  + $"/get_item_aspects_for_category?category_id={Uri.EscapeDataString(categoryId)}";
        var node = await SendAsync(session, "taxonomy.getItemAspectsForCategory", HttpMethod.Get, url, null, ct);
        return (node?["aspects"]?.AsArray() ?? new JsonArray())
            .Where(a => a is not null)
            .Select(a =>
            {
                var constraint = a!["aspectConstraint"];
                return new EbayAspectDefinition(
                    a["localizedAspectName"]!.GetValue<string>(),
                    constraint?["aspectRequired"]?.GetValue<bool>() ?? false,
                    string.Equals(constraint?["itemToAspectCardinality"]?.GetValue<string>(), "MULTI", StringComparison.OrdinalIgnoreCase),
                    !string.Equals(constraint?["aspectMode"]?.GetValue<string>(), "SELECTION_ONLY", StringComparison.OrdinalIgnoreCase),
                    (a["aspectValues"]?.AsArray() ?? new JsonArray())
                        .Select(v => v?["localizedValue"]?.GetValue<string>())
                        .Where(v => !string.IsNullOrEmpty(v))
                        .Select(v => v!)
                        .ToList(),
                    constraint?["aspectMaxLength"]?.GetValue<int>());
            })
            .ToList();
    }

    // ---- Metadata -------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<EbayCondition>> GetItemConditionsAsync(Guid storeId, string categoryId, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, allowDisabled: true, ct);
        var url = $"{Api(session)}/sell/metadata/v1/marketplace/{Uri.EscapeDataString(session.MarketplaceId)}"
                  + $"/get_item_condition_policies?filter={Uri.EscapeDataString($"categoryIds:{{{categoryId}}}")}";
        var node = await SendAsync(session, "metadata.getItemConditionPolicies", HttpMethod.Get, url, null, ct);
        return (node?["itemConditionPolicies"]?.AsArray() ?? new JsonArray())
            .SelectMany(p => p?["itemConditions"]?.AsArray() ?? new JsonArray())
            .Where(c => c?["conditionId"] is not null)
            .Select(c => new EbayCondition(
                c!["conditionId"]!.GetValue<string>(),
                c["conditionDescription"]?.GetValue<string>() ?? c["conditionId"]!.GetValue<string>()))
            .DistinctBy(c => c.ConditionId)
            .ToList();
    }

    public async Task<EbayCompatibilityInfo> GetCompatibilityInfoAsync(Guid storeId, string categoryId, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, allowDisabled: true, ct);
        var url = $"{Api(session)}/sell/metadata/v1/marketplace/{Uri.EscapeDataString(session.MarketplaceId)}"
                  + $"/get_automotive_parts_compatibility_policies?filter={Uri.EscapeDataString($"categoryIds:{{{categoryId}}}")}";
        var node = await SendAsync(session, "metadata.getAutomotivePartsCompatibilityPolicies", HttpMethod.Get, url, null, ct);
        var policy = node?["automotivePartsCompatibilityPolicies"]?.AsArray()
            .FirstOrDefault(p => p?["categoryId"]?.GetValue<string>() == categoryId);
        if (policy is null)
            return new EbayCompatibilityInfo(false, 0);
        return new EbayCompatibilityInfo(true, policy["maxNumberOfCompatibleVehicles"]?.GetValue<int>() ?? 0);
    }

    // ---- Inventory ------------------------------------------------------------------------------------

    public async Task CreateOrReplaceInventoryItemAsync(Guid storeId, string sku, EbayInventoryItem item, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, ct: ct);
        var aspects = new JsonObject();
        foreach (var (name, values) in item.Aspects)
            aspects[name] = new JsonArray(values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray());

        var body = new JsonObject
        {
            ["availability"] = new JsonObject
            {
                ["shipToLocationAvailability"] = new JsonObject { ["quantity"] = item.Quantity },
            },
            ["condition"] = item.ConditionId is null ? null : ConditionEnum.GetValueOrDefault(item.ConditionId, item.ConditionId),
            ["product"] = new JsonObject
            {
                ["title"] = item.Title,
                ["subtitle"] = item.Subtitle,
                ["description"] = item.Description,
                ["aspects"] = aspects,
                ["imageUrls"] = new JsonArray(item.ImageUrls.Select(u => (JsonNode?)JsonValue.Create(u)).ToArray()),
            },
        };
        await SendAsync(session, "inventory.createOrReplaceInventoryItem", HttpMethod.Put,
            $"{Api(session)}/sell/inventory/v1/inventory_item/{Uri.EscapeDataString(sku)}", body, ct);
    }

    public async Task CreateOrReplaceProductCompatibilityAsync(
        Guid storeId, string sku, IReadOnlyList<EbayCompatibleVehicle> vehicles, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, ct: ct);
        var products = new JsonArray();
        foreach (var v in vehicles)
        {
            var family = new JsonObject
            {
                ["year"] = v.Year.ToString(CultureInfo.InvariantCulture),
                ["make"] = v.Make,
                ["model"] = v.Model,
                ["trim"] = v.Trim,
                ["engine"] = v.Engine,
            };
            products.Add(new JsonObject { ["productFamilyProperties"] = family });
        }

        await SendAsync(session, "inventory.createOrReplaceProductCompatibility", HttpMethod.Put,
            $"{Api(session)}/sell/inventory/v1/inventory_item/{Uri.EscapeDataString(sku)}/product_compatibility",
            new JsonObject { ["compatibleProducts"] = products }, ct);
    }

    public async Task<string> CreateOfferAsync(Guid storeId, EbayOffer offer, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, ct: ct);
        var body = OfferBody(offer, includeIdentity: true);
        var node = await SendAsync(session, "inventory.createOffer", HttpMethod.Post, $"{Api(session)}/sell/inventory/v1/offer", body, ct);
        return node!["offerId"]!.GetValue<string>();
    }

    public async Task UpdateOfferAsync(Guid storeId, string offerId, EbayOffer offer, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, ct: ct);
        await SendAsync(session, "inventory.updateOffer", HttpMethod.Put,
            $"{Api(session)}/sell/inventory/v1/offer/{Uri.EscapeDataString(offerId)}", OfferBody(offer, includeIdentity: false), ct);
    }

    public async Task<string> PublishOfferAsync(Guid storeId, string offerId, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, ct: ct);
        var node = await SendAsync(session, "inventory.publishOffer", HttpMethod.Post,
            $"{Api(session)}/sell/inventory/v1/offer/{Uri.EscapeDataString(offerId)}/publish", null, ct);
        return node!["listingId"]!.GetValue<string>();
    }

    public async Task WithdrawOfferAsync(Guid storeId, string offerId, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, allowDisabled: true, ct);
        await SendAsync(session, "inventory.withdrawOffer", HttpMethod.Post,
            $"{Api(session)}/sell/inventory/v1/offer/{Uri.EscapeDataString(offerId)}/withdraw", null, ct);
    }

    public async Task<IReadOnlyList<EbayPriceQuantityResult>> BulkUpdatePriceQuantityAsync(
        Guid storeId, IReadOnlyList<EbayPriceQuantityUpdate> updates, CancellationToken ct = default)
    {
        if (updates.Count == 0) return Array.Empty<EbayPriceQuantityResult>();
        if (updates.Count > 25) throw new ArgumentException("eBay accepts at most 25 updates per call.", nameof(updates));

        var session = await _auth.GetSessionAsync(storeId, ct: ct);
        var requests = new JsonArray();
        foreach (var u in updates)
        {
            var request = new JsonObject
            {
                ["sku"] = u.Sku,
                ["shipToLocationAvailability"] = new JsonObject { ["quantity"] = u.Quantity },
            };
            if (u.OfferId is not null)
            {
                var offer = new JsonObject { ["offerId"] = u.OfferId, ["availableQuantity"] = u.Quantity };
                if (u.Price is decimal price)
                    offer["price"] = Money(price, u.CurrencyCode);
                request["offers"] = new JsonArray(offer);
            }

            requests.Add(request);
        }

        // Partial failures come back as 207 with per-SKU statuses, so read them rather than throwing.
        var node = await SendAsync(session, "inventory.bulkUpdatePriceQuantity", HttpMethod.Post,
            $"{Api(session)}/sell/inventory/v1/bulk_update_price_quantity", new JsonObject { ["requests"] = requests }, ct);

        return (node?["responses"]?.AsArray() ?? new JsonArray())
            .Where(r => r is not null)
            .GroupBy(r => r!["sku"]?.GetValue<string>() ?? string.Empty)
            .Select(g =>
            {
                var errors = g.SelectMany(r => ParseErrors(r!["errors"])).ToList();
                var ok = g.All(r => (r!["statusCode"]?.GetValue<int>() ?? 200) is >= 200 and < 300) && errors.Count == 0;
                return new EbayPriceQuantityResult(g.Key, ok, errors);
            })
            .ToList();
    }

    // ---- Fulfillment ----------------------------------------------------------------------------------

    public async Task<EbayOrderPage> GetOrdersAsync(Guid storeId, DateTime modifiedSinceUtc, int offset, int limit, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, ct: ct);
        var since = DateTime.SpecifyKind(modifiedSinceUtc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        var filter = Uri.EscapeDataString($"lastmodifieddate:[{since}..]");
        var url = $"{Api(session)}/sell/fulfillment/v1/order?filter={filter}&limit={limit}&offset={offset}";
        var node = await SendAsync(session, "fulfillment.getOrders", HttpMethod.Get, url, null, ct);

        var orders = (node?["orders"]?.AsArray() ?? new JsonArray()).Where(o => o is not null).Select(o => ParseOrder(o!)).ToList();
        return new EbayOrderPage(
            orders,
            node?["total"]?.GetValue<int>() ?? orders.Count,
            node?["offset"]?.GetValue<int>() ?? offset,
            node?["limit"]?.GetValue<int>() ?? limit);
    }

    public async Task<string> CreateShippingFulfillmentAsync(
        Guid storeId, string orderId, IReadOnlyList<(string LineItemId, int Quantity)> lineItems,
        string carrierCode, string trackingNumber, CancellationToken ct = default)
    {
        var session = await _auth.GetSessionAsync(storeId, allowDisabled: true, ct);
        var body = new JsonObject
        {
            ["lineItems"] = new JsonArray(lineItems
                .Select(l => (JsonNode?)new JsonObject { ["lineItemId"] = l.LineItemId, ["quantity"] = l.Quantity })
                .ToArray()),
            ["shippedDate"] = _clock.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            ["shippingCarrierCode"] = carrierCode,
            ["trackingNumber"] = trackingNumber,
        };

        var (_, location) = await SendRawAsync(session, "fulfillment.createShippingFulfillment", HttpMethod.Post,
            $"{Api(session)}/sell/fulfillment/v1/order/{Uri.EscapeDataString(orderId)}/shipping_fulfillment", body, ct);
        // 201 Created; the new fulfillment id is the last segment of the Location header.
        return location?.Segments.LastOrDefault()?.Trim('/') ?? trackingNumber;
    }

    // ---- Parsing --------------------------------------------------------------------------------------

    private static EbayOrder ParseOrder(JsonNode o)
    {
        var shippingStep = o["fulfillmentStartInstructions"]?.AsArray().FirstOrDefault()?["shippingStep"];
        var shipTo = shippingStep?["shipTo"];
        var contact = shipTo?["contactAddress"];
        var pricing = o["pricingSummary"];
        var currency = pricing?["total"]?["currency"]?.GetValue<string>() ?? "USD";

        var lines = (o["lineItems"]?.AsArray() ?? new JsonArray()).Where(l => l is not null).Select(l =>
        {
            var qty = l!["quantity"]?.GetValue<int>() ?? 1;
            var cost = Amount(l["lineItemCost"]);
            return new EbayOrderLineItem(
                l["lineItemId"]!.GetValue<string>(),
                l["legacyItemId"]?.GetValue<string>(),
                l["sku"]?.GetValue<string>(),
                l["title"]?.GetValue<string>() ?? string.Empty,
                qty,
                cost,
                qty > 0 ? Math.Round(cost / qty, 2, MidpointRounding.AwayFromZero) : cost);
        }).ToList();

        var subtotal = pricing?["priceSubtotal"] is { } ps ? Amount(ps) : lines.Sum(l => l.LineItemCost);
        var tax = pricing?["tax"] is { } t
            ? Amount(t)
            : (o["lineItems"]?.AsArray() ?? new JsonArray())
                .SelectMany(l => l?["ebayCollectAndRemitTaxes"]?.AsArray() ?? new JsonArray())
                .Sum(x => Amount(x?["amount"]));
        var total = pricing?["total"] is { } tt ? Amount(tt) : Amount(o["totalFeeBasisAmount"]);

        return new EbayOrder(
            o["orderId"]!.GetValue<string>(),
            o["orderPaymentStatus"]?.GetValue<string>() ?? string.Empty,
            DateTime.TryParse(o["creationDate"]?.GetValue<string>(), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var created) ? created : DateTime.UtcNow,
            o["buyer"]?["username"]?.GetValue<string>(),
            o["buyer"]?["buyerRegistrationAddress"]?["email"]?.GetValue<string>(),
            o["buyerCheckoutNotes"]?.GetValue<string>(),
            shipTo is null
                ? null
                : new EbayAddress(
                    shipTo["fullName"]?.GetValue<string>(),
                    contact?["addressLine1"]?.GetValue<string>(),
                    contact?["addressLine2"]?.GetValue<string>(),
                    contact?["city"]?.GetValue<string>(),
                    contact?["stateOrProvince"]?.GetValue<string>(),
                    contact?["postalCode"]?.GetValue<string>(),
                    contact?["countryCode"]?.GetValue<string>(),
                    shipTo["primaryPhone"]?["phoneNumber"]?.GetValue<string>()),
            shipTo?["email"]?.GetValue<string>(),
            shippingStep?["shippingServiceCode"]?.GetValue<string>(),
            subtotal,
            Amount(pricing?["deliveryCost"]),
            tax,
            total,
            currency,
            o["paymentSummary"]?["payments"]?.AsArray().FirstOrDefault()?["paymentMethod"]?.GetValue<string>(),
            lines);
    }

    private static decimal Amount(JsonNode? money)
    {
        var value = money?["value"];
        if (value is null) return 0m;
        return decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static JsonObject Money(decimal value, string currency) => new()
    {
        ["value"] = value.ToString("0.00", CultureInfo.InvariantCulture),
        ["currency"] = currency,
    };

    private static JsonObject OfferBody(EbayOffer offer, bool includeIdentity)
    {
        var body = new JsonObject
        {
            ["availableQuantity"] = offer.Quantity,
            ["categoryId"] = offer.CategoryId,
            ["listingDescription"] = offer.ListingDescription,
            ["listingDuration"] = "GTC",
            ["listingPolicies"] = new JsonObject
            {
                ["paymentPolicyId"] = offer.PaymentPolicyId,
                ["fulfillmentPolicyId"] = offer.FulfillmentPolicyId,
                ["returnPolicyId"] = offer.ReturnPolicyId,
            },
            ["pricingSummary"] = new JsonObject { ["price"] = Money(offer.Price, offer.CurrencyCode) },
            ["merchantLocationKey"] = offer.MerchantLocationKey,
        };
        if (includeIdentity)
        {
            body["sku"] = offer.Sku;
            body["marketplaceId"] = offer.MarketplaceId;
            body["format"] = "FIXED_PRICE";
        }

        return body;
    }

    /// <summary>eBay error envelope → error-severity messages (the "warnings" array is ignored).</summary>
    private static List<ExternalError> ParseErrors(JsonNode? errors) =>
        (errors?.AsArray() ?? new JsonArray())
            .Where(e => e is not null)
            .Where(e => !string.Equals(e!["category"]?.GetValue<string>(), "WARNING", StringComparison.OrdinalIgnoreCase))
            .Select(e => new ExternalError(
                e!["errorId"]?.ToString(),
                e["longMessage"]?.GetValue<string>() is { Length: > 0 } longMessage ? longMessage : e["message"]?.GetValue<string>() ?? "eBay error",
                e["parameters"]?.AsArray().FirstOrDefault()?["value"]?.GetValue<string>()))
            .ToList();

    // ---- Transport ------------------------------------------------------------------------------------

    private static string Api(EbaySession session) => EbayOptions.ApiBase(session.Environment);

    private async Task<JsonNode?> SendAsync(
        EbaySession session, string operation, HttpMethod method, string url, JsonNode? body, CancellationToken ct,
        bool logResponseBody = true)
    {
        var (node, _) = await SendRawAsync(session, operation, method, url, body, ct, logResponseBody);
        return node;
    }

    private async Task<(JsonNode? Body, Uri? Location)> SendRawAsync(
        EbaySession session, string operation, HttpMethod method, string url, JsonNode? body, CancellationToken ct,
        bool logResponseBody = true)
    {
        var requestJson = body?.ToJsonString(Json);
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-EBAY-C-MARKETPLACE-ID", session.MarketplaceId);
        request.Headers.TryAddWithoutValidation("Accept-Language", ContentLanguage);
        if (requestJson is not null)
        {
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentLanguage.Add(ContentLanguage);
        }
        else if (method == HttpMethod.Post)
        {
            request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        }

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string? responseText = null;
        try
        {
            response = await _http.SendAsync(request, ct);
            responseText = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            await LogAsync(session, operation, method, url, requestJson, null, ex.Message, false, stopwatch, logResponseBody);
            throw new EbayApiException(0, new[] { new ExternalError("transport", "eBay could not be reached. Try again shortly.") });
        }

        var success = response.IsSuccessStatusCode;
        await LogAsync(session, operation, method, url, requestJson, (int)response.StatusCode, responseText, success, stopwatch, logResponseBody);

        JsonNode? parsed = null;
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            try { parsed = JsonNode.Parse(responseText); }
            catch (JsonException) { parsed = null; }
        }

        if (!success)
        {
            var errors = ParseErrors(parsed?["errors"]);
            if (errors.Count == 0)
                errors.Add(new ExternalError(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                    $"eBay returned HTTP {(int)response.StatusCode} for {operation}."));
            var status = (int)response.StatusCode;
            response.Dispose();
            if (status == (int)HttpStatusCode.Unauthorized)
                throw new ServiceUnavailableException(EbayAuthService.ReconnectMessage);
            throw new EbayApiException(status, errors);
        }

        var location = response.Headers.Location;
        response.Dispose();
        return (parsed, location);
    }

    private async Task LogAsync(
        EbaySession session, string operation, HttpMethod method, string url, string? requestBody,
        int? status, string? responseBody, bool success, Stopwatch stopwatch, bool logResponseBody)
    {
        if (!session.LoggingEnabled)
            return;

        await _log.WriteAsync(new EbayApiLog
        {
            StoreId = session.StoreId,
            Operation = operation,
            HttpMethod = method.Method,
            Url = EbayRedactor.Redact(url) ?? url,
            RequestBody = EbayRedactor.Redact(requestBody),
            ResponseStatus = status,
            ResponseBody = logResponseBody ? EbayRedactor.Redact(Truncate(responseBody)) : "(omitted)",
            Success = success,
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            CreatedOnUtc = _clock.UtcNow,
        });
    }

    private static string? Truncate(string? text) => text is { Length: > 100_000 } ? text[..100_000] + "…(truncated)" : text;
}
