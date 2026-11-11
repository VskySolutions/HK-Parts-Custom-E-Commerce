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
/// UPS REST rating (docs/ups-shipping-requirements.md). Case ids (UP-T…) are the spec's §6 acceptance
/// tests; UPS itself is replaced by a recording fake handler.
/// </summary>
public class UpsCarrierClientTests : CatalogTestBase
{
    private const decimal KgPerPound = 0.45359237m;
    private static readonly CarrierAddress Origin =
        new("US", "NC", "28270", null, null, "Charlotte", "3334 Gray Moss Rd", null, "HK Parts");
    private static readonly CarrierAddress NewYork =
        new("US", "NY", "10001", null, null, "New York", "350 5th Ave", "Suite 100", "Jane Buyer");

    private sealed class FakeUps : HttpMessageHandler
    {
        public readonly List<(HttpRequestMessage Request, JsonElement Body)> RateCalls = new();
        public int TokenCalls;
        public HttpStatusCode TokenStatus = HttpStatusCode.OK;
        public bool SingleObject;

        /// <summary>Service code → (published, negotiated) shipment totals.</summary>
        public readonly Dictionary<string, (decimal Published, decimal Negotiated)> Rates = new()
        {
            ["03"] = (12.40m, 10.10m),
            ["02"] = (25.00m, 21.00m),
            ["01"] = (48.90m, 40.00m),
            ["13"] = (44.10m, 37.00m),
            ["07"] = (90.00m, 80.00m),
        };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/security/v1/oauth/token"))
            {
                Interlocked.Increment(ref TokenCalls);
                return TokenStatus == HttpStatusCode.OK
                    ? Json(HttpStatusCode.OK, "{\"access_token\":\"tok\",\"expires_in\":\"14399\"}")
                    : Json(TokenStatus, "{\"response\":{\"errors\":[{\"code\":\"250002\",\"message\":\"Invalid Authentication Information.\"}]}}");
            }

            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            lock (RateCalls)
                RateCalls.Add((request, body));

            static string Shipment(string code, (decimal P, decimal N) r) =>
                $"{{\"Service\":{{\"Code\":\"{code}\"}}," +
                $"\"TotalCharges\":{{\"CurrencyCode\":\"USD\",\"MonetaryValue\":\"{r.P:F2}\"}}," +
                $"\"NegotiatedRateCharges\":{{\"TotalCharge\":{{\"CurrencyCode\":\"USD\",\"MonetaryValue\":\"{r.N:F2}\"}}}}," +
                // A package-level charge that must NOT be read (UP-17).
                $"\"RatedPackage\":[{{\"TotalCharges\":{{\"MonetaryValue\":\"1.00\"}}}}]}}";

            var rated = SingleObject
                ? Shipment("03", Rates["03"])
                : "[" + string.Join(",", Rates.Select(kv => Shipment(kv.Key, kv.Value))) + "]";
            return Json(HttpStatusCode.OK, $"{{\"RateResponse\":{{\"RatedShipment\":{rated}}}}}");
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
    private UpsCredential Seed(Action<UpsCredential>? configure = null)
    {
        var credential = new UpsCredential
        {
            Name = "Test",
            Active = true,
            BaseUrl = $"https://{Guid.NewGuid():N}.ups.test",
            ClientId = "id",
            ClientSecret = "secret",
            MerchantId = "A1B2C3",
            AllowedServices = "03,12,02,13,01",
            FflAllowedServices = "01,13,14",
        };
        configure?.Invoke(credential);
        using var db = NewContext();
        db.UpsCredentials.Add(credential);
        db.SaveChanges();
        return credential;
    }

    private UpsCarrierClient Client(FakeUps fake, IMemoryCache? cache = null)
        => new(new Factory(fake), NewContext(), cache ?? new MemoryCache(new MemoryCacheOptions()),
            NullLogger<UpsCarrierClient>.Instance);

    private static ShippingRateLine Line(decimal weightLb, int qty = 1, bool ffl = false, decimal? dim = null)
        => new(Guid.NewGuid(), qty, 50m, ffl, false, null, weightLb, dim, dim, dim, null, null, 1, false);

    private static CarrierRateRequest Req(decimal pounds, params ShippingRateLine[] lines)
        => new(Origin, NewYork, pounds * KgPerPound, null, null, null, 100m, lines.Length > 0 ? lines : null);

    private static JsonElement Shipment(JsonElement body) => body.GetProperty("RateRequest").GetProperty("Shipment");

