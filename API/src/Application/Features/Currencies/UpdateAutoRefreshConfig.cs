using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Currencies;

/// <summary>Persists the exchange-rate auto-refresh configuration to platform settings and returns the new config.</summary>
public record UpdateAutoRefreshConfigCommand(bool Enabled, int IntervalHours, string? SourceUrl)
    : IRequest<AutoRefreshConfigDto>;

public class UpdateAutoRefreshConfigCommandValidator : AbstractValidator<UpdateAutoRefreshConfigCommand>
{
    public UpdateAutoRefreshConfigCommandValidator()
    {
        RuleFor(x => x.IntervalHours).GreaterThan(0);
    }
}

public class UpdateAutoRefreshConfigCommandHandler : IRequestHandler<UpdateAutoRefreshConfigCommand, AutoRefreshConfigDto>
{
    private readonly ISettingsService _settings;

    public UpdateAutoRefreshConfigCommandHandler(ISettingsService settings) => _settings = settings;

    public async Task<AutoRefreshConfigDto> Handle(UpdateAutoRefreshConfigCommand request, CancellationToken cancellationToken)
    {
        await _settings.SetAsync("currency.auto-refresh.enabled", request.Enabled ? "true" : "false", cancellationToken);
        await _settings.SetAsync("currency.auto-refresh.interval-hours", request.IntervalHours.ToString(), cancellationToken);
        await _settings.SetAsync("currency.auto-refresh.source-url", request.SourceUrl, cancellationToken);

        return new AutoRefreshConfigDto
        {
            Enabled = request.Enabled,
            IntervalHours = request.IntervalHours,
            SourceUrl = request.SourceUrl,
        };
    }
}
