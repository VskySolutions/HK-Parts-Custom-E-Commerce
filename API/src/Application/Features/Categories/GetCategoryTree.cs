using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Categories;

/// <summary>Returns the full category hierarchy as a list of root nodes with nested children.</summary>
public record GetCategoryTreeQuery() : IRequest<List<CategoryTreeNodeDto>>;

public class GetCategoryTreeQueryHandler : IRequestHandler<GetCategoryTreeQuery, List<CategoryTreeNodeDto>>
{
    private readonly IApplicationDbContext _db;

    public GetCategoryTreeQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<CategoryTreeNodeDto>> Handle(GetCategoryTreeQuery request, CancellationToken cancellationToken)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var nodes = categories.ToDictionary(c => c.Id, CategoryTreeNodeDto.From);
        var roots = new List<CategoryTreeNodeDto>();

        foreach (var category in categories)
        {
            var node = nodes[category.Id];
            if (category.ParentId.HasValue && nodes.TryGetValue(category.ParentId.Value, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }

        return roots;
    }
}
