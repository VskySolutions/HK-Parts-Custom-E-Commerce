using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Ebay;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Ebay;

namespace HKPartsEcommerce.Application.Tests.Ebay;

/// <summary>A scriptable, call-recording <see cref="IEbayClient"/>.</summary>
internal sealed class FakeEbayClient : IEbayClient
{
    public List<string> Calls { get; } = new();
    public List<IReadOnlyList<EbayPriceQuantityUpdate>> BulkUpdates { get; } = new();
    public List<(string OrderId, IReadOnlyList<(string LineItemId, int Quantity)> Lines, string Carrier, string Tracking)> Fulfillments { get; } = new();
    public EbayInventoryItem? LastInventoryItem { get; private set; }

    public List<EbayAspectDefinition> Aspects { get; set; } = new();
    public EbayCompatibilityInfo Compatibility { get; set; } = new(false, 0);
    public EbayApiException? PublishError { get; set; }
    public EbayApiException? WithdrawError { get; set; }
    public List<EbayOrder> Orders { get; set; } = new();
    public int OrderPageSize { get; set; } = 200;

    /// <summary>Calls that write to eBay (anything but metadata reads).</summary>
    public IEnumerable<string> WriteCalls => Calls.Where(c => !c.StartsWith("Get", StringComparison.Ordinal));

    public Task<string?> GetUsernameAsync(Guid storeId, CancellationToken ct = default) { Calls.Add(nameof(GetUsernameAsync)); return Task.FromResult<string?>("seller1"); }

    public Task<IReadOnlyList<EbayPolicy>> GetPoliciesAsync(Guid storeId, EbayPolicyType type, CancellationToken ct = default)
    {
        Calls.Add(nameof(GetPoliciesAsync));
        return Task.FromResult<IReadOnlyList<EbayPolicy>>(new[] { new EbayPolicy($"{type}-1", $"{type} policy") });
    }

    public Task CreateOrReplaceLocationAsync(Guid storeId, string merchantLocationKey, EbayAddress address, string name, CancellationToken ct = default)
    { Calls.Add(nameof(CreateOrReplaceLocationAsync)); return Task.CompletedTask; }

    public Task<string> GetDefaultCategoryTreeIdAsync(Guid storeId, CancellationToken ct = default) { Calls.Add(nameof(GetDefaultCategoryTreeIdAsync)); return Task.FromResult("0"); }

    public Task<EbayCategoryTree> GetCategoryTreeAsync(Guid storeId, string treeId, CancellationToken ct = default)
    {
        Calls.Add(nameof(GetCategoryTreeAsync));
        return Task.FromResult(new EbayCategoryTree(treeId, "130", new[]
        {
            new EbayCategoryNode("6000", null, "eBay Motors", 1, false),
            new EbayCategoryNode("33559", "6000", "Brake Pads", 2, true),
        }));
    }

    public Task<IReadOnlyList<EbayAspectDefinition>> GetItemAspectsAsync(Guid storeId, string treeId, string categoryId, CancellationToken ct = default)
    { Calls.Add(nameof(GetItemAspectsAsync)); return Task.FromResult<IReadOnlyList<EbayAspectDefinition>>(Aspects); }

    public Task<IReadOnlyList<EbayCondition>> GetItemConditionsAsync(Guid storeId, string categoryId, CancellationToken ct = default)
    { Calls.Add(nameof(GetItemConditionsAsync)); return Task.FromResult<IReadOnlyList<EbayCondition>>(new[] { new EbayCondition("1000", "New") }); }

    public Task<EbayCompatibilityInfo> GetCompatibilityInfoAsync(Guid storeId, string categoryId, CancellationToken ct = default)
    { Calls.Add(nameof(GetCompatibilityInfoAsync)); return Task.FromResult(Compatibility); }

    public Task CreateOrReplaceInventoryItemAsync(Guid storeId, string sku, EbayInventoryItem item, CancellationToken ct = default)
    { Calls.Add(nameof(CreateOrReplaceInventoryItemAsync)); LastInventoryItem = item; return Task.CompletedTask; }

    public Task CreateOrReplaceProductCompatibilityAsync(Guid storeId, string sku, IReadOnlyList<EbayCompatibleVehicle> vehicles, CancellationToken ct = default)
    { Calls.Add(nameof(CreateOrReplaceProductCompatibilityAsync)); return Task.CompletedTask; }

    public Task<string> CreateOfferAsync(Guid storeId, EbayOffer offer, CancellationToken ct = default)
    { Calls.Add(nameof(CreateOfferAsync)); return Task.FromResult("OFFER-1"); }

    public Task UpdateOfferAsync(Guid storeId, string offerId, EbayOffer offer, CancellationToken ct = default)
    { Calls.Add(nameof(UpdateOfferAsync)); return Task.CompletedTask; }

    public Task<string> PublishOfferAsync(Guid storeId, string offerId, CancellationToken ct = default)
    {
        Calls.Add(nameof(PublishOfferAsync));
        return PublishError is null ? Task.FromResult("110551234567") : Task.FromException<string>(PublishError);
    }

