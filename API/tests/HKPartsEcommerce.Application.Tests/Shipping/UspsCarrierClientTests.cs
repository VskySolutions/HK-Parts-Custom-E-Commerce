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
/// USPS REST rating (docs/usps-shipping-requirements.md). Case ids (US-T…) are the spec's §6 acceptance
/// tests; USPS itself is replaced by a recording fake handler.
/// </summary>
public class UspsCarrierClientTests : CatalogTestBase
{
    private const decimal KgPerPound = 0.45359237m;
    private static readonly CarrierAddress Origin = new("US", "NC", "28270", null, null);
    private static readonly CarrierAddress NewYork = new("US", "NY", "10001", null, null);
    private static readonly CarrierAddress Canada = new("CA", "ON", "K1A 0B1", null, null);

    private sealed class FakeUsps : HttpMessageHandler
    {
        public readonly List<(string Path, JsonElement? Body)> Calls = new();
        public int TokenCalls;
        public Func<string, HttpStatusCode>? StatusFor;
        public Dictionary<string, decimal> Prices { get; } = new()
        {
            ["USPS_GROUND_ADVANTAGE"] = 8.10m,
            ["PRIORITY_MAIL"] = 10.40m,
            ["PRIORITY_MAIL_EXPRESS"] = 31.00m,
            ["PRIORITY_MAIL_INTERNATIONAL"] = 38.40m,
            ["PRIORITY_MAIL_EXPRESS_INTERNATIONAL"] = 61.20m,
            ["FIRST-CLASS_PACKAGE_INTERNATIONAL_SERVICE"] = 21.75m,
        };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/oauth2/v3/token"))
            {
                Interlocked.Increment(ref TokenCalls);
                return Json(HttpStatusCode.OK, "{\"access_token\":\"tok\",\"expires_in\":28800}");
            }

            var text = await request.Content!.ReadAsStringAsync(ct);
            var body = JsonDocument.Parse(text).RootElement.Clone();
            lock (Calls)
                Calls.Add((path, body));

            var mailClass = body.GetProperty("mailClass").GetString()!;
            var status = StatusFor?.Invoke(mailClass) ?? HttpStatusCode.OK;
            if (status != HttpStatusCode.OK)
                return Json(status, "{\"error\":{\"message\":\"nope\"}}");

            // Shape of a v3 base-rates search: one total, components under rates[].
            var price = Prices[mailClass];
            return Json(HttpStatusCode.OK,
                $"{{\"totalBasePrice\":{price},\"rates\":[{{\"price\":{price - 1m},\"mailClass\":\"{mailClass}\"}},{{\"price\":1.00}}]}}");
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
    private UspsCredential Seed(Action<UspsCredential>? configure = null)
    {
        var credential = new UspsCredential
        {
            Name = "Test",
            Active = true,
            BaseUrl = $"https://{Guid.NewGuid():N}.usps.test",
            ConsumerKey = "key",
            ConsumerSecret = "secret",
            AllowedDomesticServices = "USPS_GROUND_ADVANTAGE,PRIORITY_MAIL,PRIORITY_MAIL_EXPRESS",
            AllowedInternationalServices = "PRIORITY_MAIL_INTERNATIONAL,PRIORITY_MAIL_EXPRESS_INTERNATIONAL,FIRST-CLASS_PACKAGE_INTERNATIONAL_SERVICE",
        };
        configure?.Invoke(credential);
        using var db = NewContext();
        db.UspsCredentials.Add(credential);
        db.SaveChanges();
        return credential;
    }

    private UspsCarrierClient Client(FakeUsps fake, IMemoryCache? cache = null)
        => new(new Factory(fake), NewContext(), cache ?? new MemoryCache(new MemoryCacheOptions()),
            NullLogger<UspsCarrierClient>.Instance);

    private static CarrierRateRequest Req(CarrierAddress dest, decimal pounds, decimal subtotal = 100m)
        => new(Origin, dest, pounds * KgPerPound, null, null, null, subtotal);

    [Fact]
    public async Task US_T1_lists_each_allowed_domestic_service_named_after_the_method()
    {
        Seed();
        var fake = new FakeUsps();
        var options = await Client(fake).GetRatesAsync(Req(NewYork, 2m), CancellationToken.None);

        Assert.Equal(
            new[] { "USPS - Ground Advantage", "USPS - Priority Mail", "USPS - Priority Mail Express" },
            options.Select(o => o.Name).ToArray());
        // One price per class — the total, not one option per rates[] component.
        Assert.Equal(new[] { 8.10m, 10.40m, 31.00m }, options.Select(o => o.Rate).ToArray());
    }

    [Fact]
    public async Task Only_allowed_services_are_requested()
    {
        Seed(c => c.AllowedDomesticServices = "PRIORITY_MAIL");
        var fake = new FakeUsps();
        var options = await Client(fake).GetRatesAsync(Req(NewYork, 2m), CancellationToken.None);

        Assert.Equal("PRIORITY_MAIL", Assert.Single(options).MethodId);
        Assert.Single(fake.Calls);
    }

    [Fact]
    public async Task US_T3_handling_fee_is_added_to_each_option()
    {
        Seed(c => c.HandlingFee = 1.00m);
        var options = await Client(new FakeUsps()).GetRatesAsync(Req(NewYork, 2m), CancellationToken.None);
        Assert.Equal(new[] { 9.10m, 11.40m, 32.00m }, options.Select(o => o.Rate).ToArray());
    }

