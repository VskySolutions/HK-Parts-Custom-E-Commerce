using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Payments.Adapters;

/// <summary>
/// Authorize.Net gateway (docs/authorize-net-payment-requirements.md) over the JSON API at the credential's
/// endpoint (sandbox <c>apitest.authorize.net</c> / live <c>api.authorize.net</c>, chosen per credential row).
/// <list type="bullet">
/// <item>Sale / auth from an Accept.js nonce, with invoice number, customer, bill-to, ship-to, IP, PO number and
/// a 60 s duplicate window (AN-11..15). Response code 4 is "held for review", not an approval (AN-16).</item>
/// <item>AVS, CVV, auth code, network id, card brand and last four go back on the result (AN-18); the buyer gets
/// a friendly decline message and the raw code/text stays admin-side (AN-19).</item>
/// <item>Refund and cancel look the transaction up first: unsettled + full → void, settled → refund, unsettled +
/// partial → explained refusal instead of error 54 (AN-24).</item>
/// <item>CIM customer + payment profile from a sale when enabled (AN-30/31); accepted card brands enforced on
/// the returned account type (§4); a unique refId per call (AN-51); timeouts never retried blindly — the
/// sale is looked up by invoice number first (AN-54); codes-only logging when enabled (AN-55).</item>
/// </list>
/// Settings come from the active <c>Credentials_AuthorizeNet</c> row; the Transaction Key never leaves the server.
/// </summary>
// IPaymentGatewayAdapter is re-declared so ReverseAsync / GetStatusAsync below replace the interface defaults
// (the base class implements the interface, and mapping would otherwise stop there).
public class AuthorizeNetGatewayAdapter : PaymentGatewayAdapterBase, IPaymentGatewayAdapter, IAuthorizeNetClient
{
    private const string AuthCaptureType = "authCaptureTransaction";
    private const string AuthOnlyType = "authOnlyTransaction";
    private const string PriorAuthCaptureType = "priorAuthCaptureTransaction";
    private const string VoidType = "voidTransaction";
    private const string RefundType = "refundTransaction";

