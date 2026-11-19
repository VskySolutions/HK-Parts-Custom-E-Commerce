using System.Globalization;
using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Ffl;

/// <summary>
/// FFL shipping settings, stored as <c>Ffl.*</c> platform settings (audited and cached like the checkout
/// settings). Each value falls back to the legacy behaviour until first saved.
/// </summary>
public class FflSettingsDto
{
    /// <summary>FFL-01 (legacy FIREARMS_ENABLED): when off, FFL-required products check out like any other product.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>§4 #2: intro text above the dealer search (legacy CMS block <c>ffl-shipping-text</c>).</summary>
    public string IntroText { get; set; } = FflSettingsStore.DefaultIntroText;

    /// <summary>§4 #9 / FFL-48: red warning under the selected dealer.</summary>
    public string TransferWarning { get; set; } = FflSettingsStore.DefaultTransferWarning;

    /// <summary>FFL-36: radius options (miles) at checkout; the first is the default.</summary>
    public List<int> CheckoutRadii { get; set; } = FflSettingsStore.DefaultCheckoutRadii();

    /// <summary>FFL-100: radius options (miles) on the public dealer finder.</summary>
    public List<int> PublicRadii { get; set; } = FflSettingsStore.DefaultPublicRadii();

    /// <summary>FFL-39: most dealers returned by one search.</summary>
    public int MaxResults { get; set; } = FflSettingsStore.DefaultMaxResults;

    /// <summary>FFL-25: pre-select the dealer of the signed-in buyer's last FFL order.</summary>
    public bool RememberLastDealer { get; set; } = true;

    /// <summary>Geocodio API key used to geocode ZIP codes (FFL-32). Never returned; see <see cref="GeocodioConfigured"/>.</summary>
    public string? GeocodioApiKey { get; set; }

    public bool GeocodioConfigured { get; set; }
}

/// <summary>What the storefront needs to render the FFL panel and the public finder.</summary>
public class FflPublicConfigDto
{
    public bool Enabled { get; set; }
    public string IntroText { get; set; } = string.Empty;
    public string TransferWarning { get; set; } = string.Empty;
    public List<int> CheckoutRadii { get; set; } = new();
    public List<int> PublicRadii { get; set; } = new();
}

public static class FflSettingsStore
{
    public const string EnabledKey = "Ffl.Enabled";
    public const string IntroTextKey = "Ffl.IntroText";
    public const string TransferWarningKey = "Ffl.TransferWarning";
    public const string CheckoutRadiiKey = "Ffl.CheckoutRadii";
    public const string PublicRadiiKey = "Ffl.PublicRadii";
    public const string MaxResultsKey = "Ffl.MaxResults";
    public const string RememberLastDealerKey = "Ffl.RememberLastDealer";
    public const string GeocodioApiKeyKey = "Ffl.GeocodioApiKey";

    public const int DefaultMaxResults = 200;

    public const string DefaultIntroText =
        "Firearms must be shipped to a licensed dealer (FFL). Search for a dealer near you and select one from the list. " +
        "Your dealer will contact you to pick up your firearms once they have been delivered.";

    public const string DefaultTransferWarning =
        "Please ensure your selected FFL accepts private firearm transfers before ordering. Once you place your order, " +
        "please contact your selected FFL and request that they send us a copy of their FFL license. " +
        "We cannot ship your order until we receive a copy of your selected dealer's license.";

    public static List<int> DefaultCheckoutRadii() => new() { 15, 25, 50, 100, 150 };
    public static List<int> DefaultPublicRadii() => new() { 5, 10, 15, 25, 50 };

    public static async Task<FflSettingsDto> LoadAsync(ISettingsService? settings, CancellationToken ct)
    {
        var dto = new FflSettingsDto();
        if (settings is null)
            return dto;

        if (bool.TryParse(await settings.GetValueAsync(EnabledKey, ct), out var enabled)) dto.Enabled = enabled;
        if (await settings.GetValueAsync(IntroTextKey, ct) is { Length: > 0 } intro) dto.IntroText = intro;
        if (await settings.GetValueAsync(TransferWarningKey, ct) is string warning) dto.TransferWarning = warning;
        if (ParseRadii(await settings.GetValueAsync(CheckoutRadiiKey, ct)) is { Count: > 0 } checkout) dto.CheckoutRadii = checkout;
        if (ParseRadii(await settings.GetValueAsync(PublicRadiiKey, ct)) is { Count: > 0 } pub) dto.PublicRadii = pub;
        if (int.TryParse(await settings.GetValueAsync(MaxResultsKey, ct), NumberStyles.Integer, CultureInfo.InvariantCulture, out var max)
            && max is > 0 and <= 5000)
            dto.MaxResults = max;
        if (bool.TryParse(await settings.GetValueAsync(RememberLastDealerKey, ct), out var remember)) dto.RememberLastDealer = remember;
        dto.GeocodioApiKey = await settings.GetValueAsync(GeocodioApiKeyKey, ct) is { Length: > 0 } key ? key : null;
        dto.GeocodioConfigured = dto.GeocodioApiKey is not null;
        return dto;
    }