    public Task WithdrawOfferAsync(Guid storeId, string offerId, CancellationToken ct = default)
    {
        Calls.Add(nameof(WithdrawOfferAsync));
        return WithdrawError is null ? Task.CompletedTask : Task.FromException(WithdrawError);
    }

    public Task<IReadOnlyList<EbayPriceQuantityResult>> BulkUpdatePriceQuantityAsync(Guid storeId, IReadOnlyList<EbayPriceQuantityUpdate> updates, CancellationToken ct = default)
    {
        Calls.Add(nameof(BulkUpdatePriceQuantityAsync));
        BulkUpdates.Add(updates);
        return Task.FromResult<IReadOnlyList<EbayPriceQuantityResult>>(
            updates.Select(u => new EbayPriceQuantityResult(u.Sku, true, Array.Empty<ExternalError>())).ToList());
    }

    public Task<EbayOrderPage> GetOrdersAsync(Guid storeId, DateTime modifiedSinceUtc, int offset, int limit, CancellationToken ct = default)
    {
        Calls.Add(nameof(GetOrdersAsync));
        var page = Orders.Skip(offset).Take(Math.Min(limit, OrderPageSize)).ToList();
        return Task.FromResult(new EbayOrderPage(page, Orders.Count, offset, limit));
    }

    public Task<string> CreateShippingFulfillmentAsync(Guid storeId, string orderId, IReadOnlyList<(string LineItemId, int Quantity)> lineItems, string carrierCode, string trackingNumber, CancellationToken ct = default)
    {
        Calls.Add(nameof(CreateShippingFulfillmentAsync));
        Fulfillments.Add((orderId, lineItems, carrierCode, trackingNumber));
        return Task.FromResult("FUL-1");
    }
}

internal sealed class FakeUrls : IEbayPublicUrlProvider
{
    public string? ToPublicImageUrl(string? url) => string.IsNullOrEmpty(url) ? null : url.StartsWith("http") ? url : "https://cdn.test" + url;
    public string AdminSettingsUrl(string? query = null) => "https://admin.test/settings/ebay?" + query;
}

internal sealed class RecordingJobQueue : IEbayJobQueue
{
    public List<(Guid? StoreId, IReadOnlyCollection<Guid>? Products)> InventorySyncs { get; } = new();
    public HashSet<Guid> OutOfStock { get; } = new();
    public List<Guid> Shipments { get; } = new();

    public void EnqueueInventorySync(Guid? storeId, IEnumerable<Guid>? productIds = null) => InventorySyncs.Add((storeId, productIds?.ToList()));
    public void EnqueueOutOfStock(IEnumerable<Guid> productIds) => OutOfStock.UnionWith(productIds);
    public void EnqueueShipmentSync(Guid shipmentId) => Shipments.Add(shipmentId);
    public void EnqueueCategoryRefresh(Guid storeId) { }
    public void EnqueueOrderImport(Guid storeId, bool force = false) { }
}

internal sealed class RecordingLogSink : IEbayApiLogSink
{
    public List<EbayApiLog> Rows { get; } = new();
    public Task WriteAsync(EbayApiLog entry, CancellationToken ct = default) { Rows.Add(entry); return Task.CompletedTask; }
}

internal sealed class FakeAuth : IEbayAuthService
{
    public bool LoggingEnabled { get; set; }
    public const string Token = "v^1.1#i^1#SECRET-ACCESS-TOKEN";

    public Task<EbaySession> GetSessionAsync(Guid storeId, bool allowDisabled = false, CancellationToken ct = default) =>
        Task.FromResult(new EbaySession(storeId, EbayEnvironment.Sandbox, "EBAY_US", Token, LoggingEnabled));
}

internal sealed class FakeOAuthClient : IEbayOAuthClient
{
    public int RefreshCalls { get; private set; }
    public bool FailRefresh { get; set; }
    public DateTime Now { get; set; } = DateTime.UtcNow;

    public IReadOnlyList<string> Scopes => new[] { "https://api.ebay.com/oauth/api_scope" };
    public bool IsConfigured(EbayEnvironment environment) => true;
    public string BuildAuthorizeUrl(EbayEnvironment environment, string state) => "https://auth.sandbox.ebay.com/oauth2/authorize?state=" + state;

    public Task<EbayTokenResult> ExchangeCodeAsync(EbayEnvironment environment, string code, CancellationToken ct = default) =>
        Task.FromResult(new EbayTokenResult("access-from-code", Now.AddHours(2), "refresh-from-code", Now.AddDays(500)));

    public Task<EbayTokenResult> RefreshAsync(EbayEnvironment environment, string refreshToken, CancellationToken ct = default)
    {
        RefreshCalls++;
        if (FailRefresh)
            throw new EbayApiException(400, new[] { new ExternalError("invalid_grant", "the refresh token is invalid") });
        return Task.FromResult(new EbayTokenResult("refreshed-access", Now.AddHours(2), null, null));
    }
}

/// <summary>Scripted HTTP responses for the real <see cref="EbayClient"/>.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    public List<HttpRequestMessage> Requests { get; } = new();

    public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_respond(request));
    }
}
