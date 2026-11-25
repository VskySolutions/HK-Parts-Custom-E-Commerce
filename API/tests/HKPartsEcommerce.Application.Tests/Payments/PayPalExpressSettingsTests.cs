using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Infrastructure.Credentials;
using HKPartsEcommerce.Infrastructure.Payments.Adapters;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Payments;

/// <summary>
/// PHP parity, "PayPal Express": the classic API username / password / signature work without REST keys, the
/// password and signature are write-only, and a classic-only account counts as configured (so checkout offers it,
/// on the sandbox endpoints in Test Mode).
/// </summary>
public class PayPalExpressSettingsTests : CatalogTestBase
{
    private sealed class PlainProtector : ICredentialProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string ciphertext) => ciphertext;
        public string? TryUnprotect(string ciphertext) => ciphertext;
    }

    private static SavePayPalCredentialCommand Classic(Guid? id = null, string? password = "pw123", string? signature = "sig-abc") => new(
        id, "PayPal Express", true, false, null, null, null, "https://shop.test/shop/checkout",
        ApiUsername: "seller_api1.example.com", ApiPassword: password, ApiSignature: signature,
        BrandName: "HK Parts", MinOrderAmount: 5m, MaxOrderAmount: 1000m, DefaultOrderStatus: "Processing", SortOrder: 2);

    [Fact]
    public async Task Classic_credentials_alone_save_and_secrets_stay_write_only()
    {
        Guid id;
        await using (var db = NewContext())
        {
            var dto = await new SavePayPalCredentialCommandHandler(db).Handle(Classic(), default);
            id = dto.Id;
            Assert.Equal("seller_api1.example.com", dto.ApiUsername);
            Assert.Equal("••••w123", dto.ApiPasswordMasked);
            Assert.Equal("HK Parts", dto.BrandName);
            Assert.Equal("Processing", dto.DefaultOrderStatus);
            Assert.Equal(2, dto.SortOrder);
        }

        // Saving again with the password and signature left blank keeps the stored ones.
        await using (var db = NewContext())
            await new SavePayPalCredentialCommandHandler(db).Handle(Classic(id, password: null, signature: null), default);
        await using (var db = NewContext())
        {
            var row = db.PayPalCredentials.Single(c => c.Id == id);
            Assert.Equal("pw123", row.ApiPassword);
            Assert.Equal("sig-abc", row.ApiSignature);

            // Test Mode (Sandbox): the classic-only account resolves as configured, on the sandbox endpoint.
            var resolved = await new CredentialVault(new PlainProtector(), db).GetResolvedCredentialAsync("paypal", default);
            Assert.NotNull(resolved);
            Assert.Equal(CredentialVault.PayPalClassic, resolved!.Value);
            Assert.Equal("https://api-m.sandbox.paypal.com", resolved.BaseUrl);
        }
    }

    [Fact]
    public async Task Neither_rest_nor_classic_credentials_is_refused()
    {
        await using var db = NewContext();
        await Assert.ThrowsAsync<ValidationException>(() => new SavePayPalCredentialCommandHandler(db).Handle(
            new SavePayPalCredentialCommand(null, "Empty", true, false, null, null, null, "https://shop.test/x",
                ApiUsername: "only-a-username"), default));
    }

    [Fact]
    public void Classic_api_responses_are_parsed()
    {
        var r = PaypalClassicExpress.Parse("TOKEN=EC%2d1AB23456CD789012E&TIMESTAMP=2026%2d11%2d20T10%3a00%3a00Z&ACK=Success&VERSION=204%2e0");
        Assert.Equal("EC-1AB23456CD789012E", r["TOKEN"]);
        Assert.Equal("Success", r["ACK"]);

        var failed = PaypalClassicExpress.Parse("ACK=Failure&L_SHORTMESSAGE0=Security+error&L_LONGMESSAGE0=Security+header+is+not+valid");
        Assert.Equal("Security header is not valid", failed["L_LONGMESSAGE0"]);
    }
}
