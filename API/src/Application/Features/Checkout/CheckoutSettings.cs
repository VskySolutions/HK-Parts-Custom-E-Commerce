using System.Globalization;
using System.Text.Json;
using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>
/// The checkout settings that the legacy site hard-coded (requirements section 7, "Checkout Settings"):
/// session timeout, the low-stock checkout lock, the terms checkbox, per-state notices and the blacklist.
/// Stored as <c>Checkout.*</c> platform settings, so every save is cached, flushed and written to the
/// settings change history (AS-01, AS-04). Each value falls back to the legacy default until first saved.
/// </summary>
public class CheckoutSettingsDto
{
    /// <summary>CK-02: seconds of checkout inactivity before placement is refused. 0 = never expires.</summary>
    public int SessionTimeoutSeconds { get; set; } = CheckoutSettingsStore.DefaultSessionTimeoutSeconds;

    /// <summary>CK-22: lock low-stock products while a buyer checks them out.</summary>
    public bool LockEnabled { get; set; } = true;

    /// <summary>CK-22: a product locks when its available stock is below this.</summary>
    public int LockQtyThreshold { get; set; } = CheckoutSettingsStore.DefaultLockQtyThreshold;

    /// <summary>CK-22: how long a lock lasts from first acquire. Never extended by a refresh (CK-21).</summary>
    public int LockSeconds { get; set; } = CheckoutSettingsStore.DefaultLockSeconds;

    /// <summary>SH-40 / SH-43: the buyer must tick the terms checkbox before placing the order.</summary>
    public bool TermsRequired { get; set; } = true;
    public string TermsText { get; set; } = CheckoutSettingsStore.DefaultTermsText;
    public string? TermsUrl { get; set; } = CheckoutSettingsStore.DefaultTermsUrl;

    /// <summary>SH-23: notices shown at checkout when the ship-to state matches.</summary>
    public List<CheckoutStateNotice> StateNotices { get; set; } = CheckoutSettingsStore.DefaultStateNotices();

    /// <summary>PY-15: how a blacklist entry matches the buyer.</summary>
    public BlacklistMatchMode BlacklistMatchMode { get; set; } = BlacklistMatchMode.NameOrEmail;

    /// <summary>PY-15: who is alerted when a blocked buyer tries to order (comma separated). Null = the assigned store's notification address.</summary>
    public string? BlacklistAlertEmail { get; set; }
}

/// <summary>One per-state checkout notice (SH-23), e.g. the Washington notice.</summary>
public record CheckoutStateNotice(string State, string Message, bool Active = true);

/// <summary>Reads and writes <see cref="CheckoutSettingsDto"/> through <see cref="ISettingsService"/>.</summary>
public static class CheckoutSettingsStore
{
    public const string SessionTimeoutKey = "Checkout.SessionTimeoutSeconds";
    public const string LockEnabledKey = "Checkout.LockEnabled";
    public const string LockQtyThresholdKey = "Checkout.LockQtyThreshold";
    public const string LockSecondsKey = "Checkout.LockSeconds";
    public const string TermsRequiredKey = "Checkout.TermsRequired";
    public const string TermsTextKey = "Checkout.TermsText";
    public const string TermsUrlKey = "Checkout.TermsUrl";
    public const string StateNoticesKey = "Checkout.StateNotices";
    public const string BlacklistMatchModeKey = "Checkout.BlacklistMatchMode";
    public const string BlacklistAlertEmailKey = "Checkout.BlacklistAlertEmail";

    public const int DefaultSessionTimeoutSeconds = 1800;
    public const int DefaultLockQtyThreshold = 10;
    public const int DefaultLockSeconds = 600;
    public const string DefaultTermsText = "By checking this box, you agree to our terms and conditions";
    public const string DefaultTermsUrl = "/privacy--terms";

    /// <summary>The notice the legacy shipping-method view hard-coded for Washington.</summary>
    public const string DefaultWashingtonNotice =
        "Due to recent changes to laws in Washington state, we cannot ship all types of items to your address. Please ensure the items you are purchasing are in accordance with your local and state laws. If they are not, your order may need to be cancelled.";

