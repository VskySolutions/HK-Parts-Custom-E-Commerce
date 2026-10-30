using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Currencies;

/// <summary>Reads the exchange-rate auto-refresh configuration from platform settings.</summary>
public record GetAutoRefreshConfigQuery : IRequest<AutoRefreshConfigDto>;

public class GetAutoRefreshConfigQueryHandler : IRequestHandler<GetAutoRefreshConfigQuery, AutoRefreshConfigDto>
{
    private readonly ISettingsService _settings;

    public GetAutoRefreshConfigQueryHandler(ISettingsService settings) => _settings = settings;

    public async Task<AutoRefreshConfigDto> Handle(GetAutoRefreshConfigQuery request, CancellationToken cancellationToken)
    {
        return new AutoRefreshConfigDto
        {
            Enabled = await _settings.GetAsync<bool>("currency.auto-refresh.enabled", cancellationToken),
            IntervalHours = await _settings.GetAsync<int>("currency.auto-refresh.interval-hours", cancellationToken),
            SourceUrl = await _settings.GetValueAsync("currency.auto-refresh.source-url", cancellationToken),
        };
    }
}
