using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Branding;

/// <summary>Creates or updates the deployment's singleton branding row.</summary>
public record UpdateBrandingCommand(
    string BrandName,
    string? Domain,
    Guid? LogoMediaId,
    Guid? FaviconMediaId,
    string? LogoUrl,
    string? FaviconUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? AccentColor,
    string? FontFamily,
    string? SupportEmail,
    string? SupportPhone,
    string? SocialLinksJson,
    string? LayoutOptionsJson,
    string? DefaultLanguage,
    string? DisplayTimeZone = null,
    // ---- Storefront HTML-tag palette (all optional; bound by name from the JSON body) ----
    string? BodyBackgroundColor = null,
    string? TextColor = null,
    string? HeadingColor = null,
    string? Heading1Color = null,
    string? Heading2Color = null,
    string? Heading3Color = null,
    string? Heading4Color = null,
    string? Heading5Color = null,
    string? Heading6Color = null,
    string? ParagraphColor = null,
    string? SpanColor = null,
    string? LinkColor = null) : IRequest<BrandingDto>;

public class UpdateBrandingCommandValidator : AbstractValidator<UpdateBrandingCommand>
{
    public UpdateBrandingCommandValidator()
    {
        RuleFor(x => x.BrandName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Domain).MaximumLength(255);
        RuleFor(x => x.LogoUrl).MaximumLength(500);
        RuleFor(x => x.FaviconUrl).MaximumLength(500);
        RuleFor(x => x.PrimaryColor).MaximumLength(32);
        RuleFor(x => x.SecondaryColor).MaximumLength(32);
        RuleFor(x => x.AccentColor).MaximumLength(32);
        RuleFor(x => x.BodyBackgroundColor).MaximumLength(32);
        RuleFor(x => x.TextColor).MaximumLength(32);
        RuleFor(x => x.HeadingColor).MaximumLength(32);
        RuleFor(x => x.Heading1Color).MaximumLength(32);
        RuleFor(x => x.Heading2Color).MaximumLength(32);
        RuleFor(x => x.Heading3Color).MaximumLength(32);
        RuleFor(x => x.Heading4Color).MaximumLength(32);
        RuleFor(x => x.Heading5Color).MaximumLength(32);
        RuleFor(x => x.Heading6Color).MaximumLength(32);
        RuleFor(x => x.ParagraphColor).MaximumLength(32);
        RuleFor(x => x.SpanColor).MaximumLength(32);
        RuleFor(x => x.LinkColor).MaximumLength(32);
    }
}

public class UpdateBrandingCommandHandler : IRequestHandler<UpdateBrandingCommand, BrandingDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPublisher _publisher;

    public UpdateBrandingCommandHandler(IApplicationDbContext db, IPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<BrandingDto> Handle(UpdateBrandingCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.TenantBrandings.FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            entity = new TenantBranding();
            _db.TenantBrandings.Add(entity);
        }

        entity.BrandName = request.BrandName;
        entity.Domain = request.Domain;
        entity.LogoMediaId = request.LogoMediaId;
        entity.FaviconMediaId = request.FaviconMediaId;
        // Going forward the assets live in Media; clear the legacy URL once a Media asset is chosen.
        entity.LogoUrl = request.LogoMediaId.HasValue ? null : request.LogoUrl;
        entity.FaviconUrl = request.FaviconMediaId.HasValue ? null : request.FaviconUrl;
        entity.PrimaryColor = request.PrimaryColor;
        entity.SecondaryColor = request.SecondaryColor;
        entity.AccentColor = request.AccentColor;
        entity.BodyBackgroundColor = request.BodyBackgroundColor;
        entity.TextColor = request.TextColor;
        entity.HeadingColor = request.HeadingColor;
        entity.Heading1Color = request.Heading1Color;
        entity.Heading2Color = request.Heading2Color;
        entity.Heading3Color = request.Heading3Color;
        entity.Heading4Color = request.Heading4Color;
        entity.Heading5Color = request.Heading5Color;
        entity.Heading6Color = request.Heading6Color;
        entity.ParagraphColor = request.ParagraphColor;
        entity.SpanColor = request.SpanColor;
        entity.LinkColor = request.LinkColor;
        entity.FontFamily = request.FontFamily;
        entity.SupportEmail = request.SupportEmail;
        entity.SupportPhone = request.SupportPhone;
        entity.SocialLinksJson = request.SocialLinksJson;
        entity.LayoutOptionsJson = request.LayoutOptionsJson;
        entity.DefaultLanguage = request.DefaultLanguage;
        entity.DisplayTimeZone = string.IsNullOrWhiteSpace(request.DisplayTimeZone) ? "UTC" : request.DisplayTimeZone.Trim();

        await _db.SaveChangesAsync(cancellationToken);
        await _publisher.Publish(new TenantBrandingUpdated(entity.Id, entity.BrandName), cancellationToken);
        return BrandingDto.From(entity);
    }
}