    /// <summary>Statuses where the money has not settled yet: only a void (full amount) can reverse them.</summary>
    private static readonly HashSet<string> Unsettled = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorizedPendingCapture", "capturedPendingSettlement", "FDSPendingReview",
        "FDSAuthorizedPendingReview", "underReview", "approvedReview",
    };

    /// <summary>Statuses where nothing can be refunded or voided any more.</summary>
    private static readonly HashSet<string> NotReversible = new(StringComparer.OrdinalIgnoreCase)
    {
        "voided", "declined", "expired", "failedReview", "generalError", "communicationError",
        "settlementError", "couldNotVoid", "returnedItem", "chargeback",
    };

    private static readonly Regex DuplicateProfileId = new(@"ID\s+(\d+)", RegexOptions.Compiled);

    private readonly IApplicationDbContext _db;

    public AuthorizeNetGatewayAdapter(
        ICredentialVault vault, IHttpClientFactory httpClientFactory, ILogger<AuthorizeNetGatewayAdapter> logger,
        IApplicationDbContext db)
        : base(vault, httpClientFactory, logger)
    {
        _db = db;
    }

    public override PaymentMethodType Method => PaymentMethodType.AuthorizeNet;

    // ------------------------------------------------------------------ sale / authorization

    public override Task<PaymentResult> AuthorizeAsync(PaymentRequest req, CaptureMode mode, CancellationToken ct)
        => GuardAsync("authorize", async () =>
        {
            var s = await LoadSettingsAsync(null, ct);
            if (s is null)
                return PaymentResult.Failed("Authorize.Net is not configured. Please choose another payment method.");

            // The card / bank account is tokenized in the browser by Accept.js; only the nonce reaches us (AN-11).
            var isBankAccount = IsBankAccount(req);
            if (string.IsNullOrWhiteSpace(req.PaymentToken))
                return PaymentResult.Failed(isBankAccount
                    ? "Please enter your bank account details and try again."
                    : "Please enter your card details and try again.");

            // AN-13: the credential row's transaction type wins; the platform capture-mode setting is the fallback.
            // ACH/eCheck is capture-only at Authorize.Net, so a bank account always runs auth-and-capture.
            var authOnly = s.TransactionType switch
            {
                "AuthOnly" => true,
                "AuthCapture" => false,
                _ => mode == CaptureMode.AuthorizeOnly,
            };
            var type = authOnly && !isBankAccount ? AuthOnlyType : AuthCaptureType;
            var invoice = InvoiceNumber(req.OrderNumber, req.OrderId);

            var txn = BuildSaleRequest(req, type, invoice);
            JsonDocument doc;
            try
            {
                doc = await SendAsync(s, "createTransactionRequest", new Dictionary<string, object?> { ["transactionRequest"] = txn }, type, req.Amount, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // AN-54: a timeout does not mean the card was not charged. Never retry blindly — look the
                // invoice up first; the 60 s duplicate window also refuses an immediate identical re-send.
                return await RecoverTimedOutSaleAsync(s, invoice, req.Amount, type, ct);
            }

            using (doc)
            {
                var result = MapTransaction(doc.RootElement, type == AuthOnlyType ? Op.Authorization : Op.Sale, req.Amount);
                if (!result.Success)
                    return result;

                // §4 "Accepted card types": enforced against what the gateway says the card is. A brand the store
                // does not take is voided straight away so no hold is left on the buyer's card.
                var brand = result.Details?.CardType;
                if (!isBankAccount && s.AcceptedCards is { Count: > 0 } accepted && !string.IsNullOrWhiteSpace(brand)
                    && !accepted.Contains(brand))
                {
                    await TryVoidAsync(s, result.TransactionId!, ct);
                    return PaymentResult.Failed($"{BrandLabel(brand)} cards are not accepted. Please use a different card.")
                        with { Details = result.Details! with { ResponseText = $"Card type {brand} not accepted; transaction {result.TransactionId} voided." } };
                }

                // AN-30/31: store a CIM profile for a signed-in customer (guests have no stable id to key it on).
                if (s.CustomerProfilesEnabled && result.Status != PaymentStatus.HeldForReview
                    && req.Customer?.CustomerId is Guid customerId && !string.IsNullOrWhiteSpace(result.TransactionId))
                {
                    var (profileId, paymentProfileId) = await CreateProfileAsync(s, result.TransactionId!, customerId, req.Customer.Email, ct);
                    if (profileId is not null)
                        result = result with { Details = result.Details! with { CustomerProfileId = profileId, CustomerPaymentProfileId = paymentProfileId } };
                }

                return result;
            }
        });

    private static Dictionary<string, object?> BuildSaleRequest(PaymentRequest req, string type, string invoice)
    {
        // Element order follows the Authorize.Net schema (transactionRequestType); the JSON API rejects any other.
        var txn = new Dictionary<string, object?>
        {
            ["transactionType"] = type,
            ["amount"] = Money(req.Amount),
            ["payment"] = new { opaqueData = new { dataDescriptor = "COMMON.ACCEPT.INAPP.PAYMENT", dataValue = req.PaymentToken } },
            ["order"] = new { invoiceNumber = invoice, description = Clip($"Order {req.OrderNumber ?? invoice}", 255) },
        };

        var c = req.Customer;
        if (!string.IsNullOrWhiteSpace(c?.PoNumber))
            txn["poNumber"] = Clip(c!.PoNumber, 25);

        if (c is not null)
        {
            var customer = new Dictionary<string, object?>();
            if (c.CustomerId is Guid id) { customer["type"] = "individual"; customer["id"] = MerchantCustomerId(id); }
            if (!string.IsNullOrWhiteSpace(c.Email)) customer["email"] = Clip(c.Email, 255);
            if (customer.Count > 0) txn["customer"] = customer;

            if (c.BillTo is { } bill) txn["billTo"] = Address(bill, includePhone: true);
            if (c.ShipTo is { } ship) txn["shipTo"] = Address(ship, includePhone: false);
            if (Ipv4(c.CustomerIp) is { } ip) txn["customerIP"] = ip;
        }

        txn["transactionSettings"] = new
        {
            setting = new[] { new { settingName = "duplicateWindow", settingValue = "60" } },
        };
        return txn;
    }

    private static Dictionary<string, object?> Address(PaymentAddress a, bool includePhone)
    {
        var line = string.Join(" ", new[] { a.Line1, a.Line2 }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        var d = new Dictionary<string, object?>();
        void Put(string key, string? value, int max) { if (!string.IsNullOrWhiteSpace(value)) d[key] = Clip(value.Trim(), max); }
        Put("firstName", a.FirstName, 50);
        Put("lastName", a.LastName, 50);
        Put("company", a.Company, 50);
        Put("address", line, 60);
        Put("city", a.City, 40);
        Put("state", a.Region, 40);
        Put("zip", a.PostalCode, 20);
        Put("country", a.CountryCode, 60);
        if (includePhone) Put("phoneNumber", a.Phone, 25);
        return d;
    }

    private async Task<PaymentResult> RecoverTimedOutSaleAsync(Settings s, string invoice, decimal amount, string type, CancellationToken ct)
    {
        Logger.LogWarning("Authorize.Net {Type} timed out for invoice {Invoice}; looking it up before reporting.", type, invoice);
        try
        {
            using var doc = await SendAsync(s, "getUnsettledTransactionListRequest", new Dictionary<string, object?>
            {
                ["sorting"] = new { orderBy = "submitTimeUTC", orderDescending = true },
                ["paging"] = new { limit = 50, offset = 1 },
            }, "getUnsettledTransactionList", null, ct);

            if (doc.RootElement.TryGetProperty("transactions", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in list.EnumerateArray())
                {
                    if (!string.Equals(GetString(t, "invoiceNumber"), invoice, StringComparison.Ordinal)) continue;
                    if (ReadDecimal(t, "settleAmount") is decimal settled && settled != amount) continue;

                    var transId = GetString(t, "transId");
                    var status = GetString(t, "transactionStatus");
                    var mapped = MapStatus(status);
                    var details = new PaymentGatewayDetails
                    {
                        ResponseText = $"Recovered after timeout: {status}",
                        CardType = GetString(t, "accountType"),
                        CardLastFour = LastFour(GetString(t, "accountNumber")),
                    };
                    return mapped switch
                    {
                        PaymentStatus.Authorized => PaymentResult.Authorized(transId, transId) with { Details = details },
                        PaymentStatus.Captured => PaymentResult.Captured(transId, amount, transId, transId) with { Details = details },
                        PaymentStatus.HeldForReview => new PaymentResult(true, PaymentStatus.HeldForReview, transId, transId, transId) { Details = details },
                        _ => PaymentResult.Failed(GenericDecline) with { Details = details },
                    };
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Logger.LogWarning(ex, "Authorize.Net timeout recovery lookup failed for invoice {Invoice}.", invoice);
        }

        return PaymentResult.Failed(
            "The card processor did not respond in time. Please wait a minute before trying again so you are not charged twice.")
            with { Details = new PaymentGatewayDetails { ResponseText = $"Timeout; no transaction found for invoice {invoice}." } };
    }

    // ------------------------------------------------------------------ capture

    public override Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("capture", async () =>
        {
            var s = await LoadSettingsAsync(null, ct);
            if (s is null)
                return PaymentResult.Failed("Authorize.Net credentials are not configured.");

            var transId = payment.AuthorizationId ?? payment.TransactionId ?? payment.GatewayReference;
            if (string.IsNullOrWhiteSpace(transId))
                return PaymentResult.Failed("Authorize.Net capture requires the original transaction id.");

            var txn = new Dictionary<string, object?>
            {
                ["transactionType"] = PriorAuthCaptureType,
                ["amount"] = Money(amount),
                ["refTransId"] = transId,
            };
            using var doc = await SendAsync(s, "createTransactionRequest", new Dictionary<string, object?> { ["transactionRequest"] = txn }, PriorAuthCaptureType, amount, ct);
            return WithRef(MapTransaction(doc.RootElement, Op.Capture, amount), transId);
        });

    // ------------------------------------------------------------------ refund / void / cancel

    public override Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("refund", () => ReverseOrRefundAsync(payment, amount, ct));

    /// <summary>Order cancellation: reverse everything still outstanding (void when unsettled, else refund).</summary>
    public Task<PaymentResult?> ReverseAsync(PaymentRecord payment, CancellationToken ct)
        => GuardNullableAsync("cancel", () => ReverseOrRefundAsync(payment, payment.Amount - payment.RefundedAmount, ct));

    private async Task<PaymentResult> ReverseOrRefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
    {
        var s = await LoadSettingsAsync(null, ct);
        if (s is null)
            return PaymentResult.Failed("Authorize.Net credentials are not configured.");

        var transId = payment.TransactionId ?? payment.AuthorizationId;
        if (string.IsNullOrWhiteSpace(transId))
            return PaymentResult.Failed("Authorize.Net refund requires the original transaction id.");
        if (amount <= 0)
            return PaymentResult.Failed("Nothing is left to refund on this payment.");

        var outstanding = payment.Amount - payment.RefundedAmount;
        var isFull = amount >= outstanding;

        // AN-24: check where the money is before choosing void vs refund.
        var details = await GetTransactionDetailsAsync(s, transId, ct);
        var status = details?.Status;

        if (status is not null && NotReversible.Contains(status))
            return Fail($"The transaction is '{status}' at Authorize.Net and cannot be refunded or voided.", transId, status);

        if (status is not null && Unsettled.Contains(status))
        {
            if (!isFull)
                return Fail(
                    "This payment has not settled yet (settlement usually runs within 24 hours). A partial refund can only be " +
                    "issued after settlement. Refund the full amount to void it now, or try again tomorrow.",
                    transId, status);
            return await VoidAsync(s, transId, amount, ct);
        }

        // Settled (or status unknown because the Transaction Details API is off for this account): refund.
        var refund = await SendRefundAsync(s, payment, transId, amount, details?.LastFour, ct);
        // Status unknown and the gateway says "not settled" (reason 54): a full reversal can still be a void.
        if (!refund.Success && status is null && isFull && refund.Details?.ReasonCode == "54")
            return await VoidAsync(s, transId, amount, ct);
        return refund;
    }

    private async Task<PaymentResult> SendRefundAsync(Settings s, PaymentRecord payment, string transId, decimal amount, string? lastFour, CancellationToken ct)
    {
        // AN-25: a linked refund sends the original transaction id plus the last four and "XXXX" expiry. The
        // instrument must match the original (bank account for eCheck); the real numbers were never stored here.
        var four = lastFour ?? payment.CardLastFour;
        object paymentElement = PaymentInstruments.IsBankAccount(payment.PaymentInstrument)
            ? new { bankAccount = new { accountType = "checking", routingNumber = "XXXX", accountNumber = four ?? "XXXX", nameOnAccount = "XXXX" } }
            : new { creditCard = new { cardNumber = four ?? "XXXX", expirationDate = "XXXX" } };
        var txn = new Dictionary<string, object?>
        {
            ["transactionType"] = RefundType,
            ["amount"] = Money(amount),
            ["payment"] = paymentElement,
            ["refTransId"] = transId,
        };
        using var doc = await SendAsync(s, "createTransactionRequest", new Dictionary<string, object?> { ["transactionRequest"] = txn }, RefundType, amount, ct);
        return WithRef(MapTransaction(doc.RootElement, Op.Refund, amount), transId);
    }

    private async Task<PaymentResult> VoidAsync(Settings s, string transId, decimal amount, CancellationToken ct)
    {
        var txn = new Dictionary<string, object?> { ["transactionType"] = VoidType, ["refTransId"] = transId };
        using var doc = await SendAsync(s, "createTransactionRequest", new Dictionary<string, object?> { ["transactionRequest"] = txn }, VoidType, amount, ct);
        var result = WithRef(MapTransaction(doc.RootElement, Op.Void, amount), transId);
        return result with { Details = (result.Details ?? new PaymentGatewayDetails()) with { PerformedAs = PaymentTransactionType.Void } };
    }

    private async Task TryVoidAsync(Settings s, string transId, CancellationToken ct)
    {
        try { await VoidAsync(s, transId, 0m, ct); }
        catch (Exception ex) { Logger.LogWarning(ex, "Authorize.Net void of rejected card-brand transaction {TransId} failed.", transId); }
    }

    // ------------------------------------------------------------------ status lookup

    public Task<PaymentResult?> GetStatusAsync(PaymentRecord payment, CancellationToken ct)
        => GuardNullableAsync("status", async () =>
        {
            var s = await LoadSettingsAsync(null, ct);
            if (s is null)
                return PaymentResult.Failed("Authorize.Net credentials are not configured.");
            var transId = payment.TransactionId ?? payment.AuthorizationId;
            if (string.IsNullOrWhiteSpace(transId))
                return PaymentResult.Failed("The payment has no Authorize.Net transaction id.");

            var d = await GetTransactionDetailsAsync(s, transId, ct);
            if (d is null)
                return PaymentResult.Failed("Could not read the transaction from Authorize.Net. Check that the Transaction Details API is enabled for the account.");

            var mapped = MapStatus(d.Status);
            var details = new PaymentGatewayDetails { ResponseText = d.Status, CardType = d.CardType, CardLastFour = d.LastFour, AuthCode = d.AuthCode };
            return mapped is null
                ? PaymentResult.Failed($"Unrecognised Authorize.Net transaction status '{d.Status}'.") with { Details = details }
                : new PaymentResult(true, mapped.Value, transId, transId, transId, CapturedAmount: d.SettleAmount) { Details = details };
        });

    private static PaymentStatus? MapStatus(string? status) => status switch
    {
        "authorizedPendingCapture" => PaymentStatus.Authorized,
        "capturedPendingSettlement" or "settledSuccessfully" => PaymentStatus.Captured,
        "FDSPendingReview" or "FDSAuthorizedPendingReview" or "underReview" => PaymentStatus.HeldForReview,
        "voided" => PaymentStatus.Voided,
        "declined" or "expired" or "failedReview" or "generalError" or "communicationError" or "settlementError" => PaymentStatus.Failed,
        "refundSettledSuccessfully" or "refundPendingSettlement" => PaymentStatus.Refunded,
        _ => null,
    };

    private sealed record TransactionDetails(string? Status, string? LastFour, string? CardType, string? AuthCode, decimal? SettleAmount);

    private async Task<TransactionDetails?> GetTransactionDetailsAsync(Settings s, string transId, CancellationToken ct)
    {
        try
        {
            using var doc = await SendAsync(s, "getTransactionDetailsRequest", new Dictionary<string, object?> { ["transId"] = transId }, "getTransactionDetails", null, ct);
            var root = doc.RootElement;
            if (!IsOk(root) || !root.TryGetProperty("transaction", out var t) || t.ValueKind != JsonValueKind.Object)
                return null;

            string? cardNumber = null, cardType = null;
            if (t.TryGetProperty("payment", out var pay) && pay.ValueKind == JsonValueKind.Object)
            {
                if (pay.TryGetProperty("creditCard", out var cc) && cc.ValueKind == JsonValueKind.Object)
                {
                    cardNumber = GetString(cc, "cardNumber");
                    cardType = GetString(cc, "cardType");
                }
                else if (pay.TryGetProperty("bankAccount", out var ba) && ba.ValueKind == JsonValueKind.Object)
                {
                    cardNumber = GetString(ba, "accountNumber");
                    cardType = "eCheck";
                }
            }
            return new TransactionDetails(GetString(t, "transactionStatus"), LastFour(cardNumber), cardType,
                GetString(t, "authCode"), ReadDecimal(t, "settleAmount"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            Logger.LogWarning(ex, "Authorize.Net getTransactionDetails failed for {TransId}.", transId);
            return null;
        }
    }

    // ------------------------------------------------------------------ customer profiles (CIM)

    private async Task<(string? ProfileId, string? PaymentProfileId)> CreateProfileAsync(
        Settings s, string transId, Guid customerId, string? email, CancellationToken ct)
    {
        try
        {
            var body = new Dictionary<string, object?>
            {
                ["transId"] = transId,
                // AN-31: keyed on our customer id (never the email), so two guests can never collide.
                ["customer"] = new { merchantCustomerId = MerchantCustomerId(customerId), email = Clip(email, 255) },
            };
            using var doc = await SendAsync(s, "createCustomerProfileFromTransactionRequest", body, "createCustomerProfileFromTransaction", null, ct);
            var root = doc.RootElement;
            if (IsOk(root))
                return (GetString(root, "customerProfileId"), FirstPaymentProfileId(root));

            // E00039: the customer already has a profile — add this card to it as a new payment profile.
            var (code, text) = TopMessage(root);
            if (code == "E00039" && text is not null && DuplicateProfileId.Match(text) is { Success: true } m)
            {
                var existing = m.Groups[1].Value;
                using var again = await SendAsync(s, "createCustomerProfileFromTransactionRequest", new Dictionary<string, object?>
                {
                    ["transId"] = transId,
                    ["customerProfileId"] = existing,
                }, "createCustomerProfileFromTransaction", null, ct);
                return (existing, IsOk(again.RootElement) ? FirstPaymentProfileId(again.RootElement) : null);
            }

            Logger.LogWarning("Authorize.Net customer profile not created for transaction {TransId}: {Code} {Text}", transId, code, text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // A profile is a convenience; it must never fail an approved payment.
            Logger.LogWarning(ex, "Authorize.Net customer profile creation failed for transaction {TransId}.", transId);
        }
        return (null, null);
    }

    private static string? FirstPaymentProfileId(JsonElement root)
        => root.TryGetProperty("customerPaymentProfileIdList", out var list)
           && list.ValueKind == JsonValueKind.Object
           && list.TryGetProperty("numericString", out var ids)
           && ids.ValueKind == JsonValueKind.Array && ids.GetArrayLength() > 0
            ? ids[0].ValueKind == JsonValueKind.String ? ids[0].GetString() : ids[0].GetRawText()
            : null;

    // ------------------------------------------------------------------ admin test (AN-41)

    public async Task<GatewayTestResult> TestCredentialAsync(Guid credentialId, CancellationToken ct)
    {
        var s = await LoadSettingsAsync(credentialId, ct);
        if (s is null)
            return new GatewayTestResult { Connected = false, Message = "The credential is missing its endpoint, API Login ID or Transaction Key (or the key could not be decrypted — re-enter it)." };

        var sw = Stopwatch.StartNew();
        try
        {
            using var doc = await SendAsync(s, "authenticateTestRequest", new Dictionary<string, object?>(), "authenticateTest", null, ct);
            var (code, text) = TopMessage(doc.RootElement);
            return new GatewayTestResult
            {
                Connected = IsOk(doc.RootElement),
                Code = code,
                Message = IsOk(doc.RootElement) ? "Connected" : text ?? "Authentication failed.",
                Environment = EnvironmentOf(s.Endpoint),
                ElapsedMs = sw.ElapsedMilliseconds,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return new GatewayTestResult { Connected = false, Message = ex.Message, Environment = EnvironmentOf(s.Endpoint), ElapsedMs = sw.ElapsedMilliseconds };
        }
    }

    private static string EnvironmentOf(string endpoint)
        => endpoint.Contains("apitest.", StringComparison.OrdinalIgnoreCase) ? "Sandbox" : "Production";

    // ------------------------------------------------------------------ response mapping

    private enum Op { Sale, Authorization, Capture, Void, Refund }

    /// <summary>
    /// AN-50: approved only when the transaction's responseCode is 1; 4 is held for review; 2 declined; 3 error.
    /// Every nested read is guarded — some errors come back with no transactionResponse at all.
    /// </summary>
    private PaymentResult MapTransaction(JsonElement root, Op op, decimal amount)
    {
        var hasTxn = root.TryGetProperty("transactionResponse", out var txn) && txn.ValueKind == JsonValueKind.Object;
        var responseCode = hasTxn ? ReadCode(txn, "responseCode") : null;
        var transId = hasTxn ? GetString(txn, "transId") : null;
        if (transId == "0") transId = null;

        string? reasonCode = null, reasonText = null;
        if (hasTxn && txn.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            reasonCode = ReadCode(errors[0], "errorCode");
            reasonText = GetString(errors[0], "errorText");
        }
        else if (hasTxn && txn.TryGetProperty("messages", out var msgs) && msgs.ValueKind == JsonValueKind.Array && msgs.GetArrayLength() > 0)
        {
            reasonCode = ReadCode(msgs[0], "code");
            reasonText = GetString(msgs[0], "description");
        }
        if (reasonCode is null)
            (reasonCode, reasonText) = TopMessage(root);

        var details = new PaymentGatewayDetails
        {
            ResponseCode = responseCode,
            ReasonCode = reasonCode,
            ResponseText = reasonText,
            AuthCode = hasTxn ? NullIfBlank(GetString(txn, "authCode")) : null,
            AvsResultCode = hasTxn ? NullIfBlank(GetString(txn, "avsResultCode")) : null,
            CvvResultCode = hasTxn ? NullIfBlank(GetString(txn, "cvvResultCode")) : null,
            NetworkTransactionId = hasTxn ? NullIfBlank(GetString(txn, "networkTransId")) : null,
            CardType = hasTxn ? NullIfBlank(GetString(txn, "accountType")) : null,
            CardLastFour = hasTxn ? LastFour(GetString(txn, "accountNumber")) : null,
            RefTransactionId = hasTxn ? NullIfBlank(GetString(txn, "refTransID")) : null,
        };

        if (responseCode == "1")
        {
            var ok = op switch
            {
                Op.Authorization => PaymentResult.Authorized(transId, transId),
                Op.Refund => new PaymentResult(true, PaymentStatus.Refunded, TransactionId: transId, GatewayReference: transId, CapturedAmount: amount),
                Op.Void => new PaymentResult(true, PaymentStatus.Voided, TransactionId: transId, GatewayReference: transId),
                _ => PaymentResult.Captured(transId, amount, transId, transId),
            };
            return ok with { Details = details };
        }

        if (responseCode == "4" && op is Op.Sale or Op.Authorization)
        {
            // AN-16: held by the Fraud Detection Suite. Not an approval and not a decline.
            return new PaymentResult(true, PaymentStatus.HeldForReview, transId, transId, transId) { Details = details };
        }

        var adminText = reasonText ?? "unexpected response";
        Logger.LogWarning("Authorize.Net {Op} not approved: responseCode {ResponseCode}, reason {ReasonCode}: {ReasonText}",
            op, responseCode ?? "-", reasonCode ?? "-", adminText);

        // AN-19: the buyer sees a friendly message; AN-27: admin operations show the gateway's own reason.
        var message = op is Op.Sale or Op.Authorization
            ? FriendlyDecline(reasonCode)
            : $"Authorize.Net: {adminText}{(reasonCode is null ? string.Empty : $" (code {reasonCode})")}";
        return PaymentResult.Failed(message) with { Details = details with { ResponseText = adminText } };
    }

    private const string GenericDecline = "Your payment could not be processed. Please check your card details or use a different payment method.";

    /// <summary>Buyer-facing text for the common reason codes (AN-19). The raw code/text stays on the payment row.</summary>
    internal static string FriendlyDecline(string? reasonCode) => reasonCode switch
    {
        "2" or "3" or "4" or "41" or "250" or "251" or "254" => "Your card was declined. Please use a different card or contact your card issuer.",
        "6" or "37" or "315" => "The card number is invalid. Please check it and try again.",
        "7" or "8" or "316" or "317" => "The card's expiration date is invalid or the card has expired.",
        "27" or "127" => "The billing address does not match the address your card issuer has on file. Please check the address and ZIP code.",
        "44" or "45" or "65" => "The card security code (CVV) did not match. Please check it and try again.",
        "11" => "This looks like a duplicate of a payment made a moment ago. Please wait two minutes before trying again.",
        "E00114" or "E00117" or "E00118" => "Your card details could not be processed. Please re-enter your card and try again.",
        _ => GenericDecline,
    };

    private static PaymentResult Fail(string message, string transId, string? status)
        => PaymentResult.Failed(message) with { Details = new PaymentGatewayDetails { RefTransactionId = transId, ResponseText = status is null ? message : $"{status}: {message}" } };

    private static PaymentResult WithRef(PaymentResult r, string refTransId)
        => r with { Details = (r.Details ?? new PaymentGatewayDetails()) with { RefTransactionId = r.Details?.RefTransactionId ?? refTransId } };

    // ------------------------------------------------------------------ transport

    /// <summary>The active (or a specific) credential row as the adapter needs it, or null when unusable.</summary>
    private sealed record Settings(
        string Endpoint, string LoginId, string TransactionKey, string? TransactionType,
        bool CustomerProfilesEnabled, bool LoggingEnabled, HashSet<string>? AcceptedCards);

    private async Task<Settings?> LoadSettingsAsync(Guid? credentialId, CancellationToken ct)
    {
        var query = _db.AuthorizeNetCredentials.AsNoTracking();
        var c = credentialId is Guid id
            ? await query.FirstOrDefaultAsync(x => x.Id == id, ct)
            : await query.Where(x => x.Active).OrderByDescending(x => x.UpdatedOnUtc).FirstOrDefaultAsync(ct);
        if (c is null || string.IsNullOrWhiteSpace(c.BaseUrl) || string.IsNullOrWhiteSpace(c.ApplicationLoginId)
            || string.IsNullOrWhiteSpace(c.TransactionKey))
            return null;

        var cards = SplitList(c.AcceptedCardTypes);
        return new Settings(
            c.BaseUrl.Trim().TrimEnd('/'), c.ApplicationLoginId.Trim(), c.TransactionKey.Trim(), c.TransactionType,
            c.CustomerProfilesEnabled, c.LoggingEnabled,
            cards.Count > 0 ? new HashSet<string>(cards, StringComparer.OrdinalIgnoreCase) : null);
    }

    private async Task<JsonDocument> SendAsync(
        Settings s, string requestName, IDictionary<string, object?> body, string call, decimal? amount, CancellationToken ct)
    {
        // AN-51: a unique refId per call (max 20 chars), echoed back by the gateway.
        var refId = Guid.NewGuid().ToString("N")[..20];
        var request = new Dictionary<string, object?>
        {
            ["merchantAuthentication"] = new { name = s.LoginId, transactionKey = s.TransactionKey },
            ["refId"] = refId,
        };
        foreach (var kv in body) request[kv.Key] = kv.Value;
        var json = JsonSerializer.Serialize(new Dictionary<string, object?> { [requestName] = request });

        var client = CreateClient(); // 30 s timeout (AN-54)
        using var message = new HttpRequestMessage(HttpMethod.Post, s.Endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        var sw = Stopwatch.StartNew();
        using var response = await client.SendAsync(message, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        sw.Stop();

        // Authorize.Net JSON responses start with a UTF-8 BOM that a strict parser rejects.
        payload = payload.TrimStart('﻿', ' ', '\r', '\n', '\t');
        var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);

        if (s.LoggingEnabled)
        {
            // AN-55: codes and ids only — never the request (nonce, keys) or card data.
            var root = doc.RootElement;
            var hasTxn = root.TryGetProperty("transactionResponse", out var txn) && txn.ValueKind == JsonValueKind.Object;
            var (code, _) = TopMessage(root);
            Logger.LogInformation(
                "Authorize.Net {Call}: HTTP {Status}, result {Result} ({Code}), responseCode {ResponseCode}, transId {TransId}, amount {Amount}, refId {RefId}, {ElapsedMs} ms",
                call, (int)response.StatusCode, ResultCode(root) ?? "-", code ?? "-",
                hasTxn ? ReadCode(txn, "responseCode") ?? "-" : "-",
                hasTxn ? GetString(txn, "transId") ?? "-" : "-",
                amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-", refId, sw.ElapsedMilliseconds);
        }
        return doc;
    }

    private async Task<PaymentResult?> GuardNullableAsync(string operation, Func<Task<PaymentResult>> action)
        => await GuardAsync(operation, action);

    // ------------------------------------------------------------------ helpers

    /// <summary>AN-15: invoiceNumber max 20 chars. "ORD-yyyyMMddHHmmssfff" is 21, so the "ORD-" prefix is dropped.</summary>
    internal static string InvoiceNumber(string? orderNumber, Guid orderId)
    {
        var n = string.IsNullOrWhiteSpace(orderNumber) ? orderId.ToString("N") : orderNumber.Trim();
        if (n.Length > 20 && n.StartsWith("ORD-", StringComparison.OrdinalIgnoreCase)) n = n[4..];
        return n.Length > 20 ? n[..20] : n;
    }

    /// <summary>customer.id / merchantCustomerId are max 20 chars; the first 20 hex digits of our Guid.</summary>
    internal static string MerchantCustomerId(Guid customerId) => customerId.ToString("N")[..20];

    private static string? LastFour(string? masked)
    {
        if (string.IsNullOrWhiteSpace(masked)) return null;
        var digits = new string(masked.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? digits[^4..] : null;
    }

    private static string BrandLabel(string brand) => brand switch
    {
        "AmericanExpress" => "American Express",
        "DinersClub" => "Diners Club",
        "MasterCard" => "Mastercard",
        _ => brand,
    };

    private static List<string> SplitList(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? new List<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static bool IsOk(JsonElement root) => string.Equals(ResultCode(root), "Ok", StringComparison.OrdinalIgnoreCase);

    private static string? ResultCode(JsonElement root)
        => root.TryGetProperty("messages", out var m) && m.ValueKind == JsonValueKind.Object ? GetString(m, "resultCode") : null;

    private static (string? Code, string? Text) TopMessage(JsonElement root)
    {
        if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Object
            && messages.TryGetProperty("message", out var list)
            && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0)
            return (GetString(list[0], "code"), GetString(list[0], "text"));
        return (null, null);
    }

    private static string Money(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    private static string? Clip(string? value, int max)
        => string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];

    private static string? NullIfBlank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;

    /// <summary>customerIP takes an IPv4 address (max 15). IPv4-mapped IPv6 is unwrapped; pure IPv6 is left out.</summary>
    private static string? Ipv4(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !System.Net.IPAddress.TryParse(raw.Trim(), out var ip)) return null;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? ip.ToString() : null;
    }

    /// <summary>True when the request's instrument metadata marks this as an ACH/eCheck bank-account payment.</summary>
    private static bool IsBankAccount(PaymentRequest req)
        => req.Metadata is { } m
           && m.TryGetValue(PaymentInstruments.MetadataKey, out var instrument)
           && PaymentInstruments.IsBankAccount(instrument);

    /// <summary>Authorize.Net sends codes as JSON strings ("1"), but tolerate numbers.</summary>
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

    private static decimal? ReadDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var d) => d,
            JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
