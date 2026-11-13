using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Infrastructure.Shipping.Carriers;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Shipping;

/// <summary>
/// FedEx REST rating (docs/fedex-shipping-requirements.md). Case ids (FX-T…) are the spec's §6 acceptance
/// tests; FedEx itself is replaced by a recording fake handler.
/// </summary>
public class FedExCarrierClientTests : CatalogTestBase
{
    private const decimal KgPerPound = 0.45359237m;
    private static readonly CarrierAddress Origin =
        new("US", "NC", "28270", null, null, "Charlotte", "3334 Gray Moss Rd", null, "HK Parts");
    private static readonly CarrierAddress NewYork =
        new("US", "NY", "10001", null, null, "New York", "350 5th Ave", "Suite 100", "Jane Buyer");

    private sealed class FakeFedEx : HttpMessageHandler
    {
        public readonly List<JsonElement> RateCalls = new();
        public int TokenCalls;
        public HttpStatusCode TokenStatus = HttpStatusCode.OK;
        public HttpStatusCode RateStatus = HttpStatusCode.OK;

        /// <summary>Service type → (ACCOUNT, LIST) shipment totals.</summary>
        public readonly Dictionary<string, (decimal Account, decimal List)> Rates = new()
        {
            ["GROUND_HOME_DELIVERY"] = (11.20m, 14.75m),
            ["FEDEX_2_DAY"] = (24.10m, 31.40m),
            ["PRIORITY_OVERNIGHT"] = (52.30m, 66.00m),
            ["FIRST_OVERNIGHT"] = (88.00m, 99.00m),
            ["FEDEX_1_DAY_FREIGHT"] = (300.00m, 350.00m),
        };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/oauth/token")
            {
                Interlocked.Increment(ref TokenCalls);
                return TokenStatus == HttpStatusCode.OK
                    ? Json(HttpStatusCode.OK, "{\"access_token\":\"tok\",\"token_type\":\"bearer\",\"expires_in\":3599}")
                    : Json(TokenStatus, "{\"errors\":[{\"code\":\"NOT.AUTHORIZED.ERROR\",\"message\":\"The given client credentials were not valid.\"}]}");
            }

            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            lock (RateCalls)
                RateCalls.Add(body);
            if (RateStatus != HttpStatusCode.OK)
                return Json(RateStatus, "{\"errors\":[{\"code\":\"SYSTEM.UNAVAILABLE\",\"message\":\"down\"}]}");

            // Reply only with the requested rate type, as FedEx does.
            var type = body.GetProperty("requestedShipment").GetProperty("rateRequestType")[0].GetString()!;
            var details = Rates.Select(kv =>
                $"{{\"serviceType\":\"{kv.Key}\",\"ratedShipmentDetails\":[{{\"rateType\":\"{type}\"," +
                $"\"totalNetCharge\":{(type == "LIST" ? kv.Value.List : kv.Value.Account)}}}]," +
                "\"commit\":{\"transitDays\":{\"minimumTransitTime\":\"TWO_DAYS\"}}}");
            return Json(HttpStatusCode.OK, $"{{\"output\":{{\"rateReplyDetails\":[{string.Join(",", details)}]}}}}");
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json)
            => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private sealed class Factory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public Factory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    /// <summary>Unique host per test: the token cache is process-wide and keyed by host.</summary>
    private FedExCredential Seed(Action<FedExCredential>? configure = null)
    {
        var credential = new FedExCredential
        {
            Name = "Test",
            Active = true,
            BaseUrl = $"https://{Guid.NewGuid():N}.fedex.test",
            ApiKey = "id",
            ApiSecret = "secret",
            AccountNumber = "740561073",
            AllowedServices = string.Join(",", FedExCodes.DefaultServices),
        };
        configure?.Invoke(credential);
        using var db = NewContext();
        db.FedExCredentials.Add(credential);
        db.SaveChanges();
        return credential;
    }

    private FedExCarrierClient Client(FakeFedEx fake, IMemoryCache? cache = null)
        => new(new Factory(fake), NewContext(), cache ?? new MemoryCache(new MemoryCacheOptions()),
            NullLogger<FedExCarrierClient>.Instance);

    private static ShippingRateLine Line(decimal weightLb, int qty = 1, bool ffl = false,
        decimal? l = null, decimal? w = null, decimal? h = null)
        => new(Guid.NewGuid(), qty, 50m, ffl, false, null, weightLb, l, w, h, null, null, 1, false);

    private static CarrierRateRequest Req(decimal pounds, params ShippingRateLine[] lines)
        => new(Origin, NewYork, pounds * KgPerPound, null, null, null, 100m, lines.Length > 0 ? lines : null);