    [Fact]
    public async Task US_T4_weight_is_sent_in_decimal_pounds()
    {
        Seed(c => c.AllowedDomesticServices = "PRIORITY_MAIL");
        var fake = new FakeUsps();
        await Client(fake).GetRatesAsync(Req(NewYork, 1.4m), CancellationToken.None);

        var body = fake.Calls.Single().Body!.Value;
        Assert.Equal(1.4m, body.GetProperty("weight").GetDecimal());
        Assert.Equal("28270", body.GetProperty("originZIPCode").GetString());
        Assert.Equal("10001", body.GetProperty("destinationZIPCode").GetString());
        Assert.Equal("RETAIL", body.GetProperty("priceType").GetString());
    }

    [Fact]
    public async Task US_T5_zero_weight_makes_no_call()
    {
        Seed();
        var fake = new FakeUsps();
        var options = await Client(fake).GetRatesAsync(Req(NewYork, 0m), CancellationToken.None);
        Assert.Empty(options);
        Assert.Empty(fake.Calls);
        Assert.Equal(0, fake.TokenCalls);
    }

    [Fact]
    public async Task Minimum_order_and_ship_to_countries_gate_the_usps_method()
    {
        Seed(c => { c.MinimumOrderAmount = 50m; c.ShipToCountries = "US"; });
        var client = Client(new FakeUsps());
        Assert.Empty(await client.GetRatesAsync(Req(NewYork, 2m, subtotal: 20m), CancellationToken.None));
        Assert.Empty(await client.GetRatesAsync(Req(Canada, 2m), CancellationToken.None));
        Assert.NotEmpty(await client.GetRatesAsync(Req(NewYork, 2m), CancellationToken.None));
    }

    [Fact]
    public async Task Custom_method_international_skips_usps_method_filters_and_fee()
    {
        // The USPS method is US-only with a fee; the Custom method borrowing its prices ignores both.
        Seed(c => { c.ShipToCountries = "US"; c.HandlingFee = 5m; c.DefaultLengthIn = 10m; });
        var fake = new FakeUsps();
        var options = await Client(fake).GetInternationalRatesAsync(Req(Canada, 2m), CancellationToken.None);

        Assert.Equal(21.75m, options.Min(o => o.Rate));
        var body = fake.Calls.First().Body!.Value;
        Assert.Equal("CA", body.GetProperty("destinationCountryCode").GetString());
        Assert.Equal(10m, body.GetProperty("length").GetDecimal());
        Assert.Equal(15m, body.GetProperty("width").GetDecimal());
    }

    [Fact]
    public async Task A_declined_class_is_skipped_and_a_5xx_is_retried_once()
    {
        Seed();
        var fake = new FakeUsps
        {
            StatusFor = mc => mc switch
            {
                "PRIORITY_MAIL_EXPRESS" => HttpStatusCode.BadRequest,
                "PRIORITY_MAIL" => HttpStatusCode.ServiceUnavailable,
                _ => HttpStatusCode.OK,
            },
        };
        var options = await Client(fake).GetRatesAsync(Req(NewYork, 2m), CancellationToken.None);

        Assert.Equal("USPS_GROUND_ADVANTAGE", Assert.Single(options).MethodId);
        Assert.Equal(2, fake.Calls.Count(c => c.Body!.Value.GetProperty("mailClass").GetString() == "PRIORITY_MAIL"));
        Assert.Equal(1, fake.Calls.Count(c => c.Body!.Value.GetProperty("mailClass").GetString() == "PRIORITY_MAIL_EXPRESS"));
    }

    [Fact]
    public async Task Token_and_rates_are_cached()
    {
        Seed();
        var fake = new FakeUsps();
        var cache = new MemoryCache(new MemoryCacheOptions());
        await Client(fake, cache).GetRatesAsync(Req(NewYork, 2m), CancellationToken.None);
        await Client(fake, cache).GetRatesAsync(Req(NewYork, 2m), CancellationToken.None);

        Assert.Equal(1, fake.TokenCalls);
        Assert.Equal(3, fake.Calls.Count);
    }

    [Fact]
    public async Task US_40_test_reports_quotes_and_per_class_errors()
    {
        var credential = Seed(c => c.AllowedInternationalServices = "PRIORITY_MAIL_INTERNATIONAL");
        var fake = new FakeUsps { StatusFor = mc => mc == "PRIORITY_MAIL_INTERNATIONAL" ? HttpStatusCode.BadRequest : HttpStatusCode.OK };
        var result = await Client(fake).TestCredentialAsync(credential.Id, CancellationToken.None);

        Assert.True(result.TokenOk);
        Assert.Equal(3, result.Domestic.Count(l => l.Price.HasValue));
        var line = Assert.Single(result.International);
        Assert.Null(line.Price);
        Assert.Contains("nope", line.Error);
    }

    [Fact]
    public async Task No_active_credential_quotes_nothing()
    {
        Seed(c => c.Active = false);
        Assert.Empty(await Client(new FakeUsps()).GetRatesAsync(Req(NewYork, 2m), CancellationToken.None));
    }
}
