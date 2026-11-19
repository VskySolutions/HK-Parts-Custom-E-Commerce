using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.AddressVerification;
using HKPartsEcommerce.Application.Features.Shipping;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.AddressVerification;

/// <summary>One address-verification backend (§6.2): USPS Addresses API or Avalara AvaTax v2.</summary>
public interface IAddressVerificationProvider
{
    AddressVerificationProvider Provider { get; }

    Task<bool> IsConfiguredAsync(CancellationToken ct);

    /// <summary>Never throws for a provider problem: returns <see cref="AddressVerificationStatus.Unavailable"/> (AV-14).</summary>
    Task<AddressVerificationOutcome> VerifyAsync(VerifiableAddress address, bool includeRaw, CancellationToken ct);
}

/// <summary>
/// <see cref="IAddressVerificationService"/>: applies the settings (provider, country list, suggestion), the
/// 30-day result cache keyed by address hash (AV-06), and logging without credentials.
/// </summary>
public class AddressVerificationService : IAddressVerificationService
{
    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<IAddressVerificationProvider> _providers;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<AddressVerificationService> _logger;

    public AddressVerificationService(
        IApplicationDbContext db,
        IEnumerable<IAddressVerificationProvider> providers,
        IDateTimeProvider clock,
        ILogger<AddressVerificationService> logger)
    {
        _db = db;
        _providers = providers;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> IsProviderConfiguredAsync(AddressVerificationProvider provider, CancellationToken ct) =>
        _providers.FirstOrDefault(p => p.Provider == provider) is { } impl && await impl.IsConfiguredAsync(ct);

    public async Task<AddressVerificationOutcome> VerifyAsync(
        VerifiableAddress address,
        CancellationToken ct,
        bool checkoutRules = true,
        AddressVerificationProvider? provider = null,
        bool bypassCache = false,
        bool includeRaw = false)
    {
        var settings = await AddressVerificationSupport.LoadAsync(_db, ct);
        var hash = AddressVerificationSupport.Hash(address);
        var chosen = provider ?? settings.Provider;
        var isPoBox = PoBoxDetector.IsPoBox(address.Line1, address.Line2);

        AddressVerificationOutcome NotChecked() => new()
        {
            Status = AddressVerificationStatus.NotChecked, Provider = chosen, AddressHash = hash, IsPoBox = isPoBox,
        };

        if (chosen == AddressVerificationProvider.None)
            return NotChecked();

        if (checkoutRules)
        {
            // AV-02: only the countries the admin listed. An empty list means nothing is verified (AV-07 stops
            // that being saved, but an old row could still hold it).
            var countries = ShippingRulesEngine.SplitCodes(settings.Countries);
            var country = (address.CountryCode ?? string.Empty).Trim().ToUpperInvariant();
            if (!countries.Contains(country))
                return NotChecked();
            // USPS checks US addresses only (AV-20/25).
            if (chosen == AddressVerificationProvider.Usps && country != "US")
                return NotChecked();
        }

        var impl = _providers.FirstOrDefault(p => p.Provider == chosen);
        if (impl is null)
            return NotChecked();

        if (!bypassCache)
        {
            var since = _clock.UtcNow - AddressVerificationSupport.CacheLifetime;
            var cached = await _db.AddressVerificationResults.AsNoTracking()
                .Where(r => r.AddressHash == hash && r.Provider == chosen && r.CreatedOnUtc >= since)
                .OrderByDescending(r => r.CreatedOnUtc)
                .FirstOrDefaultAsync(ct);
            if (cached is not null)
                return Apply(settings, new AddressVerificationOutcome
                {
                    Status = cached.Status,
                    Provider = cached.Provider,
                    Suggestion = AddressVerificationSupport.DeserializeAddress(cached.SuggestedJson),
                    Messages = SplitMessages(cached.Messages),
                    IsPoBox = cached.IsPoBox || isPoBox,
                    IsResidential = cached.IsResidential,
                    AddressHash = hash,
                    FromCache = true,
                });
        }

        var stopwatch = Stopwatch.StartNew();
        AddressVerificationOutcome outcome;
        try
        {
            outcome = await impl.VerifyAsync(address, includeRaw, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // AV-14: a provider bug or network failure never breaks checkout.
            _logger.LogWarning(ex, "Address verification ({Provider}) failed; treating the address as not verified.", chosen);
            outcome = new AddressVerificationOutcome
            {
                Status = AddressVerificationStatus.Unavailable,
                Messages = { "The address verification service could not be reached." },
            };
        }
        stopwatch.Stop();

        outcome.Provider = chosen;
        outcome.AddressHash = hash;
        outcome.IsPoBox = outcome.IsPoBox || isPoBox;

        if (settings.LoggingEnabled || outcome.Status == AddressVerificationStatus.Unavailable)
            _logger.LogInformation(
                "Address verification {Provider}: {Status} for address {AddressHash} in {ElapsedMs} ms.",
                chosen, outcome.Status, hash[..12], stopwatch.ElapsedMilliseconds);

        // Unavailable is not cached, so the next attempt asks the provider again. The admin test (bypassCache)
        // writes nothing either.
        if (!bypassCache && outcome.Status != AddressVerificationStatus.Unavailable)
            await CacheAsync(hash, outcome, ct);

        return Apply(settings, outcome);
    }

    /// <summary>With "Offer suggested address" off, a corrected address is simply accepted (AV-13 is optional).</summary>
    private static AddressVerificationOutcome Apply(AddressVerificationSettings settings, AddressVerificationOutcome outcome)
    {
        if (outcome.Status == AddressVerificationStatus.Suggested && !settings.OfferSuggestion)
            outcome.Status = AddressVerificationStatus.Valid;
        return outcome;
    }

    private async Task CacheAsync(string hash, AddressVerificationOutcome outcome, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        try
        {
            // NF-03: addresses are kept at most 30 days.
            var cutoff = now - AddressVerificationSupport.CacheLifetime;
            await _db.AddressVerificationResults.Where(r => r.CreatedOnUtc < cutoff).ExecuteDeleteAsync(ct);
        }
        catch (InvalidOperationException)
        {
            // In-memory providers (tests) do not support ExecuteDelete; the purge is housekeeping only.
        }

        _db.AddressVerificationResults.Add(new AddressVerificationResult
        {
            AddressHash = hash,
            Provider = outcome.Provider,
            Status = outcome.Status,
            IsPoBox = outcome.IsPoBox,
            IsResidential = outcome.IsResidential,
            SuggestedJson = Truncate(AddressVerificationSupport.SerializeAddress(outcome.Suggestion), 2000),
            Messages = Truncate(outcome.Messages.Count > 0 ? string.Join("\n", outcome.Messages) : null, 2000),
            CreatedOnUtc = now,
        });
        await _db.SaveChangesAsync(ct);
    }

    private static List<string> SplitMessages(string? messages) =>
        string.IsNullOrWhiteSpace(messages)
            ? new List<string>()
            : messages.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