    private static JsonElement Shipment(JsonElement body) => body.GetProperty("requestedShipment");

    [Fact]
    public async Task FX_T1_lists_allowed_services_named_after_the_method()
    {
        Seed();
        var fake = new FakeFedEx();
        var options = await Client(fake).GetRatesAsync(Req(5m), CancellationToken.None);

        // FIRST_OVERNIGHT and the freight service are quoted but not in the default (FX-19) list.
        Assert.Equal(
            new[] { "FedEx - Ground Home Delivery", "FedEx - 2 Day", "FedEx - Priority Overnight" },
            options.Select(o => o.Name).ToArray());
        Assert.All(options, o => Assert.Equal(2, o.EstimatedDeliveryDays));
    }

    [Fact]
    public async Task FX_T2_account_and_list_modes_request_and_read_only_their_own_rate_type()
    {
        Seed(c => c.AllowedServices = "GROUND_HOME_DELIVERY");
        var accountFake = new FakeFedEx();
        var account = Assert.Single(await Client(accountFake).GetRatesAsync(Req(5m), CancellationToken.None));
        Assert.Equal(11.20m, account.Rate);
        Assert.Equal("ACCOUNT", Shipment(accountFake.RateCalls.Single()).GetProperty("rateRequestType").EnumerateArray().Single().GetString());

        Seed(c => { c.AllowedServices = "GROUND_HOME_DELIVERY"; c.RateMode = "LIST"; c.UpdatedOnUtc = DateTime.UtcNow.AddMinutes(1); });
        var listFake = new FakeFedEx();
        var list = Assert.Single(await Client(listFake).GetRatesAsync(Req(5m), CancellationToken.None));
        Assert.Equal(14.75m, list.Rate);
    }

    [Fact]
    public async Task FX_T3_handling_fee_is_added_to_every_option()
    {
        Seed(c => c.HandlingFee = 2.00m);
        var options = await Client(new FakeFedEx()).GetRatesAsync(Req(5m), CancellationToken.None);
        Assert.Equal(new[] { 13.20m, 26.10m, 54.30m }, options.Select(o => o.Rate).ToArray());
    }

    [Fact]
    public async Task FX_T4_only_ground_when_only_ground_allowed()
    {
        Seed(c => c.AllowedServices = "GROUND_HOME_DELIVERY");
        var options = await Client(new FakeFedEx()).GetRatesAsync(Req(5m), CancellationToken.None);
        Assert.Equal("FedEx - Ground Home Delivery", Assert.Single(options).Name);
    }

    [Fact]
    public async Task FX_T5_zero_weight_makes_no_call()
    {
        Seed();
        var fake = new FakeFedEx();
        Assert.Empty(await Client(fake).GetRatesAsync(Req(0m), CancellationToken.None));
        Assert.Empty(fake.RateCalls);
        Assert.Equal(0, fake.TokenCalls);
    }

    [Fact]
    public async Task FX_T6_wrong_secret_returns_nothing_without_throwing()
    {
        Seed();
        var fake = new FakeFedEx { TokenStatus = HttpStatusCode.Unauthorized };
        Assert.Empty(await Client(fake).GetRatesAsync(Req(5m), CancellationToken.None));
        Assert.Empty(fake.RateCalls);
    }

    [Fact]
    public async Task FX_53_a_5xx_is_retried_once_then_fedex_is_dropped()
    {
        Seed();
        var fake = new FakeFedEx { RateStatus = HttpStatusCode.ServiceUnavailable };
        Assert.Empty(await Client(fake).GetRatesAsync(Req(5m), CancellationToken.None));
        Assert.Equal(2, fake.RateCalls.Count);
    }

    [Fact]
    public async Task FX_T8_product_dimensions_and_weight_are_sent_per_unit()
    {
        Seed(c => c.SendPackageInfo = true);
        var fake = new FakeFedEx();
        await Client(fake).GetRatesAsync(Req(0m, Line(3m, qty: 2, l: 20m, w: 12m, h: 8m)), CancellationToken.None);

        var shipment = Shipment(fake.RateCalls.Single());
        Assert.Equal(2, shipment.GetProperty("totalPackageCount").GetInt32());
        var item = shipment.GetProperty("requestedPackageLineItems")[0];
        Assert.Equal("LB", item.GetProperty("weight").GetProperty("units").GetString());
        Assert.Equal(3m, item.GetProperty("weight").GetProperty("value").GetDecimal());
        var dims = item.GetProperty("dimensions");
        Assert.Equal(20, dims.GetProperty("length").GetInt32());
        Assert.Equal(12, dims.GetProperty("width").GetInt32());
        Assert.Equal(8, dims.GetProperty("height").GetInt32());
        Assert.Equal("IN", dims.GetProperty("units").GetString());
    }

