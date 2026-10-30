using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Commerce;

/// <summary>
/// Reads the tenant's commerce mode from the <c>commerce.*</c> platform settings (REQ-INQ-001). Values are
/// served through <see cref="ISettingsService"/>, which memoizes them, so this can be called freely on the
/// hot checkout path. Anything missing or unparseable falls back to the Standard defaults — an unconfigured
/// install keeps taking payments rather than silently going quote-only.
/// </summary>
public class CommerceModeService : ICommerceModeService
{
    private readonly ISettingsService _settings;

    public CommerceModeService(ISettingsService settings) => _settings = settings;

    public async Task<CommerceModeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var defaults = CommerceModeSettings.Default;

        var rawMode = await _settings.GetValueAsync(CommerceSettingKeys.Mode, cancellationToken);
        var mode = Enum.TryParse<CommerceMode>(rawMode, ignoreCase: true, out var parsed)
            ? parsed
            : defaults.Mode;

        var showPrices = await BoolAsync(CommerceSettingKeys.ShowPrices, defaults.ShowPrices, cancellationToken);
        var collectAddress = await BoolAsync(CommerceSettingKeys.CollectAddress, defaults.CollectAddress, cancellationToken);

        var label = await _settings.GetValueAsync(CommerceSettingKeys.ButtonLabel, cancellationToken);
        if (string.IsNullOrWhiteSpace(label))
            label = defaults.InquiryButtonLabel;

        var rawStore = await _settings.GetValueAsync(CommerceSettingKeys.DefaultStoreId, cancellationToken);
        Guid? defaultStoreId = Guid.TryParse(rawStore, out var storeId) && storeId != Guid.Empty ? storeId : null;

        var notify = await _settings.GetValueAsync(CommerceSettingKeys.NotifyEmails, cancellationToken);
        var note = await _settings.GetValueAsync(CommerceSettingKeys.SubmitNote, cancellationToken);

        return new CommerceModeSettings(
            mode, showPrices, collectAddress, label.Trim(), defaultStoreId,
            string.IsNullOrWhiteSpace(notify) ? null : notify.Trim(),
            string.IsNullOrWhiteSpace(note) ? null : note.Trim());
    }

    public async Task<bool> IsInquiryOnlyAsync(CancellationToken cancellationToken = default)
        => (await GetAsync(cancellationToken)).IsInquiryOnly;

    /// <summary>A bool setting, tolerant of a blank/garbage value (falls back rather than throwing).</summary>
    private async Task<bool> BoolAsync(string key, bool fallback, CancellationToken ct)
    {
        var raw = await _settings.GetValueAsync(key, ct);
        return bool.TryParse(raw, out var value) ? value : fallback;
    }
}
