using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Application.Tests.GiftCertificates;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Credentials;
using HKPartsEcommerce.Infrastructure.Payments.Adapters;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Payments;

/// <summary>
/// PHP parity, the Square method settings: the admin save keeps every field (refresh token write-only), "Mode" picks
/// the endpoint, the OAuth access token is renewed before it expires (as the old Square class did), "View Locations"
/// lists the account's locations, and an authorize-only ("Delayed") payment is voided rather than refunded.
/// </summary>
public class SquareMethodSettingsTests : GiftCertificateTestBase
{
    private sealed class PlainProtector : ICredentialProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
        public string? TryUnprotect(string ciphertext) => ciphertext;
    }

    /// <summary>A fake Square: answers by path and records every call.</summary>
    private sealed class FakeSquare : HttpMessageHandler, IHttpClientFactory
    {
        public List<(string Method, string Url, string? Body)> Calls { get; } = new();

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            var url = request.RequestUri!.ToString();
            Calls.Add((request.Method.Method, url, body));
            var json = url switch
            {
                _ when url.EndsWith("/oauth2/token") => "{\"access_token\":\"EAAA-new\",\"expires_at\":\"2027-01-20T10:00:00Z\",\"refresh_token\":\"r-2\"}",
                _ when url.EndsWith("/v2/locations") => "{\"locations\":[{\"id\":\"LMAIN\",\"name\":\"HK Parts Main\",\"status\":\"ACTIVE\",\"currency\":\"USD\",\"address\":{\"address_line_1\":\"1 Main St\",\"locality\":\"Austin\",\"administrative_district_level_1\":\"TX\",\"postal_code\":\"78701\"}}]}",
                _ when url.Contains("/cancel") => "{\"payment\":{\"id\":\"p1\",\"status\":\"CANCELED\"}}",
                _ => "{}",
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private Guid SeedSquare(Action<SquareCredential> configure)
    {
        using var db = NewContext();
        var row = new SquareCredential
        {
            Name = "Square", Active = true, IsProduction = false, AccessToken = "EAAA-old",
            ApplicationId = "sandbox-sq0idb-app", ApplicationSecret = "sandbox-sq0csb-secret", LocationId = "LMAIN",
        };
        configure(row);
        db.SquareCredentials.Add(row);
        db.SaveChanges();
        return row.Id;
    }

    private SquareGatewayAdapter Adapter(Infrastructure.Persistence.AppDbContext db, FakeSquare http) =>
        new(new CredentialVault(new PlainProtector(), db), http, NullLogger<SquareGatewayAdapter>.Instance, db, Clock);

    [Fact]
    public async Task Admin_save_keeps_every_square_setting_and_the_refresh_token_is_write_only()
    {
        var expires = new DateTime(2026, 12, 20, 10, 0, 0, DateTimeKind.Utc);
        Guid id;
        await using (var db = NewContext())
        {
            var dto = await new SaveSquareCredentialCommandHandler(db).Handle(new SaveSquareCredentialCommand(
                null, "Square", true, false, null, "sandbox-sq0idb-app", "LMAIN", "EAAA-old", "secret",
                TransactionType: "AuthOnly", RefreshToken: "r-1", AccessTokenExpiresAtUtc: expires, DisplayName: "Credit Card",
                MinOrderAmount: 1m, MaxOrderAmount: 999m, DefaultOrderStatus: "Processing", SortOrder: -1), default);
            id = dto.Id;
            Assert.Equal("AuthOnly", dto.TransactionType);
            Assert.Equal("••••", dto.RefreshTokenMasked); // too short to show its last four
            Assert.Equal(expires, dto.AccessTokenExpiresAtUtc);
            Assert.Equal("Credit Card", dto.DisplayName);
            Assert.Equal(-1, dto.SortOrder);
        }

        // Re-saved with the refresh token left blank: the stored one stays.
        await using (var db = NewContext())
            await new SaveSquareCredentialCommandHandler(db).Handle(new SaveSquareCredentialCommand(
                id, "Square", true, false, null, "sandbox-sq0idb-app", "LMAIN", "EAAA-old", "secret", RefreshToken: null), default);
        await using (var db = NewContext())
        {
            Assert.Equal("r-1", db.SquareCredentials.Single(c => c.Id == id).RefreshToken);
            // Mode = Sandbox, no Base URL: the sandbox endpoint.
            var resolved = await new CredentialVault(new PlainProtector(), db).GetResolvedCredentialAsync("square", default);
            Assert.Equal("https://connect.squareupsandbox.com/v2", resolved!.BaseUrl);
        }
    }

    [Fact]
    public async Task Access_token_is_renewed_before_it_expires_and_locations_are_listed()
    {
        var id = SeedSquare(s => { s.RefreshToken = "r-1"; s.AccessTokenExpiresAtUtc = Now.AddDays(2); });
        var http = new FakeSquare();

        GatewayTestResult result;
        await using (var db = NewContext())
            result = await Adapter(db, http).ListLocationsAsync(id, default);

        Assert.True(result.Connected);
        Assert.Equal("Sandbox", result.Environment);
        var location = Assert.Single(result.Locations!);
        Assert.Equal("LMAIN", location.Id);
        Assert.Equal("1 Main St, Austin, TX, 78701", location.Address);

        // The refresh went to Square's OAuth host with the app's id + secret, and the new token was used and saved.
        var refresh = http.Calls.First(c => c.Url.EndsWith("/oauth2/token"));
        Assert.Equal("https://connect.squareupsandbox.com/oauth2/token", refresh.Url);
        Assert.Contains("\"grant_type\":\"refresh_token\"", refresh.Body);
        Assert.Contains("\"client_secret\":\"sandbox-sq0csb-secret\"", refresh.Body);
        await using (var db = NewContext())
        {
            var row = db.SquareCredentials.Single(c => c.Id == id);
            Assert.Equal("EAAA-new", row.AccessToken);
            Assert.Equal("r-2", row.RefreshToken);
            Assert.Equal(new DateTime(2027, 1, 20, 10, 0, 0, DateTimeKind.Utc), row.AccessTokenExpiresAtUtc);
        }
    }

    [Fact]
    public async Task Token_far_from_expiry_is_not_renewed()
    {
        var id = SeedSquare(s => { s.RefreshToken = "r-1"; s.AccessTokenExpiresAtUtc = Now.AddDays(25); });
        var http = new FakeSquare();
        await using (var db = NewContext())
            await Adapter(db, http).ListLocationsAsync(id, default);
        Assert.DoesNotContain(http.Calls, c => c.Url.EndsWith("/oauth2/token"));
    }

    [Fact]
    public async Task Delayed_payment_is_voided_not_refunded()
    {
        SeedSquare(_ => { });
        var http = new FakeSquare();
        await using var db = NewContext();
        var result = await Adapter(db, http).RefundAsync(
            new PaymentRecord { Method = PaymentMethodType.Square, Status = PaymentStatus.Authorized, AuthorizationId = "p1", Amount = 20m, CurrencyCode = "USD" },
            20m, default);

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Voided, result.Status);
        Assert.Contains(http.Calls, c => c.Url.EndsWith("/payments/p1/cancel"));
        Assert.DoesNotContain(http.Calls, c => c.Url.EndsWith("/refunds"));
    }

    [Theory]
    [InlineData("https://connect.squareup.com/v2", "https://connect.squareup.com")]
    [InlineData("https://connect.squareupsandbox.com/v2/", "https://connect.squareupsandbox.com")]
    public void OAuth_host_is_the_payments_host_without_v2(string baseUrl, string expected)
        => Assert.Equal(expected, SquareGatewayAdapter.SquareOAuthBase(baseUrl));
}
