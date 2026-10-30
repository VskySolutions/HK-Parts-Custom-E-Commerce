using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.ProductCustomFields;

/// <summary>Returns every product custom field (a small, admin-curated list) by display order then name.</summary>
public record ListProductCustomFieldsQuery(bool? IsActive = null) : IRequest<List<ProductCustomFieldDto>>;

public class ListProductCustomFieldsQueryHandler : IRequestHandler<ListProductCustomFieldsQuery, List<ProductCustomFieldDto>>
{
    private readonly IApplicationDbContext _db;

    public ListProductCustomFieldsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<ProductCustomFieldDto>> Handle(ListProductCustomFieldsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.ProductCustomFields.AsNoTracking();
        if (request.IsActive.HasValue)
            query = query.Where(f => f.IsActive == request.IsActive.Value);

        var fields = await query
            .OrderBy(f => f.DisplayOrder)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);
        return fields.Select(ProductCustomFieldDto.From).ToList();
    }
}
