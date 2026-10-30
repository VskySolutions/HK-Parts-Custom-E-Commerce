using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsHomeSections;

/// <summary>Toggles a section's enabled flag. Enabling a HeroBanner while another HeroBanner is already enabled
/// is rejected (the single-enabled-hero invariant).</summary>
public record SetHomeSectionEnabledCommand(Guid Id, bool Enabled) : IRequest<CmsHomeSectionDto>;

public class SetHomeSectionEnabledCommandValidator : AbstractValidator<SetHomeSectionEnabledCommand>
{
    public SetHomeSectionEnabledCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class SetHomeSectionEnabledCommandHandler : IRequestHandler<SetHomeSectionEnabledCommand, CmsHomeSectionDto>
{
    private readonly IApplicationDbContext _db;

    public SetHomeSectionEnabledCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsHomeSectionDto> Handle(SetHomeSectionEnabledCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSHomePageSections
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSHomePageSection), request.Id);

        // Invariant: at most one enabled HeroBanner at a time.
        if (request.Enabled && entity.SectionType == HomePageSectionType.HeroBanner)
        {
            var otherHeroEnabled = await _db.CMSHomePageSections
                .AnyAsync(s => s.Id != entity.Id
                    && s.SectionType == HomePageSectionType.HeroBanner
                    && s.IsEnabled, cancellationToken);
            if (otherHeroEnabled)
                throw new ConflictException("A Hero Banner section is already enabled. Disable it before enabling another.");
        }

        entity.IsEnabled = request.Enabled;
        await _db.SaveChangesAsync(cancellationToken);
        return CmsHomeSectionDto.From(entity);
    }
}
