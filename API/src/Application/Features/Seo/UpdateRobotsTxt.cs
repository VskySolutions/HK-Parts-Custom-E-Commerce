using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>
/// Sets (or clears) the custom robots.txt body stored as a platform setting. A null/blank body clears the
/// override so <c>/robots.txt</c> reverts to the computed default.
/// </summary>
public record UpdateRobotsTxtCommand(string? Content) : IRequest<SeoSettingsDto>;

public class UpdateRobotsTxtCommandValidator : AbstractValidator<UpdateRobotsTxtCommand>
{
    public UpdateRobotsTxtCommandValidator()
    {
        RuleFor(x => x.Content).MaximumLength(20000);
    }
}

public class UpdateRobotsTxtCommandHandler : IRequestHandler<UpdateRobotsTxtCommand, SeoSettingsDto>
{
    private readonly ISeoService _seo;
    private readonly ISettingsService _settings;

    public UpdateRobotsTxtCommandHandler(ISeoService seo, ISettingsService settings)
    {
        _seo = seo;
        _settings = settings;
    }

    public async Task<SeoSettingsDto> Handle(UpdateRobotsTxtCommand request, CancellationToken cancellationToken)
    {
        // Blank reverts to the default (store null); otherwise persist the trimmed custom body.
        var content = string.IsNullOrWhiteSpace(request.Content) ? null : request.Content.Trim();
        await _settings.SetAsync(SeoSettingKeys.RobotsTxt, content, cancellationToken);

        var effective = await _seo.GetRobotsTxtAsync(cancellationToken);
        return new SeoSettingsDto
        {
            RobotsTxt = effective,
            IsCustomRobotsTxt = content is not null,
        };
    }
}
