using System.Globalization;
using System.Text.Json;
using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>
/// Guest checkout settings (docs/guest-checkout-requirements.md §4.1, A-01..A-05). Stored as
/// <c>Checkout.Guest.*</c> platform settings, so every save is cached and written to the settings change
/// history with admin, time, old and new value (A-06). Each value falls back to its default until first saved.
/// </summary>
public class GuestCheckoutSettingsDto
{
    /// <summary>A-01: offer "Checkout as Guest". Off = buyers must sign in or create an account.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>A-03: payment methods guests may use (<see cref="PaymentMethodType"/> names). Empty = all enabled methods.</summary>
    public List<string> AllowedPaymentMethods { get; set; } = new();

    /// <summary>A-04: the largest order total a guest may place. Null = no limit.</summary>
    public decimal? MaxOrderTotal { get; set; }

    /// <summary>A-05: days a signed "View your order" link in an email stays valid.</summary>
    public int LinkLifetimeDays { get; set; } = GuestCheckoutSettingsStore.DefaultLinkDays;

    /// <summary>U-71 / N-07: when an account verifies its email, move that email's guest orders onto it. Off = an admin links them (A-18).</summary>
    public bool AutoLinkOrdersOnVerify { get; set; } = true;
}

public static class GuestCheckoutSettingsStore
{
    public const string EnabledKey = "Checkout.Guest.Enabled";
    public const string PaymentMethodsKey = "Checkout.Guest.PaymentMethods";
    public const string MaxTotalKey = "Checkout.Guest.MaxTotal";
    public const string LinkDaysKey = "Checkout.Guest.LinkDays";
    public const string AutoLinkKey = "Checkout.Guest.AutoLinkOnVerify";

    public const int DefaultLinkDays = 30;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<GuestCheckoutSettingsDto> LoadAsync(ISettingsService? settings, CancellationToken ct)
    {
        var dto = new GuestCheckoutSettingsDto();
        if (settings is null)
            return dto;

        if (bool.TryParse(await settings.GetValueAsync(EnabledKey, ct), out var enabled)) dto.Enabled = enabled;
        if (await settings.GetValueAsync(PaymentMethodsKey, ct) is { Length: > 0 } methods)
        {
            try { dto.AllowedPaymentMethods = JsonSerializer.Deserialize<List<string>>(methods, Json) ?? new(); }
            catch (JsonException) { dto.AllowedPaymentMethods = new(); }
        }
        if (decimal.TryParse(await settings.GetValueAsync(MaxTotalKey, ct), NumberStyles.Number, CultureInfo.InvariantCulture, out var max) && max > 0)
            dto.MaxOrderTotal = max;
        if (int.TryParse(await settings.GetValueAsync(LinkDaysKey, ct), NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) && days > 0)
            dto.LinkLifetimeDays = days;
        if (bool.TryParse(await settings.GetValueAsync(AutoLinkKey, ct), out var autoLink)) dto.AutoLinkOrdersOnVerify = autoLink;
        return dto;
    }

    public static async Task SaveAsync(ISettingsService settings, GuestCheckoutSettingsDto s, CancellationToken ct)
    {
        await settings.SetAsync(EnabledKey, s.Enabled ? "true" : "false", ct);
        await settings.SetAsync(PaymentMethodsKey, JsonSerializer.Serialize(s.AllowedPaymentMethods, Json), ct);
        await settings.SetAsync(MaxTotalKey, s.MaxOrderTotal is decimal m ? m.ToString(CultureInfo.InvariantCulture) : string.Empty, ct);
        await settings.SetAsync(LinkDaysKey, s.LinkLifetimeDays.ToString(CultureInfo.InvariantCulture), ct);
        await settings.SetAsync(AutoLinkKey, s.AutoLinkOrdersOnVerify ? "true" : "false", ct);
    }

    /// <summary>A-03: whether <paramref name="method"/> is open to guests. The no-gateway paths always are.</summary>
    public static bool AllowsMethod(GuestCheckoutSettingsDto s, PaymentMethodType method) =>
        s.AllowedPaymentMethods.Count == 0
        || method is PaymentMethodType.GiftCertificate or PaymentMethodType.NoPaymentRequired
        || s.AllowedPaymentMethods.Contains(method.ToString(), StringComparer.OrdinalIgnoreCase);
}

