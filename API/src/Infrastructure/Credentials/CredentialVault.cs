using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Credentials;

/// <summary>
/// Runtime credential resolution over the per-integration <c>Credentials_*</c> tables, plus the generic
/// Encrypt/Decrypt used by the few secrets stored elsewhere (SMTP, reCAPTCHA). Integration adapters keep
/// calling <see cref="GetCredentialAsync"/> with their service-type key; this reads the active row of the
/// matching table and rebuilds the exact string/JSON that adapter parses. Secret columns are decrypted
/// transparently by the EF value converter, so this only assembles the resolved shape.
/// </summary>
public class CredentialVault : ICredentialVault
{
    private readonly ICredentialProtector _protector;
    private readonly IApplicationDbContext _db;

    public CredentialVault(ICredentialProtector protector, IApplicationDbContext db)
    {
        _protector = protector;
        _db = db;
    }

    public string Encrypt(string plaintext) => _protector.Protect(plaintext);

    public string Decrypt(string ciphertext) => _protector.Unprotect(ciphertext);

    public async Task<string?> GetCredentialAsync(string serviceType, CancellationToken cancellationToken = default)
        => (await GetResolvedCredentialAsync(serviceType, cancellationToken))?.Value;

    public async Task<ResolvedCredential?> GetResolvedCredentialAsync(string serviceType, CancellationToken cancellationToken = default)
    {
        return serviceType switch
        {
            // Payment gateways. Stripe passes no baseUrl: the Stripe.net SDK owns the host, and the key
            // prefix (sk_test_/sk_live_) decides the mode — there is no endpoint for an admin to choose.
            "stripe"       => Resolve(await ActiveAsync(_db.StripeCredentials, cancellationToken), c => Json(new { secretKey = c.SecretKey, returnUrl = c.ReturnUrl })),
            "paypal"       => Resolve(await ActiveAsync(_db.PayPalCredentials, cancellationToken), c => Pair(c.ClientId, c.SecretKey), c => c.BaseUrl, c => c.ReturnUrl),
            "razorpay"     => Resolve(await ActiveAsync(_db.RazorpayCredentials, cancellationToken), c => Pair(c.KeyId, c.KeySecret), c => c.BaseUrl),
            "square"       => Resolve(await ActiveAsync(_db.SquareCredentials, cancellationToken), SquareValue, c => c.BaseUrl),
            "authorizenet" => Resolve(await ActiveAsync(_db.AuthorizeNetCredentials, cancellationToken), c => Pair(c.ApplicationLoginId, c.TransactionKey), c => c.BaseUrl),
            // Tax providers
            "taxjar"       => Resolve(await ActiveAsync(_db.TaxJarCredentials, cancellationToken), c => c.SecretKey, c => c.BaseUrl),
            "stripe-tax"   => Resolve(await ActiveAsync(_db.StripeTaxCredentials, cancellationToken), c => c.SecretKey, c => c.BaseUrl),
            // Storage
            "azure-blob"   => Resolve(await ActiveAsync(_db.AzureBlobCredentials, cancellationToken), c => c.ConnectionString),
            // Shipping carriers (adapters parse a small JSON document). baseUrl is not optional decoration:
            // it is the only thing telling the adapter whether this account lives on the sandbox or the live
            // host, and the adapters hold no fallback of their own.
            "fedex"        => Resolve(await ActiveAsync(_db.FedExCredentials, cancellationToken), c => Json(new { baseUrl = c.BaseUrl, apiKey = c.ApiKey, secretKey = c.ApiSecret, accountNumber = c.AccountNumber })),
            "dhl"          => Resolve(await ActiveAsync(_db.DhlExpressCredentials, cancellationToken), c => Json(new { baseUrl = c.BaseUrl, apiKey = c.ApiKey, apiSecret = c.ApiSecret, accountNumber = c.AccountNumber })),
            "usps"         => Resolve(await ActiveAsync(_db.UspsCredentials, cancellationToken), c => Json(new { baseUrl = c.BaseUrl, consumerKey = c.ConsumerKey, consumerSecret = c.ConsumerSecret })),
            "ups"          => Resolve(await ActiveAsync(_db.UpsCredentials, cancellationToken), c => Json(new { baseUrl = c.BaseUrl, clientId = c.ClientId, clientSecret = c.ClientSecret, merchantId = c.MerchantId })),
            _              => null,
        };
    }

    /// <summary>The active (non-deleted) row for an integration, most-recently-updated first.</summary>
    private static Task<T?> ActiveAsync<T>(DbSet<T> set, CancellationToken ct) where T : IntegrationCredentialBase
        => set.AsNoTracking()
            .Where(x => x.Active)
            .OrderByDescending(x => x.UpdatedOnUtc)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Projects the active row's credential string and pairs it with its production flag and endpoint; null
    /// when unusable. <paramref name="baseUrl"/> is omitted only for providers with no host to choose.
    /// </summary>
    private static ResolvedCredential? Resolve<T>(
        T? credential, Func<T, string?> project, Func<T, string?>? baseUrl = null,
        Func<T, string?>? returnUrl = null) where T : IntegrationCredentialBase
    {
        if (credential is null) return null;
        var value = project(credential);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : new ResolvedCredential(
                value, credential.IsProduction, baseUrl?.Invoke(credential), credential.TransactionFeePercent,
                returnUrl?.Invoke(credential));
    }

    /// <summary>Colon-joined "id:secret" pair; null unless both halves are present.</summary>
    private static string? Pair(string? left, string? right)
        => string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right) ? null : $"{left}:{right}";

    /// <summary>
    /// Square's runtime value: a small JSON envelope of the access token plus the (optional) Location id, so
    /// the adapter can both authenticate and stamp the charge with the location. Null — so the gateway is not
    /// offered — unless the access token is present, matching the "configured means usable" rule of the others.
    /// </summary>
    private static string? SquareValue(SquareCredential c)
        => string.IsNullOrWhiteSpace(c.AccessToken)
            ? null
            : Json(new { accessToken = c.AccessToken, locationId = c.LocationId });

    /// <summary>Serializes the projected shape (using its runtime type so its members are emitted).</summary>
    private static string Json(object shape) => JsonSerializer.Serialize(shape, shape.GetType());
}