    public static List<CheckoutStateNotice> DefaultStateNotices() => new() { new("WA", DefaultWashingtonNotice) };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<CheckoutSettingsDto> LoadAsync(ISettingsService? settings, CancellationToken ct)
    {
        var dto = new CheckoutSettingsDto();
        if (settings is null)
            return dto;

        if (Int(await settings.GetValueAsync(SessionTimeoutKey, ct)) is int timeout && timeout >= 0) dto.SessionTimeoutSeconds = timeout;
        if (Bool(await settings.GetValueAsync(LockEnabledKey, ct)) is bool lockOn) dto.LockEnabled = lockOn;
        if (Int(await settings.GetValueAsync(LockQtyThresholdKey, ct)) is int threshold && threshold > 0) dto.LockQtyThreshold = threshold;
        if (Int(await settings.GetValueAsync(LockSecondsKey, ct)) is int seconds && seconds > 0) dto.LockSeconds = seconds;
        if (Bool(await settings.GetValueAsync(TermsRequiredKey, ct)) is bool terms) dto.TermsRequired = terms;
        if (await settings.GetValueAsync(TermsTextKey, ct) is { Length: > 0 } text) dto.TermsText = text;
        // An empty string is a deliberate "no link"; only a missing key means the default.
        if (await settings.GetValueAsync(TermsUrlKey, ct) is string url) dto.TermsUrl = url.Length == 0 ? null : url;
        if (await settings.GetValueAsync(StateNoticesKey, ct) is string notices)
            dto.StateNotices = ParseNotices(notices);
        if (Enum.TryParse<BlacklistMatchMode>(await settings.GetValueAsync(BlacklistMatchModeKey, ct), true, out var mode)
            && Enum.IsDefined(mode))
            dto.BlacklistMatchMode = mode;
        if (await settings.GetValueAsync(BlacklistAlertEmailKey, ct) is { Length: > 0 } alert) dto.BlacklistAlertEmail = alert;
        return dto;
    }

    public static async Task SaveAsync(ISettingsService settings, CheckoutSettingsDto s, CancellationToken ct)
    {
        await settings.SetAsync(SessionTimeoutKey, s.SessionTimeoutSeconds.ToString(CultureInfo.InvariantCulture), ct);
        await settings.SetAsync(LockEnabledKey, s.LockEnabled ? "true" : "false", ct);
        await settings.SetAsync(LockQtyThresholdKey, s.LockQtyThreshold.ToString(CultureInfo.InvariantCulture), ct);
        await settings.SetAsync(LockSecondsKey, s.LockSeconds.ToString(CultureInfo.InvariantCulture), ct);
        await settings.SetAsync(TermsRequiredKey, s.TermsRequired ? "true" : "false", ct);
        await settings.SetAsync(TermsTextKey, s.TermsText.Trim(), ct);
        await settings.SetAsync(TermsUrlKey, s.TermsUrl?.Trim() ?? string.Empty, ct);
        await settings.SetAsync(StateNoticesKey, JsonSerializer.Serialize(s.StateNotices, Json), ct);
        await settings.SetAsync(BlacklistMatchModeKey, s.BlacklistMatchMode.ToString(), ct);
        await settings.SetAsync(BlacklistAlertEmailKey, s.BlacklistAlertEmail?.Trim() ?? string.Empty, ct);
    }

    /// <summary>The active notice for a ship-to state (case-insensitive two-letter code or full name), or null.</summary>
    public static string? NoticeFor(CheckoutSettingsDto settings, string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
            return null;
        var code = Shipping.ShippingRulesEngine.NormalizeUsState(region);
        return settings.StateNotices
            .FirstOrDefault(n => n.Active && string.Equals(
                Shipping.ShippingRulesEngine.NormalizeUsState(n.State), code, StringComparison.OrdinalIgnoreCase))
            ?.Message;
    }

    internal static List<CheckoutStateNotice> ParseNotices(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new();
        try
        {
            return JsonSerializer.Deserialize<List<CheckoutStateNotice>>(raw, Json)?
                .Where(n => !string.IsNullOrWhiteSpace(n.State) && !string.IsNullOrWhiteSpace(n.Message))
                .ToList() ?? new();
        }
        catch (JsonException)
        {
            return DefaultStateNotices();
        }
    }

    private static int? Int(string? raw) => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    private static bool? Bool(string? raw) => bool.TryParse(raw, out var v) ? v : null;
}

/// <summary>Machine-readable checkout error codes, sent as the <c>code</c> member of the 409 problem body (section 8.2).</summary>
public static class CheckoutErrorCodes
{
    public const string CheckoutExpired = "CHECKOUT_EXPIRED";
    public const string CheckoutLocked = "CHECKOUT_LOCKED";
    public const string TermsRequired = "TERMS_REQUIRED";
    public const string CustomerBlocked = "CUSTOMER_BLOCKED";
    public const string PaymentMethodNotAllowed = "PAYMENT_METHOD_NOT_ALLOWED";
    public const string TotalChanged = "TOTAL_CHANGED";

