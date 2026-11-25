using System.Globalization;
using System.Net.Http;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Payments.Adapters;

/// <summary>
/// PHP parity: PayPal Express Checkout over PayPal's classic NVP API, signed with the API username, password and
/// signature (what the legacy site's "PayPal Express" method used). The same redirect flow as the REST adapter:
/// <c>SetExpressCheckout</c> → the buyer approves on PayPal → on return <c>GetExpressCheckoutDetails</c> +
/// <c>DoExpressCheckoutPayment</c>. "Sale" captures at once; "Authorization" holds the funds for <c>DoCapture</c>.
/// The gateway reference is <c>nvp-sale:&lt;token&gt;</c> / <c>nvp-auth:&lt;token&gt;</c>, so the return step uses the
/// same payment action and later calls know the payment is classic.
/// </summary>
public sealed class PaypalClassicExpress
{
    public const string SalePrefix = "nvp-sale:";
    public const string AuthPrefix = "nvp-auth:";
    private const string Version = "204.0";

    private readonly HttpClient _http;
    private readonly PayPalCredential _cred;

    public PaypalClassicExpress(HttpClient http, PayPalCredential cred)
    {
        _http = http;
        _cred = cred;
    }

    public static bool IsClassic(PaymentRecord payment) =>
        payment.GatewayReference is { } r && (r.StartsWith(SalePrefix, StringComparison.Ordinal) || r.StartsWith(AuthPrefix, StringComparison.Ordinal));

    /// <summary>"Test Mode" = the credential's Sandbox environment.</summary>
    private string Endpoint => _cred.IsProduction ? "https://api-3t.paypal.com/nvp" : "https://api-3t.sandbox.paypal.com/nvp";
    private string WebHost => _cred.IsProduction ? "https://www.paypal.com" : "https://www.sandbox.paypal.com";

    public async Task<PaymentResult> SetExpressCheckoutAsync(PaymentRequest req, bool authorizeOnly, string returnUrl, CancellationToken ct)
    {
        var sep = returnUrl.Contains('?') ? '&' : '?';
        var fields = new Dictionary<string, string>
        {
            ["METHOD"] = "SetExpressCheckout",
            ["RETURNURL"] = $"{returnUrl}{sep}order={req.OrderId}",
            ["CANCELURL"] = $"{returnUrl}{sep}order={req.OrderId}&cancelled=1",
            ["PAYMENTREQUEST_0_AMT"] = Money(req.Amount),
            ["PAYMENTREQUEST_0_CURRENCYCODE"] = req.CurrencyCode.ToUpperInvariant(),
            ["PAYMENTREQUEST_0_PAYMENTACTION"] = authorizeOnly ? "Authorization" : "Sale",
            ["NOSHIPPING"] = "1",
        };
        if (!string.IsNullOrWhiteSpace(req.OrderNumber))
        {
            fields["PAYMENTREQUEST_0_INVNUM"] = req.OrderNumber!;
            fields["PAYMENTREQUEST_0_DESC"] = $"Order {req.OrderNumber}";
        }
        if (!string.IsNullOrWhiteSpace(_cred.BrandName))
            fields["BRANDNAME"] = _cred.BrandName!.Trim(); // "Store Name Shown in PayPal"
        if (!string.IsNullOrWhiteSpace(req.Customer?.Email))
            fields["EMAIL"] = req.Customer!.Email!.Trim();

        var r = await CallAsync(fields, ct);
        if (!Ok(r) || !r.TryGetValue("TOKEN", out var token) || string.IsNullOrWhiteSpace(token))
            return PaymentResult.Failed($"PayPal: {Error(r)}");

        var url = $"{WebHost}/cgi-bin/webscr?cmd=_express-checkout&useraction=commit&token={Uri.EscapeDataString(token)}";
        return PaymentResult.Redirect(url, (authorizeOnly ? AuthPrefix : SalePrefix) + token);
    }

