using System.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Shipping;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.AddressVerification;

/// <summary>The Address Verification screen (§5.2): the settings, the P.O. box rule and whether each provider is configured.</summary>
public record GetAddressVerificationSettingsQuery : IRequest<AddressVerificationSettingsDto>;

public class GetAddressVerificationSettingsQueryHandler
    : IRequestHandler<GetAddressVerificationSettingsQuery, AddressVerificationSettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IAddressVerificationService _verifier;

    public GetAddressVerificationSettingsQueryHandler(IApplicationDbContext db, IAddressVerificationService verifier)
    {
        _db = db;
        _verifier = verifier;
    }

    public async Task<AddressVerificationSettingsDto> Handle(GetAddressVerificationSettingsQuery request, CancellationToken ct)
    {
        var dto = AddressVerificationSettingsDto.From(
            await AddressVerificationSupport.LoadAsync(_db, ct), await ShippingRulesEngine.LoadAsync(_db, ct));
        dto.ProviderConfigured = await ProviderStatusAsync(_verifier, ct);
        return dto;
    }

    internal static async Task<Dictionary<string, bool>> ProviderStatusAsync(IAddressVerificationService verifier, CancellationToken ct) => new()
    {
        [nameof(AddressVerificationProvider.Avalara)] = await verifier.IsProviderConfiguredAsync(AddressVerificationProvider.Avalara, ct),
        [nameof(AddressVerificationProvider.Usps)] = await verifier.IsProviderConfiguredAsync(AddressVerificationProvider.Usps, ct),
    };
}

/// <summary>
/// Saves the Address Verification screen. Refuses an enabled provider with no countries (AV-07) and USPS with
/// a non-US country (AV-25: USPS checks US addresses only). The P.O. box rule is written to the shipping rules,
/// which the checkout destination check reads, and bumps their version like any rules change.
/// The save is recorded by the admin audit trail (AD-60).
/// </summary>
public record UpdateAddressVerificationSettingsCommand(
    AddressVerificationProvider Provider,
    string? FailedMessage,
    List<string>? Countries,
    bool OfferSuggestion,
    AddressUnavailablePolicy OnUnavailable,
    bool LoggingEnabled,
    bool BlockPoBox,
    string? PoBoxMessage) : IRequest<AddressVerificationSettingsDto>;

public class UpdateAddressVerificationSettingsCommandValidator : AbstractValidator<UpdateAddressVerificationSettingsCommand>
{
    public UpdateAddressVerificationSettingsCommandValidator()
    {
        RuleFor(x => x.Provider).IsInEnum();
        RuleFor(x => x.OnUnavailable).IsInEnum();
        RuleFor(x => x.FailedMessage).MaximumLength(255);
        RuleFor(x => x.FailedMessage).NotEmpty()
            .When(x => x.Provider != AddressVerificationProvider.None)
            .WithMessage("Enter the message shown to customers when an address fails verification.");
        RuleForEach(x => x.Countries).Length(2).WithMessage("Use two-letter country codes, e.g. US.");
        RuleFor(x => x.Countries)
            .Must(c => c is { Count: > 0 })
            .When(x => x.Provider != AddressVerificationProvider.None)
            .WithMessage("Choose at least one country to verify, or switch address verification off.");
        RuleFor(x => x.Countries)
            .Must(c => c == null || c.All(code => string.Equals(code?.Trim(), "US", StringComparison.OrdinalIgnoreCase)))
            .When(x => x.Provider == AddressVerificationProvider.Usps)
            .WithMessage("USPS verifies US addresses only. Remove the other countries or choose Avalara.");
        RuleFor(x => x.PoBoxMessage).MaximumLength(500);
        RuleFor(x => x.PoBoxMessage).NotEmpty()
            .When(x => x.BlockPoBox)
            .WithMessage("Enter the message shown when a P.O. box address is refused.");
    }
}

