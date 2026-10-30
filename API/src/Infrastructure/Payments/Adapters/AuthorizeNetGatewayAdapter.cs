using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Payments.Adapters;

/// <summary>
/// Authorize.Net gateway adapter (WO-33) using the JSON transaction API via <see cref="IHttpClientFactory"/>.
/// Credentials are stored as <c>apiLoginId:transactionKey</c> (Credentials_AuthorizeNet); the credential's
/// <c>IsProduction</c> flag selects the live vs. sandbox endpoint. The client-side payment nonce is passed
/// as opaque data. <see cref="CaptureMode"/> selects authCapture vs. authOnly transactions.
/// </summary>
public class AuthorizeNetGatewayAdapter : PaymentGatewayAdapterBase
{

    public AuthorizeNetGatewayAdapter(ICredentialVault vault, IHttpClientFactory httpClientFactory, ILogger<AuthorizeNetGatewayAdapter> logger)
        : base(vault, httpClientFactory, logger) { }

    public override PaymentMethodType Method => PaymentMethodType.AuthorizeNet;

    public override Task<PaymentResult> AuthorizeAsync(PaymentRequest req, CaptureMode mode, CancellationToken ct)
        => GuardAsync("authorize", async () =>
        {
            var auth = await ResolveAuthAsync(ct);
            if (auth is null)
                return PaymentResult.Failed("Authorize.Net credentials are not configured (format 'apiLoginId:transactionKey').");

            // The card OR bank account is tokenized in the browser by Accept.js into this opaque-data nonce
            // (bankData tokenizes to the same descriptor as cardData). Without it there is nothing to charge —
            // the gateway would reject the empty token with a generic error, so fail here with a message that
            // names the real cause instead of surfacing an opaque "declined".
            var isBankAccount = IsBankAccount(req);
            if (string.IsNullOrWhiteSpace(req.PaymentToken))
                return PaymentResult.Failed(isBankAccount
                    ? "Authorize.Net requires your bank account details. Please enter them and try again."
                    : "Authorize.Net requires card details. Please enter your card and try again.");

            // ACH/eCheck settles as a single authCapture — Authorize.Net rejects authOnly for a bank account —
            // so a bank-account payment always captures, ignoring an authorize-only capture mode. Cards honour
            // the configured mode.
            var type = mode == CaptureMode.AuthorizeOnly && !isBankAccount
                ? "authOnlyTransaction"
                : "authCaptureTransaction";
            var transactionRequest = new Dictionary<string, object?>
            {
                ["transactionType"] = type,
                ["amount"] = Money(req.Amount),
                ["payment"] = new
                {
                    opaqueData = new { dataDescriptor = "COMMON.ACCEPT.INAPP.PAYMENT", dataValue = req.PaymentToken ?? string.Empty },
                },
            };

            using var doc = await SendAsync(auth.Value.Endpoint, (auth.Value.Name, auth.Value.Key), transactionRequest, ct);
            return Map(doc.RootElement, type, req.Amount);
        });

    public override Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("capture", async () =>
        {
            var auth = await ResolveAuthAsync(ct);
            if (auth is null)
                return PaymentResult.Failed("Authorize.Net credentials are not configured.");

            var transId = payment.AuthorizationId ?? payment.GatewayReference;
            if (string.IsNullOrWhiteSpace(transId))
                return PaymentResult.Failed("Authorize.Net capture requires the original transaction id.");

            const string type = "priorAuthCaptureTransaction";
            var transactionRequest = new Dictionary<string, object?>
            {
                ["transactionType"] = type,
                ["amount"] = Money(amount),
                ["refTransId"] = transId,
            };

            using var doc = await SendAsync(auth.Value.Endpoint, (auth.Value.Name, auth.Value.Key), transactionRequest, ct);
            return Map(doc.RootElement, type, amount);
        });

    public override Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("refund", async () =>
        {
            var auth = await ResolveAuthAsync(ct);
            if (auth is null)
                return PaymentResult.Failed("Authorize.Net credentials are not configured.");

            var transId = payment.TransactionId ?? payment.AuthorizationId;
            if (string.IsNullOrWhiteSpace(transId))
                return PaymentResult.Failed("Authorize.Net refund requires the settled transaction id.");

            const string type = "refundTransaction";
            // A linked refund (by refTransId) still echoes the original instrument. An eCheck/ACH charge must be
            // refunded against a bank account, a card against a card — sending the wrong element is rejected. The
            // real account/PAN was never on our servers (it was tokenized), so masked placeholders are used, as
            // the gateway matches the refund on refTransId.
            object paymentElement = PaymentInstruments.IsBankAccount(payment.PaymentInstrument)
                ? new { bankAccount = new { accountType = "checking", routingNumber = "XXXX", accountNumber = "XXXX", nameOnAccount = "XXXX" } }
                : new { creditCard = new { cardNumber = "XXXX", expirationDate = "XXXX" } };
            var transactionRequest = new Dictionary<string, object?>
            {
                ["transactionType"] = type,
                ["amount"] = Money(amount),
                ["payment"] = paymentElement,
                ["refTransId"] = transId,
            };

            using var doc = await SendAsync(auth.Value.Endpoint, (auth.Value.Name, auth.Value.Key), transactionRequest, ct);
            return Map(doc.RootElement, type, amount);
        });

