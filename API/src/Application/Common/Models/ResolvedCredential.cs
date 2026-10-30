namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>
/// An active integration credential resolved for runtime use: its raw string/JSON value (the same value
/// <see cref="Interfaces.ICredentialVault.GetCredentialAsync"/> returns), whether the row is a production
/// (live) credential, and the API host it belongs to.
/// </summary>
/// <param name="BaseUrl">
/// The endpoint this credential authenticates against, straight from its admin-configured column. Adapters
/// hold no fallback: sandbox and live are different hosts for most providers, and a constant compiled into
/// the adapter cannot know which account an admin pasted in — guessing sends sandbox keys to the live host
/// and fails with an opaque 4xx. Null only for providers with genuinely no endpoint to choose (Stripe, whose
/// SDK owns the host and whose key prefix decides the mode).
///
/// The shipping carriers also see this inside their JSON <see cref="Value"/>, which already models it as
/// part of each adapter's credential contract; this envelope is how the providers whose value is a bare
/// secret (PayPal, Square, …) receive it.
/// </param>
/// <param name="TransactionFeePercent">
/// The optional transaction/processing fee (% of order total) the admin configured for this integration;
/// null when unset. Only payment gateways act on it — the checkout adds the active gateway's fee to the
/// order total as an additional charge.
/// </param>
/// <param name="ReturnUrl">
/// The storefront URL a redirect-based gateway sends the buyer back to after approving/cancelling payment
/// (e.g. PayPal). Null for gateways that carry it elsewhere (Stripe encodes it inside <see cref="Value"/>)
/// or have no redirect flow.
/// </param>
public sealed record ResolvedCredential(
    string Value, bool IsProduction, string? BaseUrl = null, decimal? TransactionFeePercent = null, string? ReturnUrl = null);
