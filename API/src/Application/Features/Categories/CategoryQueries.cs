using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Categories;

/// <summary>Shared loading helpers for the category detail view.</summary>
public static class CategoryQueries
{
    /// <summary>Eager-loads everything <see cref="CategoryDto.From"/> reads (image slots + facet attributes).</summary>
    public static IQueryable<Category> WithDetails(this IQueryable<Category> query) =>
        query
            .Include(c => c.Image)
            .Include(c => c.Image2)
            .Include(c => c.MetaImage)
            .Include(c => c.FilterAttributes);

    public static async Task<CategoryDto> GetDtoAsync(IApplicationDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .AsNoTracking()
            .WithDetails()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), id);

        return CategoryDto.From(category);
    }

    /// <summary>Throws <see cref="NotFoundException"/> for the first referenced media id that doesn't exist.</summary>
    public static async Task EnsureMediaExistsAsync(IApplicationDbContext db, CancellationToken cancellationToken, params Guid?[] mediaIds)
    {
        var ids = mediaIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
            return;

        var found = await db.Media.Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToListAsync(cancellationToken);
        var missing = ids.Except(found).ToList();
        if (missing.Count > 0)
            throw new NotFoundException(nameof(Media), missing[0]);
    }
}