    public static ConflictException Conflict(string code, string message, IReadOnlyDictionary<string, object?>? extra = null)
    {
        var ext = new Dictionary<string, object?> { ["code"] = code };
        if (extra is not null)
            foreach (var (k, v) in extra) ext[k] = v;
        return new ConflictException(message, ext);
    }
}

public record GetCheckoutSettingsQuery : IRequest<CheckoutSettingsDto>;

public class GetCheckoutSettingsQueryHandler : IRequestHandler<GetCheckoutSettingsQuery, CheckoutSettingsDto>
{
    private readonly ISettingsService _settings;

    public GetCheckoutSettingsQueryHandler(ISettingsService settings) => _settings = settings;

    public Task<CheckoutSettingsDto> Handle(GetCheckoutSettingsQuery request, CancellationToken ct) =>
        CheckoutSettingsStore.LoadAsync(_settings, ct);
}

public record UpdateCheckoutSettingsCommand(
    int SessionTimeoutSeconds,
    bool LockEnabled,
    int LockQtyThreshold,
    int LockSeconds,
    bool TermsRequired,
    string TermsText,
    string? TermsUrl,
    List<CheckoutStateNotice>? StateNotices,
    BlacklistMatchMode BlacklistMatchMode,
    string? BlacklistAlertEmail) : IRequest<CheckoutSettingsDto>;

public class UpdateCheckoutSettingsCommandValidator : AbstractValidator<UpdateCheckoutSettingsCommand>
{
    public UpdateCheckoutSettingsCommandValidator()
    {
        RuleFor(x => x.SessionTimeoutSeconds).InclusiveBetween(0, 86400)
            .WithMessage("Session timeout must be between 0 (never) and 86400 seconds.");
        RuleFor(x => x.LockQtyThreshold).InclusiveBetween(1, 100000);
        RuleFor(x => x.LockSeconds).InclusiveBetween(30, 86400)
            .WithMessage("Lock time must be between 30 and 86400 seconds.");
        RuleFor(x => x.TermsText).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TermsUrl).MaximumLength(500);
        RuleFor(x => x.BlacklistMatchMode).IsInEnum();
        RuleFor(x => x.BlacklistAlertEmail).MaximumLength(500)
            .Must(BeEmailList!).When(x => !string.IsNullOrWhiteSpace(x.BlacklistAlertEmail))
            .WithMessage("Enter one or more email addresses separated by commas.");
        RuleForEach(x => x.StateNotices).ChildRules(n =>
        {
            n.RuleFor(x => x.State).NotEmpty().MaximumLength(30);
            n.RuleFor(x => x.Message).NotEmpty().MaximumLength(1000);
        });
        RuleFor(x => x.StateNotices)
            .Must(n => n == null || n.Select(x => Shipping.ShippingRulesEngine.NormalizeUsState(x.State)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == n.Count)
            .WithMessage("Each state can have only one notice.");
    }

    private static bool BeEmailList(string value) =>
        value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(e => e.Contains('@') && e.IndexOf('@') > 0 && e.IndexOf('@') < e.Length - 1);
}

public class UpdateCheckoutSettingsCommandHandler : IRequestHandler<UpdateCheckoutSettingsCommand, CheckoutSettingsDto>
{
    private readonly ISettingsService _settings;

    public UpdateCheckoutSettingsCommandHandler(ISettingsService settings) => _settings = settings;

    public async Task<CheckoutSettingsDto> Handle(UpdateCheckoutSettingsCommand r, CancellationToken ct)
    {
        await CheckoutSettingsStore.SaveAsync(_settings, new CheckoutSettingsDto
        {
            SessionTimeoutSeconds = r.SessionTimeoutSeconds,
            LockEnabled = r.LockEnabled,
            LockQtyThreshold = r.LockQtyThreshold,
            LockSeconds = r.LockSeconds,
            TermsRequired = r.TermsRequired,
            TermsText = r.TermsText,
            TermsUrl = string.IsNullOrWhiteSpace(r.TermsUrl) ? null : r.TermsUrl,
            StateNotices = (r.StateNotices ?? new())
                .Select(n => n with
                {
                    State = Shipping.ShippingRulesEngine.NormalizeUsState(n.State) ?? n.State.Trim(),
                    Message = n.Message.Trim(),
                })
                .ToList(),
            BlacklistMatchMode = r.BlacklistMatchMode,
            BlacklistAlertEmail = string.IsNullOrWhiteSpace(r.BlacklistAlertEmail) ? null : r.BlacklistAlertEmail,
        }, ct);
        return await CheckoutSettingsStore.LoadAsync(_settings, ct);
    }
}
