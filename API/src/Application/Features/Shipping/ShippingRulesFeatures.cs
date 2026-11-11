using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Shipping;

/// <summary>The shipping rules (add-on fees + destination restrictions); the legacy defaults until first saved.</summary>
public record GetShippingRulesQuery : IRequest<ShippingRulesDto>;

public class GetShippingRulesQueryHandler : IRequestHandler<GetShippingRulesQuery, ShippingRulesDto>
{
    private readonly IApplicationDbContext _db;

    public GetShippingRulesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ShippingRulesDto> Handle(GetShippingRulesQuery request, CancellationToken cancellationToken) =>
        ShippingRulesDto.From(await ShippingRulesEngine.LoadAsync(_db, cancellationToken));
}

/// <summary>
/// Saves the singleton shipping rules and bumps their version. Placed orders keep the fees they were charged:
/// each order's shipping snapshot records its own amounts and the version that priced them (OP-20/22).
/// </summary>
public record UpdateShippingRulesCommand(
    bool SignatureEnabled,
    decimal SignatureFee,
    bool InsuranceEnabled,
    List<InsuranceBand> InsuranceBands,
    decimal InsuranceExcessFeePer100,
    bool StateRestrictionEnabled,
    List<string> RestrictedStates,
    bool InternationalRestrictionEnabled,
    List<string>? AllowedCountryCodes,
    bool RefusePoBoxes,
    string? PoBoxMessage,
    string? InvalidDestinationMessage) : IRequest<ShippingRulesDto>;

public class UpdateShippingRulesCommandValidator : AbstractValidator<UpdateShippingRulesCommand>
{
    public UpdateShippingRulesCommandValidator()
    {
        RuleFor(x => x.SignatureFee).InclusiveBetween(0m, 10000m);
        RuleFor(x => x.InsuranceExcessFeePer100).InclusiveBetween(0m, 10000m);
        RuleFor(x => x.InsuranceBands).NotNull();
        RuleFor(x => x.InsuranceBands)
            .Must(b => b is { Count: > 0 })
            .When(x => x.InsuranceEnabled)
            .WithMessage("Add at least one insurance band, or switch insurance off.");
        RuleForEach(x => x.InsuranceBands).ChildRules(b =>
        {
            b.RuleFor(x => x.UpTo).GreaterThan(0m).WithMessage("Each band's declared-value limit must be above 0.");
            b.RuleFor(x => x.Fee).InclusiveBetween(0m, 10000m);
        });
        RuleFor(x => x.InsuranceBands)
            .Must(b => b == null || b.Select(x => x.UpTo).Distinct().Count() == b.Count)
            .WithMessage("Two insurance bands have the same declared-value limit.");
        RuleFor(x => x.RestrictedStates).NotNull();
        RuleForEach(x => x.RestrictedStates).NotEmpty().MaximumLength(10);
        RuleForEach(x => x.AllowedCountryCodes).Length(2).WithMessage("Use two-letter country codes, e.g. US.");
        RuleFor(x => x.PoBoxMessage).MaximumLength(500);
        RuleFor(x => x.InvalidDestinationMessage).MaximumLength(500);
    }
}

public class UpdateShippingRulesCommandHandler : IRequestHandler<UpdateShippingRulesCommand, ShippingRulesDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateShippingRulesCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ShippingRulesDto> Handle(UpdateShippingRulesCommand request, CancellationToken cancellationToken)
    {
        var rules = await _db.ShippingRuleSettings.OrderBy(s => s.CreatedOnUtc).FirstOrDefaultAsync(cancellationToken);
        if (rules is null)
        {
            // Version 1 is the built-in defaults that priced orders until now, so the first save becomes 2.
            rules = new ShippingRuleSettings();
            _db.ShippingRuleSettings.Add(rules);
        }

        rules.SignatureEnabled = request.SignatureEnabled;
        rules.SignatureFee = decimal.Round(request.SignatureFee, 2);
        rules.InsuranceEnabled = request.InsuranceEnabled;
        rules.InsuranceBandsJson = request.InsuranceBands.Count > 0
            ? ShippingRulesEngine.SerializeBands(request.InsuranceBands
                .Select(b => new InsuranceBand(decimal.Round(b.UpTo, 2), decimal.Round(b.Fee, 2))))
            : null;
        rules.InsuranceExcessFeePer100 = decimal.Round(request.InsuranceExcessFeePer100, 2);
        rules.StateRestrictionEnabled = request.StateRestrictionEnabled;
        rules.RestrictedStates = string.Join(",", request.RestrictedStates
            .Select(ShippingRulesEngine.NormalizeUsState)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct());
        rules.InternationalRestrictionEnabled = request.InternationalRestrictionEnabled;
        var countries = ShippingRulesEngine.SplitCodes(string.Join(",", request.AllowedCountryCodes ?? new List<string>()));
        rules.AllowedCountryCodes = countries.Count > 0 ? string.Join(",", countries) : null;
        rules.RefusePoBoxes = request.RefusePoBoxes;
        rules.PoBoxMessage = string.IsNullOrWhiteSpace(request.PoBoxMessage) ? null : request.PoBoxMessage.Trim();
        rules.InvalidDestinationMessage = string.IsNullOrWhiteSpace(request.InvalidDestinationMessage)
            ? null : request.InvalidDestinationMessage.Trim();
        rules.Version++;

        await _db.SaveChangesAsync(cancellationToken);
        return ShippingRulesDto.From(rules);
    }
}
