using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;
using HKPartsEcommerce.Application.Features.Payments;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Payments.Adapters;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Payments;

/// <summary>
/// Authorize.Net (docs/authorize-net-payment-requirements.md). Case ids (AN-T…) are the spec's §6 acceptance
/// tests; the gateway is replaced by a recording fake that answers per request type, like the sandbox would.
/// </summary>
public class AuthorizeNetGatewayTests : CatalogTestBase
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    // ------------------------------------------------------------------ fake gateway

    private sealed class FakeAnet : HttpMessageHandler
    {
        public readonly List<(string Request, JsonElement Body)> Calls = new();

        /// <summary>Answer for a call: request name ("createTransactionRequest") + its body → JSON. Null = default.</summary>
        public Func<string, JsonElement, string?>? Respond;
        public bool TimeOutSales;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var text = await request.Content!.ReadAsStringAsync(ct);
            var root = JsonDocument.Parse(text).RootElement.Clone();
            var prop = root.EnumerateObject().Single();
            lock (Calls) Calls.Add((prop.Name, prop.Value));

            if (TimeOutSales && prop.Name == "createTransactionRequest"
                && TxnType(prop.Value) is "authCaptureTransaction" or "authOnlyTransaction")
                throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");

            var json = Respond?.Invoke(prop.Name, prop.Value) ?? Default(prop.Name, prop.Value);
            // Authorize.Net really does prefix its JSON with a BOM.
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("﻿" + json, Encoding.UTF8, "application/json") };
        }

        private static string Default(string name, JsonElement body) => name switch
        {
            "createTransactionRequest" => Approved(TxnType(body) == "voidTransaction" ? "0" : "60001"),
            "authenticateTestRequest" => Ok(),
            _ => Ok(),
        };
    }

    internal static string? TxnType(JsonElement body)
        => body.TryGetProperty("transactionRequest", out var t) ? t.GetProperty("transactionType").GetString() : null;

    private static string Ok() => "{\"messages\":{\"resultCode\":\"Ok\",\"message\":[{\"code\":\"I00001\",\"text\":\"Successful.\"}]}}";

    private static string Approved(string transId = "60001", string accountType = "Visa", string last4 = "1111", string responseCode = "1") =>
        "{\"transactionResponse\":{\"responseCode\":\"" + responseCode + "\",\"authCode\":\"ABC123\",\"avsResultCode\":\"Y\",\"cvvResultCode\":\"M\"," +
        "\"transId\":\"" + transId + "\",\"refTransID\":\"\",\"accountNumber\":\"XXXX" + last4 + "\",\"accountType\":\"" + accountType + "\"," +
        "\"networkTransId\":\"NTI0001\",\"messages\":[{\"code\":\"" + (responseCode == "4" ? "252" : "1") + "\",\"description\":\"This transaction has been approved.\"}]}," +
        "\"messages\":{\"resultCode\":\"Ok\",\"message\":[{\"code\":\"I00001\",\"text\":\"Successful.\"}]}}";

    private static string Declined(string reason, string text = "This transaction has been declined.") =>
        "{\"transactionResponse\":{\"responseCode\":\"2\",\"transId\":\"60002\",\"accountNumber\":\"XXXX0002\",\"accountType\":\"Visa\"," +
        "\"errors\":[{\"errorCode\":\"" + reason + "\",\"errorText\":\"" + text + "\"}]}," +
        "\"messages\":{\"resultCode\":\"Error\",\"message\":[{\"code\":\"E00027\",\"text\":\"The transaction was unsuccessful.\"}]}}";

    private static string Details(string status, decimal settle = 25m) =>
        "{\"transaction\":{\"transId\":\"60001\",\"transactionStatus\":\"" + status + "\",\"responseCode\":1,\"authCode\":\"ABC123\"," +
        "\"settleAmount\":" + settle.ToString(System.Globalization.CultureInfo.InvariantCulture) +
        ",\"payment\":{\"creditCard\":{\"cardNumber\":\"XXXX1111\",\"expirationDate\":\"XXXX\",\"cardType\":\"Visa\"}}}," +
        "\"messages\":{\"resultCode\":\"Ok\",\"message\":[{\"code\":\"I00001\",\"text\":\"Successful.\"}]}}";

    private sealed class Factory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public Factory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    /// <summary>Routes capture / refund / reverse / status straight to one adapter (what the real router does).</summary>
    private sealed class AdapterRouter : IPaymentGatewayRouter
    {
        private readonly IPaymentGatewayAdapter _a;
        public AdapterRouter(IPaymentGatewayAdapter a) => _a = a;
        public Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default) => _a.AuthorizeAsync(request, CaptureMode.AuthorizeAndCapture, ct);
        public Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default) => _a.CaptureAsync(payment, amount, ct);
        public Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default) => _a.RefundAsync(payment, amount, ct);
        public Task<PaymentResult> VerifyRedirectAsync(PaymentRecord payment, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaymentResult> VerifyClientPaymentAsync(PaymentRecord payment, IReadOnlyDictionary<string, string> data, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CaptureForOrderAsync(Guid orderId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PaymentMethodAvailability>> AvailableMethodsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PaymentResult?> ReverseAsync(PaymentRecord payment, CancellationToken ct = default) => _a.ReverseAsync(payment, ct);
        public Task<PaymentResult?> GetStatusAsync(PaymentRecord payment, CancellationToken ct = default) => _a.GetStatusAsync(payment, ct);
    }

    // ------------------------------------------------------------------ fixtures

    private AuthorizeNetCredential Seed(Action<AuthorizeNetCredential>? configure = null)
    {
        var c = new AuthorizeNetCredential
        {
            Name = "Sandbox",
            Active = true,
            BaseUrl = "https://apitest.authorize.net/xml/v1/request.api",
            ApplicationLoginId = "login",
            TransactionKey = "tk-secret-5678",
            PublicClientKey = "client",
            TransactionType = "AuthCapture",
        };
        configure?.Invoke(c);
        using var db = NewContext();
        db.AuthorizeNetCredentials.Add(c);
        db.SaveChanges();
        return c;
    }

    private AuthorizeNetGatewayAdapter Adapter(FakeAnet fake)
        => new(null!, new Factory(fake), NullLogger<AuthorizeNetGatewayAdapter>.Instance, NewContext());

    private static PaymentRequest Sale(decimal amount = 25m, Guid? customerId = null) => new(
        Guid.NewGuid(), PaymentMethodType.AuthorizeNet, amount, "USD", "nonce-value",
        OrderNumber: "ORD-20261106120000123",
        Customer: new PaymentCustomer(
            customerId, "buyer@example.com",
            new PaymentAddress("Jane", "Doe", null, "1 Main St", "Suite 5", "Charlotte", "NC", "28270", "US", "704-555-0100"),
            new PaymentAddress("Jane", "Doe", null, "1 Main St", "Suite 5", "Charlotte", "NC", "28270", "US"),
            "::ffff:203.0.113.7"));

    private PaymentRecord SeedPayment(PaymentStatus status, decimal amount = 25m, decimal refunded = 0m, string transId = "60001")
    {
        using var db = NewContext();
        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}"[..20],
            Status = OrderStatus.Pending,
            PaymentStatus = status,
            PlacedOnUtc = Now,
            CurrencyCode = "USD",
            TotalAmount = amount,
        };
        db.Orders.Add(order);
        var payment = new PaymentRecord
        {
            OrderId = order.Id,
            Method = PaymentMethodType.AuthorizeNet,
            Status = status,
            Amount = amount,
            AuthorizedAmount = amount,
            RefundedAmount = refunded,
            TransactionId = transId,
            AuthorizationId = transId,
            CardLastFour = "1111",
            CurrencyCode = "USD",
        };
        db.PaymentRecords.Add(payment);
        db.SaveChanges();
        return payment;
    }

    private PaymentRecord Reload(Guid id)
    {
        using var db = NewContext();
        return db.PaymentRecords.Include(p => p.Transactions).AsNoTracking().Single(p => p.Id == id);
    }

    // ------------------------------------------------------------------ sale

    [Fact]
    public async Task AN_T1_approved_sale_returns_auth_code_avs_cvv_brand_and_last_four()
    {
        Seed();
        var fake = new FakeAnet();
        var result = await Adapter(fake).AuthorizeAsync(Sale(), CaptureMode.AuthorizeAndCapture, default);

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Captured, result.Status);
        Assert.Equal("60001", result.TransactionId);
        var d = result.Details!;
        Assert.Equal(("ABC123", "Y", "M", "1111", "Visa", "NTI0001"), (d.AuthCode, d.AvsResultCode, d.CvvResultCode, d.CardLastFour, d.CardType, d.NetworkTransactionId));
    }

    [Fact]
    public async Task AN_T10_request_carries_invoice_bill_to_ship_to_phone_ip_and_duplicate_window()
    {
        Seed();
        var fake = new FakeAnet();
        await Adapter(fake).AuthorizeAsync(Sale(), CaptureMode.AuthorizeAndCapture, default);

        var (name, body) = Assert.Single(fake.Calls);
        Assert.Equal("createTransactionRequest", name);
        Assert.Equal(20, body.GetProperty("refId").GetString()!.Length); // AN-51
        var t = body.GetProperty("transactionRequest");
        Assert.Equal("authCaptureTransaction", t.GetProperty("transactionType").GetString());
        Assert.Equal("25.00", t.GetProperty("amount").GetString());
        Assert.Equal("COMMON.ACCEPT.INAPP.PAYMENT", t.GetProperty("payment").GetProperty("opaqueData").GetProperty("dataDescriptor").GetString());
        // 21-char order number → "ORD-" dropped to fit invoiceNumber's 20.
        Assert.Equal("20261106120000123", t.GetProperty("order").GetProperty("invoiceNumber").GetString());
        Assert.Equal("1 Main St Suite 5", t.GetProperty("billTo").GetProperty("address").GetString());
        Assert.Equal("704-555-0100", t.GetProperty("billTo").GetProperty("phoneNumber").GetString());
        Assert.Equal("Charlotte", t.GetProperty("shipTo").GetProperty("city").GetString());
        Assert.Equal("203.0.113.7", t.GetProperty("customerIP").GetString());
        Assert.Equal("buyer@example.com", t.GetProperty("customer").GetProperty("email").GetString());
        var setting = t.GetProperty("transactionSettings").GetProperty("setting")[0];
        Assert.Equal(("duplicateWindow", "60"), (setting.GetProperty("settingName").GetString(), setting.GetProperty("settingValue").GetString()));

        // Schema order matters to the JSON API: these must appear in this sequence.
        var order = t.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(new[] { "transactionType", "amount", "payment", "order", "customer", "billTo", "shipTo", "customerIP", "transactionSettings" }, order);
    }

    [Fact]
    public async Task AN_T2_auth_only_setting_sends_authOnlyTransaction_and_leaves_it_authorized()
    {
        Seed(c => c.TransactionType = "AuthOnly");
        var fake = new FakeAnet();
        var result = await Adapter(fake).AuthorizeAsync(Sale(), CaptureMode.AuthorizeAndCapture, default);

        Assert.Equal("authOnlyTransaction", TxnType(fake.Calls[0].Body));
        Assert.Equal(PaymentStatus.Authorized, result.Status);
    }

    [Fact]
    public async Task AN_T4_decline_gives_a_friendly_message_and_keeps_the_raw_code()
    {
        Seed();
        var fake = new FakeAnet { Respond = (n, _) => n == "createTransactionRequest" ? Declined("2") : null };
        var result = await Adapter(fake).AuthorizeAsync(Sale(), CaptureMode.AuthorizeAndCapture, default);

        Assert.False(result.Success);
        Assert.Equal("Your card was declined. Please use a different card or contact your card issuer.", result.ErrorMessage);
        Assert.Equal("2", result.Details!.ResponseCode);
        Assert.Equal("2", result.Details.ReasonCode);
        Assert.Equal("This transaction has been declined.", result.Details.ResponseText);
    }

    [Theory]
    [InlineData("27", "billing address")]
    [InlineData("44", "security code")]
    [InlineData("65", "security code")]
    [InlineData("6", "card number is invalid")]
    [InlineData("8", "expired")]
    public async Task AN_19_common_reason_codes_get_specific_messages(string reason, string fragment)
    {
        Seed();
        var fake = new FakeAnet { Respond = (n, _) => n == "createTransactionRequest" ? Declined(reason, "raw") : null };
        var result = await Adapter(fake).AuthorizeAsync(Sale(), CaptureMode.AuthorizeAndCapture, default);
        Assert.Contains(fragment, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw", result.ErrorMessage);
    }

    [Fact]
    public async Task AN_T5_response_code_4_is_held_for_review_not_an_approval()
    {
        Seed();
        var fake = new FakeAnet { Respond = (n, _) => n == "createTransactionRequest" ? Approved(responseCode: "4") : null };
        var result = await Adapter(fake).AuthorizeAsync(Sale(), CaptureMode.AuthorizeAndCapture, default);

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.HeldForReview, result.Status);
        Assert.Equal("60001", result.TransactionId);
    }

    [Fact]
    public async Task AN_50_error_without_transactionResponse_is_a_failure_not_a_crash()
    {
        Seed();
        var fake = new FakeAnet
        {
            Respond = (n, _) => "{\"messages\":{\"resultCode\":\"Error\",\"message\":[{\"code\":\"E00007\",\"text\":\"User authentication failed due to invalid authentication values.\"}]}}",
        };
        var result = await Adapter(fake).AuthorizeAsync(Sale(), CaptureMode.AuthorizeAndCapture, default);

        Assert.False(result.Success);
        Assert.Equal("E00007", result.Details!.ReasonCode);
        Assert.Contains("invalid authentication", result.Details.ResponseText);
    }

    [Fact]
    public async Task Card_brand_not_accepted_is_voided_and_refused()
    {
        Seed(c => c.AcceptedCardTypes = "Visa,MasterCard");
        var fake = new FakeAnet
        {
            Respond = (n, b) => n == "createTransactionRequest" && TxnType(b) == "authCaptureTransaction" ? Approved(accountType: "AmericanExpress", last4: "0005") : null,
        };
        var result = await Adapter(fake).AuthorizeAsync(Sale(), CaptureMode.AuthorizeAndCapture, default);

        Assert.False(result.Success);
        Assert.Contains("American Express", result.ErrorMessage);
        Assert.Equal("voidTransaction", TxnType(fake.Calls[1].Body));
        Assert.Equal("60001", fake.Calls[1].Body.GetProperty("transactionRequest").GetProperty("refTransId").GetString());
    }

    [Fact]
    public async Task AN_T8_customer_profiles_store_customer_and_payment_profile_ids()
    {
        Seed(c => c.CustomerProfilesEnabled = true);
        var customerId = Guid.NewGuid();
        var fake = new FakeAnet
        {
            Respond = (n, _) => n == "createCustomerProfileFromTransactionRequest"
                ? "{\"customerProfileId\":\"900100\",\"customerPaymentProfileIdList\":{\"numericString\":[\"800200\"]},\"messages\":{\"resultCode\":\"Ok\",\"message\":[{\"code\":\"I00001\",\"text\":\"Successful.\"}]}}"
                : null,
        };
        var result = await Adapter(fake).AuthorizeAsync(Sale(customerId: customerId), CaptureMode.AuthorizeAndCapture, default);

        Assert.Equal(("900100", "800200"), (result.Details!.CustomerProfileId, result.Details.CustomerPaymentProfileId));
        var profileCall = fake.Calls.Single(c => c.Request == "createCustomerProfileFromTransactionRequest").Body;
        Assert.Equal("60001", profileCall.GetProperty("transId").GetString());
        // AN-31: keyed on the customer id, not the email.
        Assert.Equal(customerId.ToString("N")[..20], profileCall.GetProperty("customer").GetProperty("merchantCustomerId").GetString());
    }

    [Fact]
    public async Task Customer_profiles_are_skipped_for_guests()
    {
        Seed(c => c.CustomerProfilesEnabled = true);
        var fake = new FakeAnet();
        await Adapter(fake).AuthorizeAsync(Sale(customerId: null), CaptureMode.AuthorizeAndCapture, default);
        Assert.DoesNotContain(fake.Calls, c => c.Request == "createCustomerProfileFromTransactionRequest");
    }

    [Fact]
    public async Task AN_54_timeout_looks_the_invoice_up_instead_of_retrying()
    {
        Seed();
        var fake = new FakeAnet
        {
            TimeOutSales = true,
            Respond = (n, _) => n == "getUnsettledTransactionListRequest"
                ? "{\"transactions\":[{\"transId\":\"70007\",\"invoiceNumber\":\"20261106120000123\",\"transactionStatus\":\"capturedPendingSettlement\",\"settleAmount\":25.00,\"accountType\":\"Visa\",\"accountNumber\":\"XXXX1111\"}],\"messages\":{\"resultCode\":\"Ok\",\"message\":[{\"code\":\"I00001\",\"text\":\"Successful.\"}]}}"
                : null,
        };
        var result = await Adapter(fake).AuthorizeAsync(Sale(), CaptureMode.AuthorizeAndCapture, default);

        Assert.Equal(PaymentStatus.Captured, result.Status);
        Assert.Equal("70007", result.TransactionId);
        Assert.Single(fake.Calls, c => c.Request == "createTransactionRequest"); // never re-sent
    }

    // ------------------------------------------------------------------ capture / refund / void

    [Fact]
    public async Task AN_T6_cancelling_an_unsettled_payment_sends_a_void()
    {
        Seed();
        var payment = SeedPayment(PaymentStatus.Captured);
        var fake = new FakeAnet { Respond = (n, _) => n == "getTransactionDetailsRequest" ? Details("capturedPendingSettlement") : null };

        var result = await Adapter(fake).ReverseAsync(payment, default);

        Assert.True(result!.Success);
        Assert.Equal(PaymentStatus.Voided, result.Status);
        Assert.Equal("voidTransaction", TxnType(fake.Calls.Last().Body));
        Assert.DoesNotContain(fake.Calls, c => TxnType(c.Body) == "refundTransaction");
    }

    [Fact]
    public async Task AN_24_partial_refund_of_an_unsettled_payment_is_explained_not_sent()
    {
        Seed();
        var payment = SeedPayment(PaymentStatus.Captured);
        var fake = new FakeAnet { Respond = (n, _) => n == "getTransactionDetailsRequest" ? Details("capturedPendingSettlement") : null };

        var result = await Adapter(fake).RefundAsync(payment, 10m, default);

        Assert.False(result.Success);
        Assert.Contains("has not settled", result.ErrorMessage);
        Assert.DoesNotContain(fake.Calls, c => c.Request == "createTransactionRequest");
    }

    [Fact]
    public async Task AN_T7_two_partial_refunds_of_a_settled_payment_both_reference_the_original()
    {
        Seed();
        var payment = SeedPayment(PaymentStatus.Captured, amount: 25m);
        var refundIds = new Queue<string>(new[] { "80001", "80002" });
        var fake = new FakeAnet
        {
            Respond = (n, b) => n switch
            {
                "getTransactionDetailsRequest" => Details("settledSuccessfully"),
                "createTransactionRequest" when TxnType(b) == "refundTransaction" => Approved(refundIds.Dequeue()),
                _ => null,
            },
        };
        var adapter = Adapter(fake);
        var clock = new FixedClock(Now);

        await new RefundPaymentCommandHandler(NewContext(), new AdapterRouter(adapter), clock).Handle(new RefundPaymentCommand(payment.Id, 10m), default);
        await new RefundPaymentCommandHandler(NewContext(), new AdapterRouter(adapter), clock).Handle(new RefundPaymentCommand(payment.Id, 15m), default);

        var refunds = fake.Calls.Where(c => TxnType(c.Body) == "refundTransaction").Select(c => c.Body.GetProperty("transactionRequest")).ToList();
        Assert.Equal(2, refunds.Count);
        Assert.All(refunds, r => Assert.Equal("60001", r.GetProperty("refTransId").GetString()));
        Assert.All(refunds, r => Assert.Equal("1111", r.GetProperty("payment").GetProperty("creditCard").GetProperty("cardNumber").GetString()));
        Assert.All(refunds, r => Assert.Equal("XXXX", r.GetProperty("payment").GetProperty("creditCard").GetProperty("expirationDate").GetString()));

        var saved = Reload(payment.Id);
        Assert.Equal(PaymentStatus.Refunded, saved.Status);
        Assert.Equal(25m, saved.RefundedAmount);
        Assert.Equal("60001", saved.TransactionId); // never overwritten by a refund id
        Assert.Equal(new[] { "80001", "80002" }, saved.Transactions.OrderBy(t => t.Amount).Select(t => t.TransactionId).ToArray());
        Assert.All(saved.Transactions, t => Assert.Equal(PaymentTransactionType.Refund, t.Type));
    }

    [Fact]
    public async Task AN_27_a_failed_refund_throws_with_the_gateway_reason_and_records_the_attempt()
    {
        Seed();
        var payment = SeedPayment(PaymentStatus.Captured);
        var fake = new FakeAnet
        {
            Respond = (n, b) => n switch
            {
                "getTransactionDetailsRequest" => Details("settledSuccessfully"),
                "createTransactionRequest" => Declined("55", "The sum of credits against the referenced transaction would exceed original debit amount."),
                _ => null,
            },
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new RefundPaymentCommandHandler(NewContext(), new AdapterRouter(Adapter(fake)), new FixedClock(Now))
                .Handle(new RefundPaymentCommand(payment.Id, 5m), default));

        Assert.Contains("exceed original debit", ex.Message);
        var saved = Reload(payment.Id);
        Assert.Equal(PaymentStatus.Captured, saved.Status);
        Assert.Equal(0m, saved.RefundedAmount);
        var row = Assert.Single(saved.Transactions);
        Assert.False(row.Success);
        Assert.Equal("55", row.ReasonCode);
    }

    [Fact]
    public async Task AN_T3_capture_is_bounded_by_the_authorized_amount_not_the_order_total()
    {
        Seed();
        // $25 order, $10 gift certificate → $15 authorized.
        var payment = SeedPayment(PaymentStatus.Authorized, amount: 15m);
        var fake = new FakeAnet();
        var handler = () => new CapturePaymentCommandHandler(NewContext(), new AdapterRouter(Adapter(fake)), new FixedClock(Now));

        await Assert.ThrowsAsync<ConflictException>(() => handler().Handle(new CapturePaymentCommand(payment.Id, 25m), default));
        Assert.Empty(fake.Calls);

        await handler().Handle(new CapturePaymentCommand(payment.Id), default);
        var t = fake.Calls.Single().Body.GetProperty("transactionRequest");
        Assert.Equal("priorAuthCaptureTransaction", t.GetProperty("transactionType").GetString());
        Assert.Equal("15.00", t.GetProperty("amount").GetString());
        Assert.Equal("60001", t.GetProperty("refTransId").GetString());
        Assert.Equal(PaymentStatus.Captured, Reload(payment.Id).Status);
    }

    [Fact]
    public async Task AN_16_status_sync_releases_an_approved_held_payment()
    {
        Seed();
        var payment = SeedPayment(PaymentStatus.HeldForReview);
        using (var db = NewContext())
        {
            var order = db.Orders.Single(o => o.Id == payment.OrderId);
            order.Status = OrderStatus.PaymentReview;
            db.SaveChanges();
        }
        var fake = new FakeAnet { Respond = (n, _) => n == "getTransactionDetailsRequest" ? Details("capturedPendingSettlement") : null };

        var dto = await new SyncPaymentStatusCommandHandler(NewContext(), new AdapterRouter(Adapter(fake)), new FixedClock(Now), new NoopInventory())
            .Handle(new SyncPaymentStatusCommand(payment.Id), default);

        Assert.Equal(PaymentStatus.Captured, dto.Status);
        using var check = NewContext();
        var saved = check.Orders.Single(o => o.Id == payment.OrderId);
        Assert.Equal(OrderStatus.Pending, saved.Status);
        Assert.Equal(PaymentStatus.Captured, saved.PaymentStatus);
    }

    // ------------------------------------------------------------------ admin settings

    [Fact]
    public async Task AN_T11_secrets_are_never_returned_and_blank_keeps_the_stored_key()
    {
        var c = Seed(x => x.SignatureKey = "sig-abcd");
        var dto = await new SaveAuthorizeNetCredentialCommandHandler(NewContext()).Handle(new SaveAuthorizeNetCredentialCommand(
            c.Id, "Sandbox", true, false, c.BaseUrl, "login", TransactionKey: null, SignatureKey: "  ", PublicClientKey: "client"), default);

        Assert.Null(dto.TransactionKey);
        Assert.Null(dto.SignatureKey);
        Assert.Equal("••••5678", dto.TransactionKeyMasked);
        Assert.Equal("••••abcd", dto.SignatureKeyMasked);
        using var db = NewContext();
        var stored = db.AuthorizeNetCredentials.AsNoTracking().Single(x => x.Id == c.Id);
        Assert.Equal("tk-secret-5678", stored.TransactionKey);
        Assert.Equal("sig-abcd", stored.SignatureKey);
    }

    [Fact]
    public async Task Saving_with_a_blank_key_and_none_readable_stored_asks_for_the_key()
    {
        // A key encrypted under another machine's key ring reads back as null, the same as never set.
        var c = Seed(x => x.TransactionKey = null);
        var ex = await Assert.ThrowsAsync<HKPartsEcommerce.Application.Common.Exceptions.ValidationException>(() =>
            new SaveAuthorizeNetCredentialCommandHandler(NewContext()).Handle(new SaveAuthorizeNetCredentialCommand(
                c.Id, "Sandbox", true, false, c.BaseUrl, "login", TransactionKey: null, SignatureKey: null, PublicClientKey: "client"), default));
        Assert.Contains("TransactionKey", ex.Errors.Keys);

        // Entering it saves.
        var dto = await new SaveAuthorizeNetCredentialCommandHandler(NewContext()).Handle(new SaveAuthorizeNetCredentialCommand(
            c.Id, "Sandbox", true, false, c.BaseUrl, "login", TransactionKey: "new-key-9999", SignatureKey: null, PublicClientKey: "client"), default);
        Assert.Equal("••••9999", dto.TransactionKeyMasked);
    }

    [Fact]
    public void Transaction_key_is_required_only_when_creating()
    {
        var v = new SaveAuthorizeNetCredentialCommandValidator();
        var create = new SaveAuthorizeNetCredentialCommand(null, "n", true, false, "https://apitest.authorize.net/xml/v1/request.api", "login", null, null, "client");
        Assert.Contains(v.Validate(create).Errors, e => e.PropertyName == "TransactionKey");
        Assert.DoesNotContain(v.Validate(create with { Id = Guid.NewGuid() }).Errors, e => e.PropertyName == "TransactionKey");
    }

    [Fact]
    public async Task AN_41_test_button_reports_connected()
    {
        var c = Seed();
        var fake = new FakeAnet();
        var result = await Adapter(fake).TestCredentialAsync(c.Id, default);
        Assert.True(result.Connected);
        Assert.Equal("Sandbox", result.Environment);
        Assert.Equal("authenticateTestRequest", fake.Calls.Single().Request);
    }

    private sealed class NoopInventory : IInventoryService
    {
        public Task<bool> DecrementStockAsync(Guid productId, Guid? variantId, Guid storeId, int quantity, CancellationToken ct = default) => Task.FromResult(true);
        public Task RestoreStockAsync(Guid productId, Guid? variantId, Guid storeId, int quantity, CancellationToken ct = default) => Task.CompletedTask;
        public Task MarkAsReceivedAsync(Guid productId, Guid? variantId, Guid storeId, int quantityAccepted, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> SetStockLevelAsync(Guid productId, Guid? variantId, Guid storeId, int quantity, int? lowStockThreshold, CancellationToken ct = default) => Task.FromResult(quantity);
        public Task<int> AdjustStockAsync(Guid productId, Guid? variantId, Guid storeId, int delta, CancellationToken ct = default) => Task.FromResult(delta);
        public Task<bool> IsAvailableAsync(Guid productId, Guid? variantId, Guid storeId, int quantity, CancellationToken ct = default) => Task.FromResult(true);
        public Task DecrementForOrderAsync(Order order, CancellationToken ct = default) => Task.CompletedTask;
        public Task RestoreForOrderAsync(Order order, IReadOnlyCollection<Guid>? orderLineItemIds = null, CancellationToken ct = default) => Task.CompletedTask;
    }
}
