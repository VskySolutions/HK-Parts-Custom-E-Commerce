using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsHomeSections;

/// <summary>Updates an existing home page section (WO-96): its type, name, order, enabled flag and typed config.
/// Re-checks the single-enabled-HeroBanner invariant (excluding this section).</summary>
public record UpdateHomeSectionCommand(
    Guid Id,
    HomePageSectionType SectionType,
    string DisplayName,
    int DisplayOrder = 0,
    bool IsEnabled = true,
    HomeSectionConfig? Config = null) : IRequest<CmsHomeSectionDto>;

public class UpdateHomeSectionCommandValidator : AbstractValidator<UpdateHomeSectionCommand>
{
    public UpdateHomeSectionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SectionType).IsInEnum();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
    }
}

public class UpdateHomeSectionCommandHandler : IRequestHandler<UpdateHomeSectionCommand, CmsHomeSectionDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateHomeSectionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsHomeSectionDto> Handle(UpdateHomeSectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSHomePageSections
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSHomePageSection), request.Id);

        // Invariant: at most one enabled HeroBanner at a time (ignore this section itself).
        if (request is { SectionType: HomePageSectionType.HeroBanner, IsEnabled: true })
        {
            var otherHeroEnabled = await _db.CMSHomePageSections
                .AnyAsync(s => s.Id != request.Id
                    && s.SectionType == HomePageSectionType.HeroBanner
                    && s.IsEnabled, cancellationToken);
            if (otherHeroEnabled)
                throw new ConflictException("A Hero Banner section is already enabled. Disable it before enabling another.");
        }

        var config = (request.Config ?? new HomeSectionConfig()).Normalized(request.SectionType);

        entity.SectionType = request.SectionType;
        entity.DisplayName = request.DisplayName;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsEnabled = request.IsEnabled;
        entity.Configuration = HomeSectionConfig.Serialize(config);

        await _db.SaveChangesAsync(cancellationToken);
        return CmsHomeSectionDto.From(entity);
    }
}
