using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Common.Extensions;

/// <summary>
/// Resolves the store a store-scoped request acts on. The storefront has no store context today, so it (and
/// any admin request without <c>?storeId=</c>) uses the <em>primary</em> store: the first-created enabled one.
/// </summary>
public static class StoreScope
{
    public static async Task<Guid> ResolveAsync(IApplicationDbContext db, Guid? requested, CancellationToken ct)
    {
        if (requested is Guid id)
        {
            if (await db.Stores.AsNoTracking().AnyAsync(s => s.Id == id, ct))
                return id;
            throw new NotFoundException(nameof(Store), id);
        }

        var primary = await db.Stores.AsNoTracking()
            .OrderByDescending(s => s.IsEnabled).ThenBy(s => s.CreatedOnUtc)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);
        return primary ?? throw new NotFoundException("No store is configured. Create a store first.");
    }
}
