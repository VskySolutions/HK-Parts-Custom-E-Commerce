using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.StoreManagers;

/// <summary>Lists store-manager assignments, optionally filtered to a single store.</summary>
public record ListStoreManagersQuery(Guid? StoreId) : IRequest<IReadOnlyList<StoreManagerAssignmentDto>>;

public class ListStoreManagersQueryHandler : IRequestHandler<ListStoreManagersQuery, IReadOnlyList<StoreManagerAssignmentDto>>
{
    private readonly IApplicationDbContext _db;

    public ListStoreManagersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<StoreManagerAssignmentDto>> Handle(ListStoreManagersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<StoreManagerAssignment> query = _db.StoreManagerAssignments
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Store);

        if (request.StoreId is Guid storeId)
            query = query.Where(a => a.StoreId == storeId);

        var list = await query
            .OrderBy(a => a.StoreId)
            .ToListAsync(cancellationToken);

        return list.Select(StoreManagerAssignmentDto.From).ToList();
    }
}
