using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsBanners;

/// <summary>Creates a new promotional banner/slide.</summary>
public record CreateCmsBannerCommand(
    string Title,
    string DisplayLocation,
    string? Subtitle = null,
    Guid? ImageMediaId = null,
    string? LinkUrl = null,
    string? CtaLabel = null,
    DateTime? StartsOnUtc = null,
    DateTime? EndsOnUtc = null,
    int DisplayOrder = 0,
    bool IsEnabled = true) : IRequest<CmsBannerDto>;

public class CreateCmsBannerCommandValidator : AbstractValidator<CreateCmsBannerCommand>
{
    public CreateCmsBannerCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayLocation).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EndsOnUtc).GreaterThan(x => x.StartsOnUtc!.Value)
            .When(x => x.StartsOnUtc.HasValue && x.EndsOnUtc.HasValue)
            .WithMessage("EndsOnUtc must be after StartsOnUtc.");
    }
}

public class CreateCmsBannerCommandHandler : IRequestHandler<CreateCmsBannerCommand, CmsBannerDto>
{
    private readonly IApplicationDbContext _db;

    public CreateCmsBannerCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsBannerDto> Handle(CreateCmsBannerCommand request, CancellationToken cancellationToken)
    {
        var entity = new CMSBanner
        {
            Title = request.Title,
            Subtitle = request.Subtitle,
            ImageMediaId = request.ImageMediaId,
            LinkUrl = request.LinkUrl,
            CtaLabel = request.CtaLabel,
            DisplayLocation = request.DisplayLocation,
            StartsOnUtc = request.StartsOnUtc,
            EndsOnUtc = request.EndsOnUtc,
            DisplayOrder = request.DisplayOrder,
            IsEnabled = request.IsEnabled,
        };

        _db.CMSBanners.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return CmsBannerDto.From(entity);
    }
}
