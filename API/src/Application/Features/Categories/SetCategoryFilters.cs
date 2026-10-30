using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Categories;

/// <summary>
/// Replaces the facets offered on a category's listing: the brand facet toggle plus exactly the requested
/// specification attributes, in the given order.
/// </summary>
public record SetCategoryFiltersCommand(Guid CategoryId, bool ShowBrandFilter, List<Guid> SpecificationAttributeIds) : IRequest<CategoryDto>;

public class SetCategoryFiltersCommandHandler : IRequestHandler<SetCategoryFiltersCommand, CategoryDto>
{
    private readonly IApplicationDbContext _db;

    public SetCategoryFiltersCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CategoryDto> Handle(SetCategoryFiltersCommand request, CancellationToken cancellationToken)
    {
        var category = await _db.Categories
            .Include(c => c.FilterAttributes)
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), request.CategoryId);

        var attributeIds = request.SpecificationAttributeIds?.Distinct().ToList() ?? new List<Guid>();
        if (attributeIds.Count > 0)
        {
            var found = await _db.SpecificationAttributes
                .Where(a => attributeIds.Contains(a.Id))
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);
            var missing = attributeIds.Except(found).ToList();
            if (missing.Count > 0)
                throw new NotFoundException(nameof(SpecificationAttribute), missing[0]);
        }

        category.ShowBrandFilter = request.ShowBrandFilter;

        var desired = attributeIds.ToHashSet();
        foreach (var row in category.FilterAttributes.Where(f => !desired.Contains(f.SpecificationAttributeId)).ToList())
            category.FilterAttributes.Remove(row);

        for (var i = 0; i < attributeIds.Count; i++)
        {
            var attributeId = attributeIds[i];
            var row = category.FilterAttributes.FirstOrDefault(f => f.SpecificationAttributeId == attributeId);
            if (row is null)
                category.FilterAttributes.Add(new CategoryFilterAttribute { CategoryId = category.Id, SpecificationAttributeId = attributeId, DisplayOrder = i });
            else
                row.DisplayOrder = i;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await CategoryQueries.GetDtoAsync(_db, category.Id, cancellationToken);
    }
}
