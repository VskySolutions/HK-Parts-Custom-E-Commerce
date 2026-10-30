using System.Globalization;
using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Loyalty;

/// <summary>
/// Writes the loyalty program configuration to platform settings (WO-27). Rates are stored with the
/// invariant culture so they read back identically regardless of server locale.
/// </summary>
public record UpdateLoyaltyConfigCommand(bool Enabled, decimal EarnRate, decimal RedeemRate) : IRequest<LoyaltyConfigDto>;

public class UpdateLoyaltyConfigCommandValidator : AbstractValidator<UpdateLoyaltyConfigCommand>
{
    public UpdateLoyaltyConfigCommandValidator()
    {
        // Earning may be switched off (0), but redeeming divides by the rate, so it must be positive.
        RuleFor(x => x.EarnRate).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.RedeemRate).GreaterThan(0m);
    }
}

public class UpdateLoyaltyConfigCommandHandler : IRequestHandler<UpdateLoyaltyConfigCommand, LoyaltyConfigDto>
{
    private readonly ISettingsService _settings;

    public UpdateLoyaltyConfigCommandHandler(ISettingsService settings) => _settings = settings;

    public async Task<LoyaltyConfigDto> Handle(UpdateLoyaltyConfigCommand request, CancellationToken cancellationToken)
    {
        await _settings.SetAsync(LoyaltySettings.EnabledKey, request.Enabled ? "true" : "false", cancellationToken);
        await _settings.SetAsync(LoyaltySettings.EarnRateKey, request.EarnRate.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await _settings.SetAsync(LoyaltySettings.RedeemRateKey, request.RedeemRate.ToString(CultureInfo.InvariantCulture), cancellationToken);

        return new LoyaltyConfigDto
        {
            Enabled = request.Enabled,
            EarnRate = request.EarnRate,
            RedeemRate = request.RedeemRate,
        };
    }
}