/// <summary>Machine-readable reasons guest checkout is unavailable (U-05), sent on the quote and with the 401.</summary>
public static class GuestBlockReasons
{
    public const string Disabled = "GUEST_DISABLED";
    public const string AccountRequiredItem = "ACCOUNT_REQUIRED_ITEM";
    public const string OverLimit = "GUEST_OVER_LIMIT";
    public const string StorePolicy = "STORE_REQUIRES_ACCOUNT";

    public static string Message(string code, decimal? limit = null) => code switch
    {
        Disabled => "Guest checkout is not available right now. Please sign in or create an account to continue.",
        AccountRequiredItem => "An item in your cart needs an account to buy. Please sign in or create an account to continue.",
        OverLimit => limit is decimal l
            ? $"Orders over {l.ToString("0.00", CultureInfo.InvariantCulture)} need an account. Please sign in or create an account to continue."
            : "This order needs an account. Please sign in or create an account to continue.",
        _ => "Guest checkout is not available for this order. Please log in or create an account to continue.",
    };
}

public record GetGuestCheckoutSettingsQuery : IRequest<GuestCheckoutSettingsDto>;

public class GetGuestCheckoutSettingsQueryHandler : IRequestHandler<GetGuestCheckoutSettingsQuery, GuestCheckoutSettingsDto>
{
    private readonly ISettingsService _settings;
    public GetGuestCheckoutSettingsQueryHandler(ISettingsService settings) => _settings = settings;

    public Task<GuestCheckoutSettingsDto> Handle(GetGuestCheckoutSettingsQuery request, CancellationToken ct) =>
        GuestCheckoutSettingsStore.LoadAsync(_settings, ct);
}

public record UpdateGuestCheckoutSettingsCommand(
    bool Enabled,
    List<string>? AllowedPaymentMethods,
    decimal? MaxOrderTotal,
    int LinkLifetimeDays,
    bool AutoLinkOrdersOnVerify) : IRequest<GuestCheckoutSettingsDto>;

public class UpdateGuestCheckoutSettingsCommandValidator : AbstractValidator<UpdateGuestCheckoutSettingsCommand>
{
    public UpdateGuestCheckoutSettingsCommandValidator()
    {
        RuleFor(x => x.MaxOrderTotal).GreaterThan(0).When(x => x.MaxOrderTotal is not null)
            .WithMessage("Leave the guest order limit empty for no limit, or enter an amount above 0.");
        RuleFor(x => x.LinkLifetimeDays).InclusiveBetween(1, 365);
        RuleForEach(x => x.AllowedPaymentMethods)
            .Must(m => Enum.TryParse<PaymentMethodType>(m, true, out _))
            .WithMessage("'{PropertyValue}' is not a payment method.");
    }
}

public class UpdateGuestCheckoutSettingsCommandHandler : IRequestHandler<UpdateGuestCheckoutSettingsCommand, GuestCheckoutSettingsDto>
{
    private readonly ISettingsService _settings;
    public UpdateGuestCheckoutSettingsCommandHandler(ISettingsService settings) => _settings = settings;

    public async Task<GuestCheckoutSettingsDto> Handle(UpdateGuestCheckoutSettingsCommand r, CancellationToken ct)
    {
        await GuestCheckoutSettingsStore.SaveAsync(_settings, new GuestCheckoutSettingsDto
        {
            Enabled = r.Enabled,
            AllowedPaymentMethods = (r.AllowedPaymentMethods ?? new())
                .Select(m => Enum.Parse<PaymentMethodType>(m, true).ToString())
                .Distinct()
                .ToList(),
            MaxOrderTotal = r.MaxOrderTotal,
            LinkLifetimeDays = r.LinkLifetimeDays,
            AutoLinkOrdersOnVerify = r.AutoLinkOrdersOnVerify,
        }, ct);
        return await GuestCheckoutSettingsStore.LoadAsync(_settings, ct);
    }
}