    [Fact]
    public async Task Default_package_is_used_when_the_product_has_no_dimensions()
    {
        Seed(c => { c.DefaultLengthIn = 11m; c.DefaultWidthIn = 11m; c.DefaultHeightIn = 11m; });
        var fake = new FakeFedEx();
        await Client(fake).GetRatesAsync(Req(0m, Line(3m)), CancellationToken.None);

        var item = Assert.Single(Shipment(fake.RateCalls.Single()).GetProperty("requestedPackageLineItems").EnumerateArray());
        Assert.Equal(11, item.GetProperty("dimensions").GetProperty("length").GetInt32());
    }

    [Fact]
    public async Task FX_T9_ffl_cart_is_not_residential_and_ffl_filter_applies_when_set()
    {
        Seed(c => c.FflAllowedServices = "PRIORITY_OVERNIGHT,STANDARD_OVERNIGHT");
        var fake = new FakeFedEx();
        var options = await Client(fake).GetRatesAsync(Req(0m, Line(2m, ffl: true)), CancellationToken.None);

        Assert.Equal("PRIORITY_OVERNIGHT", Assert.Single(options).MethodId);
        Assert.False(Shipment(fake.RateCalls.Single()).GetProperty("recipient").GetProperty("address").GetProperty("residential").GetBoolean());
    }

    [Fact]
    public async Task Ffl_cart_without_an_ffl_list_keeps_all_allowed_services()
    {
        Seed();
        var options = await Client(new FakeFedEx()).GetRatesAsync(Req(0m, Line(2m, ffl: true)), CancellationToken.None);
        Assert.Equal(3, options.Count);
    }

    [Fact]
    public async Task FX_17_18_customer_address_is_residential_with_every_street_line()
    {
        Seed();
        var fake = new FakeFedEx();
        await Client(fake).GetRatesAsync(Req(5m), CancellationToken.None);

        var body = fake.RateCalls.Single();
        var recipient = Shipment(body).GetProperty("recipient").GetProperty("address");
        Assert.True(recipient.GetProperty("residential").GetBoolean());
        Assert.Equal(new[] { "350 5th Ave", "Suite 100" },
            recipient.GetProperty("streetLines").EnumerateArray().Select(e => e.GetString()).ToArray());
        Assert.Equal("NY", recipient.GetProperty("stateOrProvinceCode").GetString());
        Assert.Equal("740561073", body.GetProperty("accountNumber").GetProperty("value").GetString());
        Assert.Equal("USE_SCHEDULED_PICKUP", Shipment(body).GetProperty("pickupType").GetString());
    }

    [Fact]
    public async Task FX_T10_second_identical_quote_is_cached_and_one_token_serves_all()
    {
        Seed();
        var fake = new FakeFedEx();
        var cache = new MemoryCache(new MemoryCacheOptions());
        await Client(fake, cache).GetRatesAsync(Req(5m), CancellationToken.None);
        await Client(fake, cache).GetRatesAsync(Req(5m), CancellationToken.None);
        await Client(fake, cache).GetRatesAsync(Req(7m), CancellationToken.None);

        Assert.Equal(2, fake.RateCalls.Count);
        Assert.Equal(1, fake.TokenCalls);
    }

    [Fact]
    public async Task Minimum_order_and_ship_to_countries_gate_fedex()
    {
        Seed(c => { c.MinimumOrderAmount = 150m; });
        Assert.Empty(await Client(new FakeFedEx()).GetRatesAsync(Req(5m), CancellationToken.None));
    }

    [Fact]
    public async Task FX_T11_test_reports_token_expiry_and_quotes_or_the_fedex_error()
    {
        var credential = Seed();
        var ok = await Client(new FakeFedEx()).TestCredentialAsync(credential.Id, CancellationToken.None);
        Assert.True(ok.TokenOk);
        Assert.NotNull(ok.TokenValidUntil);
        Assert.Equal(3, ok.Domestic.Count(l => l.Price.HasValue));

        var bad = await Client(new FakeFedEx { TokenStatus = HttpStatusCode.Unauthorized })
            .TestCredentialAsync(credential.Id, CancellationToken.None);
        Assert.False(bad.TokenOk);
        Assert.Contains("NOT.AUTHORIZED.ERROR", bad.TokenError);
    }

    [Fact]
    public async Task Missing_account_number_quotes_nothing()
    {
        Seed(c => c.AccountNumber = null);
        Assert.Empty(await Client(new FakeFedEx()).GetRatesAsync(Req(5m), CancellationToken.None));
    }
}