    [Fact]
    public async Task UP_T1_lists_allowed_services_named_after_the_method()
    {
        Seed();
        var fake = new FakeUps();
        var options = await Client(fake).GetRatesAsync(Req(5m), CancellationToken.None);

        // 07 is quoted by UPS but not allowed.
        Assert.Equal(
            new[] { "UPS - Ground", "UPS - 2nd Day Air", "UPS - Next Day Air Saver", "UPS - Next Day Air" },
            options.Select(o => o.Name).ToArray());
        Assert.EndsWith("/api/rating/v2409/Shoptimeintransit", fake.RateCalls.Single().Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task UP_T2_only_ground_when_only_03_allowed()
    {
        Seed(c => c.AllowedServices = "03");
        var options = await Client(new FakeUps()).GetRatesAsync(Req(5m), CancellationToken.None);
        Assert.Equal("UPS - Ground", Assert.Single(options).Name);
    }

    [Fact]
    public async Task UP_T3_negotiated_shipment_total_when_on_published_when_off()
    {
        Seed(c => { c.AllowedServices = "03"; c.UseNegotiatedRates = true; });
        var fake = new FakeUps();
        Assert.Equal(10.10m, Assert.Single(await Client(fake).GetRatesAsync(Req(5m), CancellationToken.None)).Rate);
        Assert.True(Shipment(fake.RateCalls.Single().Body).TryGetProperty("ShipmentRatingOptions", out _));
    }

    [Fact]
    public async Task UP_T17_reads_the_shipment_total_not_a_package_charge()
    {
        Seed(c => c.AllowedServices = "03");
        var fake = new FakeUps();
        var option = Assert.Single(await Client(fake).GetRatesAsync(Req(5m), CancellationToken.None));
        Assert.Equal(12.40m, option.Rate);
        Assert.False(Shipment(fake.RateCalls.Single().Body).TryGetProperty("ShipmentRatingOptions", out _));
    }

    [Fact]
    public async Task UP_T4_send_package_info_sends_one_package_per_unit()
    {
        Seed(c => { c.AllowedServices = "03"; c.SendPackageInfo = true; });
        var fake = new FakeUps();
        var options = await Client(fake).GetRatesAsync(Req(0m, Line(1.23m, qty: 2, dim: 10m), Line(4m, dim: 6m)), CancellationToken.None);

        var shipment = Shipment(fake.RateCalls.Single().Body);
        var packages = shipment.GetProperty("Package").EnumerateArray().ToList();
        Assert.Equal(3, packages.Count);
        // UP-20: rounded up to 0.1 lb.
        Assert.Equal("1.3", packages[0].GetProperty("PackageWeight").GetProperty("Weight").GetString());
        Assert.Equal("10.0", packages[0].GetProperty("Dimensions").GetProperty("Length").GetString());
        Assert.Equal(12.40m, Assert.Single(options).Rate);
    }

    [Fact]
    public async Task Package_info_off_sends_one_package_of_the_line_weight()
    {
        Seed(c => c.AllowedServices = "03");
        var fake = new FakeUps();
        await Client(fake).GetRatesAsync(Req(1m, Line(1.23m, qty: 2), Line(4m)), CancellationToken.None);

        var package = Assert.Single(Shipment(fake.RateCalls.Single().Body).GetProperty("Package").EnumerateArray());
        Assert.Equal("6.5", package.GetProperty("PackageWeight").GetProperty("Weight").GetString());
    }

    [Fact]
    public async Task UP_T5_handling_fee_is_added_to_each_option()
    {
        Seed(c => { c.AllowedServices = "03,02"; c.HandlingFee = 1.50m; });
        var options = await Client(new FakeUps()).GetRatesAsync(Req(5m), CancellationToken.None);
        Assert.Equal(new[] { 13.90m, 26.50m }, options.Select(o => o.Rate).ToArray());
    }

    [Fact]
    public async Task UP_T6_ffl_cart_gets_next_day_services_only_and_is_not_residential()
    {
        Seed();
        var fake = new FakeUps();
        var options = await Client(fake).GetRatesAsync(Req(0m, Line(2m, ffl: true)), CancellationToken.None);

        Assert.Equal(new[] { "13", "01" }, options.Select(o => o.MethodId).ToArray());
        var shipTo = Shipment(fake.RateCalls.Single().Body).GetProperty("ShipTo").GetProperty("Address");
        Assert.False(shipTo.TryGetProperty("ResidentialAddressIndicator", out _));
    }

    [Fact]
    public async Task Customer_cart_is_residential()
    {
        Seed();
        var fake = new FakeUps();
        await Client(fake).GetRatesAsync(Req(5m), CancellationToken.None);
        var shipTo = Shipment(fake.RateCalls.Single().Body).GetProperty("ShipTo").GetProperty("Address");
        Assert.True(shipTo.TryGetProperty("ResidentialAddressIndicator", out _));
    }

    [Fact]
    public async Task UP_T7_zero_weight_makes_no_call()
    {
        Seed();
        var fake = new FakeUps();
        Assert.Empty(await Client(fake).GetRatesAsync(Req(0m), CancellationToken.None));
        Assert.Empty(fake.RateCalls);
        Assert.Equal(0, fake.TokenCalls);
    }

    [Fact]
    public async Task UP_T8_wrong_secret_returns_nothing_without_throwing()
    {
        Seed();
        var fake = new FakeUps { TokenStatus = HttpStatusCode.Unauthorized };
        Assert.Empty(await Client(fake).GetRatesAsync(Req(5m), CancellationToken.None));
        Assert.Empty(fake.RateCalls);
    }

    [Fact]
    public async Task UP_T10_one_token_for_many_rate_calls()
    {
        Seed();
        var fake = new FakeUps();
        for (var i = 1; i <= 5; i++)
            // Different weights so the rate cache cannot hide the calls.
            await Client(fake).GetRatesAsync(Req(i), CancellationToken.None);
        Assert.Equal(1, fake.TokenCalls);
        Assert.Equal(5, fake.RateCalls.Count);
    }

    [Fact]
    public async Task Rates_are_cached_for_the_same_shipment()
    {
        Seed();
        var fake = new FakeUps();
        var cache = new MemoryCache(new MemoryCacheOptions());
        await Client(fake, cache).GetRatesAsync(Req(5m), CancellationToken.None);
        await Client(fake, cache).GetRatesAsync(Req(5m), CancellationToken.None);
        Assert.Single(fake.RateCalls);
    }

    [Fact]
    public async Task UP_T11_real_names_address_lines_once_and_request_headers()
    {
        Seed();
        var fake = new FakeUps();
        await Client(fake).GetRatesAsync(Req(5m), CancellationToken.None);

        var (request, body) = fake.RateCalls.Single();
        var shipment = Shipment(body);
        Assert.Equal("HK Parts", shipment.GetProperty("Shipper").GetProperty("Name").GetString());
        Assert.Equal("A1B2C3", shipment.GetProperty("Shipper").GetProperty("ShipperNumber").GetString());
        Assert.Equal("Jane Buyer", shipment.GetProperty("ShipTo").GetProperty("Name").GetString());
        Assert.Equal(
            new[] { "350 5th Ave", "Suite 100" },
            shipment.GetProperty("ShipTo").GetProperty("Address").GetProperty("AddressLine").EnumerateArray().Select(e => e.GetString()).ToArray());

        // UP-52 / UP-53.
        var charge = shipment.GetProperty("PaymentDetails").GetProperty("ShipmentCharge")[0];
        Assert.Equal("01", charge.GetProperty("Type").GetString());
        Assert.Equal("A1B2C3", charge.GetProperty("BillShipper").GetProperty("AccountNumber").GetString());
        Assert.Equal("01", body.GetProperty("RateRequest").GetProperty("PickupType").GetProperty("Code").GetString());

        // UP-50.
        Assert.True(request.Headers.Contains("transId"));
        Assert.Equal("HKParts", request.Headers.GetValues("transactionSrc").Single());
    }

    [Fact]
    public async Task UP_54_a_single_rated_shipment_object_is_handled()
    {
        Seed();
        var options = await Client(new FakeUps { SingleObject = true }).GetRatesAsync(Req(5m), CancellationToken.None);
        Assert.Equal("03", Assert.Single(options).MethodId);
    }

    [Fact]
    public async Task UP_40_test_reports_token_expiry_and_quotes_or_the_ups_error()
    {
        var credential = Seed();
        var ok = await Client(new FakeUps()).TestCredentialAsync(credential.Id, CancellationToken.None);
        Assert.True(ok.TokenOk);
        Assert.NotNull(ok.TokenValidUntil);
        Assert.Equal(4, ok.Domestic.Count(l => l.Price.HasValue));

        var bad = await Client(new FakeUps { TokenStatus = HttpStatusCode.Unauthorized })
            .TestCredentialAsync(credential.Id, CancellationToken.None);
        Assert.False(bad.TokenOk);
        Assert.Contains("250002", bad.TokenError);
    }
}
