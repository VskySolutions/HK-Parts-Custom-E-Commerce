using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsHomeSections;

/// <summary>Creates a configurable home page section (WO-96). The typed <paramref name="Config"/> is serialized
/// to the entity's JSON column. Enforces the invariant that at most one HeroBanner section may be enabled.</summary>
public record CreateHomeSectionCommand(
    HomePageSectionType SectionType,
    string DisplayName,
    int DisplayOrder = 0,
    bool IsEnabled = true,
    HomeSectionConfig? Config = null) : IRequest<CmsHomeSectionDto>;

public class CreateHomeSectionCommandValidator : AbstractValidator<CreateHomeSectionCommand>
{
    public CreateHomeSectionCommandValidator()
    {
        RuleFor(x => x.SectionType).IsInEnum();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
    }
}

public class CreateHomeSectionCommandHandler : IRequestHandler<CreateHomeSectionCommand, CmsHomeSectionDto>
{
    private readonly IApplicationDbContext _db;

    public CreateHomeSectionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsHomeSectionDto> Handle(CreateHomeSectionCommand request, CancellationToken cancellationToken)
    {
        // Invariant: at most one enabled HeroBanner at a time.
        if (request is { SectionType: HomePageSectionType.HeroBanner, IsEnabled: true })
        {
            var heroEnabled = await _db.CMSHomePageSections
                .AnyAsync(s => s.SectionType == HomePageSectionType.HeroBanner && s.IsEnabled, cancellationToken);
            if (heroEnabled)
                throw new ConflictException("A Hero Banner section is already enabled. Disable it before enabling another.");
        }

        var config = (request.Config ?? new HomeSectionConfig()).Normalized(request.SectionType);

        var entity = new CMSHomePageSection
        {
            SectionType = request.SectionType,
            DisplayName = request.DisplayName,
            DisplayOrder = request.DisplayOrder,
            IsEnabled = request.IsEnabled,
            Configuration = HomeSectionConfig.Serialize(config),
        };

        _db.CMSHomePageSections.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return CmsHomeSectionDto.From(entity);
    }
}