    private PaymentResult Map(JsonElement root, string transactionType, decimal amount)
    {
        var hasTxn = root.TryGetProperty("transactionResponse", out var txn) && txn.ValueKind == JsonValueKind.Object;
        // responseCode "1" = approved. Anything else (or a missing transactionResponse) is a failure.
        var responseCode = hasTxn ? ReadCode(txn, "responseCode") : null;

        if (responseCode != "1")
        {
            var reason = DescribeFailure(root, hasTxn ? txn : (JsonElement?)null);
            // Log the raw response so the real cause (invalid token, auth failure, AVS/CVV decline, …) is
            // diagnosable server-side — the buyer-facing string is necessarily terse.
            Logger.LogWarning("Authorize.Net {Type} not approved (code '{Code}'): {Reason}. Raw: {Raw}",
                transactionType, responseCode ?? string.Empty, reason, root.GetRawText());
            return PaymentResult.Failed(string.IsNullOrEmpty(responseCode)
                ? $"Authorize.Net error: {reason}"
                : $"Authorize.Net declined (code {responseCode}): {reason}");
        }

        var transId = GetString(txn, "transId");
        return transactionType switch
        {
            "authOnlyTransaction" => PaymentResult.Authorized(transId, transId),
            "refundTransaction" => new PaymentResult(true, PaymentStatus.Refunded, TransactionId: transId, GatewayReference: transId, CapturedAmount: amount),
            _ => PaymentResult.Captured(transId, amount, transId, transId),
        };
    }

    /// <summary>
    /// Resolves the active Authorize.Net credential into (apiLoginId, transactionKey) and the live/sandbox
    /// endpoint (from its production flag). Returns null when unconfigured or malformed.
    /// </summary>
    private async Task<(string Name, string Key, string Endpoint)?> ResolveAuthAsync(CancellationToken ct)
    {
        var resolved = await ResolveAsync(ct);
        if (resolved is null)
            return null;

        var parts = resolved.Value.Split(':', 2);
        if (parts.Length != 2)
            return null;

        var endpoint = RequireBaseUrl(resolved);
        return (parts[0], parts[1], endpoint);
    }

    private async Task<JsonDocument> SendAsync(string endpoint, (string Name, string Key) auth, IDictionary<string, object?> transactionRequest, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            createTransactionRequest = new
            {
                merchantAuthentication = new { name = auth.Name, transactionKey = auth.Key },
                transactionRequest,
            },
        });

        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        using var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        // Authorize.Net JSON responses are prefixed with a UTF-8 BOM that breaks a strict parser.
        payload = payload.TrimStart('﻿', ' ', '\r', '\n', '\t');
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
    }

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>True when the request's instrument metadata marks this as an ACH/eCheck bank-account payment.</summary>
    private static bool IsBankAccount(PaymentRequest req)
        => req.Metadata is { } m
           && m.TryGetValue(PaymentInstruments.MetadataKey, out var instrument)
           && PaymentInstruments.IsBankAccount(instrument);

    /// <summary>
    /// The most specific human-readable failure reason available: the transaction-level error text if present,
    /// else the top-level Authorize.Net message (auth failures, invalid Accept.js tokens, and validation errors
    /// surface there rather than under transactionResponse), else a generic fallback.
    /// </summary>
    private static string DescribeFailure(JsonElement root, JsonElement? txn)
    {
        if (txn is { } t
            && t.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            var text = GetString(errors[0], "errorText");
            if (!string.IsNullOrWhiteSpace(text)) return text!;
        }
        return DescribeError(root);
    }

    /// <summary>Authorize.Net returns responseCode as a JSON string ("1"), but tolerate a numeric 1 as well.</summary>
    private static string? ReadCode(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var n) ? n.ToString(CultureInfo.InvariantCulture) : value.GetRawText(),
            _ => null,
        };
    }

    private static string DescribeError(JsonElement root)
    {
        if (root.TryGetProperty("messages", out var messages)
            && messages.TryGetProperty("message", out var list)
            && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0)
        {
            return GetString(list[0], "text") ?? "unknown error";
        }
        return "unexpected response";
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