    /// <summary>On the buyer's return: complete the payment PayPal holds for the token (idempotent on a refresh).</summary>
    public async Task<PaymentResult> CompleteAsync(PaymentRecord payment, CancellationToken ct)
    {
        var reference = payment.GatewayReference!;
        var authorizeOnly = reference.StartsWith(AuthPrefix, StringComparison.Ordinal);
        var token = reference[(authorizeOnly ? AuthPrefix : SalePrefix).Length..];

        var details = await CallAsync(new Dictionary<string, string> { ["METHOD"] = "GetExpressCheckoutDetails", ["TOKEN"] = token }, ct);
        if (!Ok(details))
            return PaymentResult.Failed($"PayPal: {Error(details)}", PaymentStatus.Pending);

        // Refreshed return page: the payment was already completed for this token.
        if (details.TryGetValue("CHECKOUTSTATUS", out var status) && status == "PaymentActionCompleted"
            && details.TryGetValue("PAYMENTREQUEST_0_TRANSACTIONID", out var doneId) && !string.IsNullOrWhiteSpace(doneId))
            return authorizeOnly
                ? PaymentResult.Authorized(doneId, reference)
                : PaymentResult.Captured(doneId, payment.Amount, doneId, reference);

        if (!details.TryGetValue("PAYERID", out var payerId) || string.IsNullOrWhiteSpace(payerId))
            return PaymentResult.Failed("PayPal payment was not approved.", PaymentStatus.Pending);

        var r = await CallAsync(new Dictionary<string, string>
        {
            ["METHOD"] = "DoExpressCheckoutPayment",
            ["TOKEN"] = token,
            ["PAYERID"] = payerId,
            ["PAYMENTREQUEST_0_AMT"] = Money(payment.Amount),
            ["PAYMENTREQUEST_0_CURRENCYCODE"] = payment.CurrencyCode.ToUpperInvariant(),
            ["PAYMENTREQUEST_0_PAYMENTACTION"] = authorizeOnly ? "Authorization" : "Sale",
        }, ct);
        if (!Ok(r) || !r.TryGetValue("PAYMENTINFO_0_TRANSACTIONID", out var txnId) || string.IsNullOrWhiteSpace(txnId))
            return PaymentResult.Failed($"PayPal: {Error(r)}", PaymentStatus.Pending);

        r.TryGetValue("PAYMENTINFO_0_PAYMENTSTATUS", out var payStatus);
        r.TryGetValue("PAYMENTINFO_0_PENDINGREASON", out var pendingReason);
        if (authorizeOnly || string.Equals(pendingReason, "authorization", StringComparison.OrdinalIgnoreCase))
            return PaymentResult.Authorized(txnId, reference);
        if (string.Equals(payStatus, "Completed", StringComparison.OrdinalIgnoreCase))
            return PaymentResult.Captured(txnId, payment.Amount, txnId, reference);
        // e.g. an eCheck still clearing: paid on PayPal's side but not settled yet; the order waits for it.
        return PaymentResult.Captured(txnId, payment.Amount, txnId, reference) with
        {
            Details = new PaymentGatewayDetails { ResponseText = $"PayPal payment status {payStatus} ({pendingReason})." },
        };
    }

    public async Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
    {
        var authorizationId = payment.AuthorizationId ?? payment.TransactionId;
        if (string.IsNullOrWhiteSpace(authorizationId))
            return PaymentResult.Failed("PayPal capture requires the authorization id.");
        var r = await CallAsync(new Dictionary<string, string>
        {
            ["METHOD"] = "DoCapture",
            ["AUTHORIZATIONID"] = authorizationId,
            ["AMT"] = Money(amount),
            ["CURRENCYCODE"] = payment.CurrencyCode.ToUpperInvariant(),
            ["COMPLETETYPE"] = "Complete",
        }, ct);
        return Ok(r) && r.TryGetValue("TRANSACTIONID", out var txnId)
            ? PaymentResult.Captured(txnId, amount, authorizationId, payment.GatewayReference)
            : PaymentResult.Failed($"PayPal: {Error(r)}", PaymentStatus.Authorized);
    }

    /// <summary>A captured payment is refunded; one still only authorized is voided.</summary>
    public async Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
    {
        if (payment.Status == PaymentStatus.Authorized)
        {
            var authorizationId = payment.AuthorizationId ?? payment.TransactionId;
            var v = await CallAsync(new Dictionary<string, string> { ["METHOD"] = "DoVoid", ["AUTHORIZATIONID"] = authorizationId ?? string.Empty }, ct);
            return Ok(v)
                ? new PaymentResult(true, PaymentStatus.Voided, TransactionId: authorizationId, GatewayReference: payment.GatewayReference)
                : PaymentResult.Failed($"PayPal: {Error(v)}");
        }

        var txnId = payment.TransactionId;
        if (string.IsNullOrWhiteSpace(txnId))
            return PaymentResult.Failed("PayPal refund requires the transaction id.");
        var full = amount >= payment.Amount - payment.RefundedAmount;
        var fields = new Dictionary<string, string>
        {
            ["METHOD"] = "RefundTransaction",
            ["TRANSACTIONID"] = txnId,
            ["REFUNDTYPE"] = full ? "Full" : "Partial",
            ["CURRENCYCODE"] = payment.CurrencyCode.ToUpperInvariant(),
        };
        if (!full)
            fields["AMT"] = Money(amount);
        var r = await CallAsync(fields, ct);
        return Ok(r)
            ? new PaymentResult(true, PaymentStatus.Refunded, TransactionId: r.GetValueOrDefault("REFUNDTRANSACTIONID"),
                GatewayReference: r.GetValueOrDefault("REFUNDTRANSACTIONID"), CapturedAmount: amount)
            : PaymentResult.Failed($"PayPal: {Error(r)}");
    }

    private async Task<Dictionary<string, string>> CallAsync(Dictionary<string, string> fields, CancellationToken ct)
    {
        fields["USER"] = _cred.ApiUsername ?? string.Empty;
        fields["PWD"] = _cred.ApiPassword ?? string.Empty;
        fields["SIGNATURE"] = _cred.ApiSignature ?? string.Empty;
        fields["VERSION"] = Version;
        using var response = await _http.PostAsync(Endpoint, new FormUrlEncodedContent(fields), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return Parse(body);
    }

    public static Dictionary<string, string> Parse(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in (body ?? string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            result[Uri.UnescapeDataString(pair[..eq].Replace('+', ' '))] = Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
        }
        return result;
    }

    private static bool Ok(Dictionary<string, string> r) =>
        r.TryGetValue("ACK", out var ack) && ack.StartsWith("Success", StringComparison.OrdinalIgnoreCase);

    private static string Error(Dictionary<string, string> r) =>
        r.GetValueOrDefault("L_LONGMESSAGE0") ?? r.GetValueOrDefault("L_SHORTMESSAGE0") ?? "the request was not accepted";

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
