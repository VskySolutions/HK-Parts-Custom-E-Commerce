using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsHomeSections;

/// <summary>Rewrites section ordering: each section's <c>DisplayOrder</c> becomes its index in the supplied
/// list. Ids not present are ignored; sections omitted from the list keep their previous order. Returns the
/// full section list in the new order.</summary>
public record ReorderHomeSectionsCommand(List<Guid> OrderedIds) : IRequest<IReadOnlyList<CmsHomeSectionDto>>;

public class ReorderHomeSectionsCommandValidator : AbstractValidator<ReorderHomeSectionsCommand>
{
    public ReorderHomeSectionsCommandValidator()
    {
        RuleFor(x => x.OrderedIds).NotNull();
    }
}

public class ReorderHomeSectionsCommandHandler : IRequestHandler<ReorderHomeSectionsCommand, IReadOnlyList<CmsHomeSectionDto>>
{
    private readonly IApplicationDbContext _db;

    public ReorderHomeSectionsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CmsHomeSectionDto>> Handle(ReorderHomeSectionsCommand request, CancellationToken cancellationToken)
    {
        var sections = await _db.CMSHomePageSections.ToListAsync(cancellationToken);

        var orderedIds = request.OrderedIds ?? new List<Guid>();
        for (var index = 0; index < orderedIds.Count; index++)
        {
            var section = sections.FirstOrDefault(s => s.Id == orderedIds[index]);
            if (section is not null)
                section.DisplayOrder = index;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return sections
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Id)
            .Select(CmsHomeSectionDto.From)
            .ToList();
    }
}