    /// <summary>"15,25,50" → [15, 25, 50] (distinct, 1–500, in the order given).</summary>
    public static List<int> ParseRadii(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? new()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .Where(n => n is > 0 and <= 500)
                .Distinct()
                .ToList();

    public static string FormatRadii(IEnumerable<int> radii) =>
        string.Join(",", radii.Select(r => r.ToString(CultureInfo.InvariantCulture)));

    public static FflSettingsDto Redact(FflSettingsDto dto)
    {
        dto.GeocodioApiKey = null;
        return dto;
    }
}

public record GetFflSettingsQuery : IRequest<FflSettingsDto>;

public class GetFflSettingsQueryHandler : IRequestHandler<GetFflSettingsQuery, FflSettingsDto>
{
    private readonly ISettingsService _settings;
    public GetFflSettingsQueryHandler(ISettingsService settings) => _settings = settings;

    public async Task<FflSettingsDto> Handle(GetFflSettingsQuery request, CancellationToken ct) =>
        FflSettingsStore.Redact(await FflSettingsStore.LoadAsync(_settings, ct));
}

/// <param name="GeocodioApiKey">A new key to store; null/empty keeps the current one.</param>
/// <param name="ClearGeocodioApiKey">Remove the stored key.</param>
public record UpdateFflSettingsCommand(
    bool Enabled,
    string IntroText,
    string? TransferWarning,
    List<int> CheckoutRadii,
    List<int> PublicRadii,
    int MaxResults,
    bool RememberLastDealer,
    string? GeocodioApiKey = null,
    bool ClearGeocodioApiKey = false) : IRequest<FflSettingsDto>;

public class UpdateFflSettingsCommandValidator : AbstractValidator<UpdateFflSettingsCommand>
{
    public UpdateFflSettingsCommandValidator()
    {
        RuleFor(x => x.IntroText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.TransferWarning).MaximumLength(2000);
        RuleFor(x => x.CheckoutRadii).NotEmpty().Must(BeValidRadii).WithMessage("Enter one or more radius values between 1 and 500 miles.");
        RuleFor(x => x.PublicRadii).NotEmpty().Must(BeValidRadii).WithMessage("Enter one or more radius values between 1 and 500 miles.");
        RuleFor(x => x.MaxResults).InclusiveBetween(10, 5000);
        RuleFor(x => x.GeocodioApiKey).MaximumLength(200);
    }

    private static bool BeValidRadii(List<int>? radii) =>
        radii is { Count: > 0 and <= 10 } && radii.All(r => r is > 0 and <= 500) && radii.Distinct().Count() == radii.Count;
}

public class UpdateFflSettingsCommandHandler : IRequestHandler<UpdateFflSettingsCommand, FflSettingsDto>
{
    private readonly ISettingsService _settings;
    public UpdateFflSettingsCommandHandler(ISettingsService settings) => _settings = settings;

    public async Task<FflSettingsDto> Handle(UpdateFflSettingsCommand r, CancellationToken ct)
    {
        await _settings.SetAsync(FflSettingsStore.EnabledKey, r.Enabled ? "true" : "false", ct);
        await _settings.SetAsync(FflSettingsStore.IntroTextKey, r.IntroText.Trim(), ct);
        await _settings.SetAsync(FflSettingsStore.TransferWarningKey, r.TransferWarning?.Trim() ?? string.Empty, ct);
        await _settings.SetAsync(FflSettingsStore.CheckoutRadiiKey, FflSettingsStore.FormatRadii(r.CheckoutRadii), ct);
        await _settings.SetAsync(FflSettingsStore.PublicRadiiKey, FflSettingsStore.FormatRadii(r.PublicRadii), ct);
        await _settings.SetAsync(FflSettingsStore.MaxResultsKey, r.MaxResults.ToString(CultureInfo.InvariantCulture), ct);
        await _settings.SetAsync(FflSettingsStore.RememberLastDealerKey, r.RememberLastDealer ? "true" : "false", ct);
        if (r.ClearGeocodioApiKey)
            await _settings.SetAsync(FflSettingsStore.GeocodioApiKeyKey, string.Empty, ct);
        else if (!string.IsNullOrWhiteSpace(r.GeocodioApiKey))
            await _settings.SetAsync(FflSettingsStore.GeocodioApiKeyKey, r.GeocodioApiKey.Trim(), ct);

        return FflSettingsStore.Redact(await FflSettingsStore.LoadAsync(_settings, ct));
    }
}

public record GetFflPublicConfigQuery : IRequest<FflPublicConfigDto>;

public class GetFflPublicConfigQueryHandler : IRequestHandler<GetFflPublicConfigQuery, FflPublicConfigDto>
{
    private readonly ISettingsService _settings;
    public GetFflPublicConfigQueryHandler(ISettingsService settings) => _settings = settings;

    public async Task<FflPublicConfigDto> Handle(GetFflPublicConfigQuery request, CancellationToken ct)
    {
        var s = await FflSettingsStore.LoadAsync(_settings, ct);
        return new FflPublicConfigDto
        {
            Enabled = s.Enabled,
            IntroText = s.IntroText,
            TransferWarning = s.TransferWarning,
            CheckoutRadii = s.CheckoutRadii,
            PublicRadii = s.PublicRadii,
        };
    }
}