public class UpdateAddressVerificationSettingsCommandHandler
    : IRequestHandler<UpdateAddressVerificationSettingsCommand, AddressVerificationSettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IAddressVerificationService _verifier;

    public UpdateAddressVerificationSettingsCommandHandler(IApplicationDbContext db, IAddressVerificationService verifier)
    {
        _db = db;
        _verifier = verifier;
    }

    public async Task<AddressVerificationSettingsDto> Handle(UpdateAddressVerificationSettingsCommand request, CancellationToken ct)
    {
        var settings = await _db.AddressVerificationSettings.OrderBy(s => s.CreatedOnUtc).FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new AddressVerificationSettings();
            _db.AddressVerificationSettings.Add(settings);
        }

        settings.Provider = request.Provider;
        settings.FailedMessage = string.IsNullOrWhiteSpace(request.FailedMessage) ? null : request.FailedMessage.Trim();
        var countries = ShippingRulesEngine.SplitCodes(string.Join(",", request.Countries ?? new List<string>()));
        settings.Countries = countries.Count > 0 ? string.Join(",", countries) : null;
        settings.OfferSuggestion = request.OfferSuggestion;
        settings.OnUnavailable = request.OnUnavailable;
        settings.LoggingEnabled = request.LoggingEnabled;

        var rules = await _db.ShippingRuleSettings.OrderBy(s => s.CreatedOnUtc).FirstOrDefaultAsync(ct);
        var poBoxMessage = string.IsNullOrWhiteSpace(request.PoBoxMessage) ? null : request.PoBoxMessage.Trim();
        var newRules = rules is null;
        if (rules is null)
        {
            rules = new ShippingRuleSettings();
            _db.ShippingRuleSettings.Add(rules);
        }
        if (newRules || rules.RefusePoBoxes != request.BlockPoBox || rules.PoBoxMessage != poBoxMessage)
        {
            rules.RefusePoBoxes = request.BlockPoBox;
            rules.PoBoxMessage = poBoxMessage;
            rules.Version++;
        }

        await _db.SaveChangesAsync(ct);

        var dto = AddressVerificationSettingsDto.From(settings, rules);
        dto.ProviderConfigured = await GetAddressVerificationSettingsQueryHandler.ProviderStatusAsync(_verifier, ct);
        return dto;
    }
}

/// <summary>
/// Admin "Test address" (AD-61): runs a provider against a typed address, skipping the cache and the country
/// list, and shows the raw answer. Nothing is cached or recorded on a cart.
/// </summary>
public record TestAddressVerificationCommand(AddressVerificationProvider? Provider, VerificationAddressDto Address)
    : IRequest<AddressVerificationTestDto>;

public class TestAddressVerificationCommandValidator : AbstractValidator<TestAddressVerificationCommand>
{
    public TestAddressVerificationCommandValidator()
    {
        RuleFor(x => x.Address).NotNull();
        RuleFor(x => x.Address.Line1).NotEmpty().When(x => x.Address is not null);
        RuleFor(x => x.Address.CountryCode).NotEmpty().Length(2).When(x => x.Address is not null);
    }
}

public class TestAddressVerificationCommandHandler : IRequestHandler<TestAddressVerificationCommand, AddressVerificationTestDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IAddressVerificationService _verifier;

    public TestAddressVerificationCommandHandler(IApplicationDbContext db, IAddressVerificationService verifier)
    {
        _db = db;
        _verifier = verifier;
    }

    public async Task<AddressVerificationTestDto> Handle(TestAddressVerificationCommand request, CancellationToken ct)
    {
        var provider = request.Provider ?? (await AddressVerificationSupport.LoadAsync(_db, ct)).Provider;
        if (provider == AddressVerificationProvider.None)
            throw new Common.Exceptions.ConflictException("Choose a provider to test (Avalara or USPS).");

        var stopwatch = Stopwatch.StartNew();
        var outcome = await _verifier.VerifyAsync(
            AddressVerificationSupport.ToVerifiable(request.Address), ct,
            checkoutRules: false, provider: provider, bypassCache: true, includeRaw: true);
        stopwatch.Stop();

        return new AddressVerificationTestDto
        {
            Provider = provider,
            Status = outcome.Status,
            IsPoBox = outcome.IsPoBox,
            IsResidential = outcome.IsResidential,
            Suggestion = VerificationAddressDto.From(outcome.Suggestion),
            Messages = outcome.Messages,
            RawResponse = outcome.RawResponse,
            DurationMs = stopwatch.ElapsedMilliseconds,
        };
    }
}
