using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>One saved item on the Customer → Wishlist tab (read-only admin view).</summary>
public class CustomerWishlistItemDto
{
    public Guid Id { get; set; }
    /// <summary>Null when the product row no longer exists at all (the UI shows "—").</summary>
    public string? ProductName { get; set; }
    /// <summary>The variant's own SKU when set, else the product SKU; null when neither exists (the UI shows "—").</summary>
    public string? Sku { get; set; }
    /// <summary>First image (variant-specific first, then by display order); null when none.</summary>
    public string? ImageUrl { get; set; }
}

/// <summary>
/// A customer's wishlist items for the admin Wishlist tab, newest first and paged. Only active wishlists
/// (<c>Wishlists.Deleted = 0</c>, via the global filter) are included.
/// </summary>
public record GetCustomerWishlistItemsQuery(Guid CustomerId, int Page = 1, int PageSize = 10)
    : IRequest<PaginatedList<CustomerWishlistItemDto>>;

public class GetCustomerWishlistItemsQueryHandler
    : IRequestHandler<GetCustomerWishlistItemsQuery, PaginatedList<CustomerWishlistItemDto>>
{
    private readonly IApplicationDbContext _db;

    public GetCustomerWishlistItemsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CustomerWishlistItemDto>> Handle(
        GetCustomerWishlistItemsQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Customers.AsNoTracking().AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
            throw new NotFoundException(nameof(Customer), request.CustomerId);

        // One SQL statement per page (plus the page count): items → active wishlist (inner join, soft-delete
        // filter applies) → product / variant (left joins that IGNORE the soft-delete filter, so a deleted or
        // unpublished product still shows its stored name and SKU) → first image as a correlated subquery.
        var query =
            from item in CustomerWishlistItems.ForCustomer(_db, request.CustomerId)
            join p in _db.Products.IgnoreQueryFilters() on item.ProductId equals p.Id into pj
            from product in pj.DefaultIfEmpty()
            join v in _db.ProductVariants.IgnoreQueryFilters() on item.ProductVariantId equals (Guid?)v.Id into vj
            from variant in vj.DefaultIfEmpty()
            orderby item.CreatedOnUtc descending, item.Id
            select new CustomerWishlistItemDto
            {
                Id = item.Id,
                ProductName = product != null ? product.Name : null,
                Sku = variant != null && variant.Sku != null && variant.Sku != ""
                    ? variant.Sku
                    : (product != null && product.Sku != "" ? product.Sku : null),
                ImageUrl = _db.ProductPictures
                    .Where(pp => pp.ProductId == item.ProductId
                        && pp.Media != null && pp.Media.MediaType == MediaType.Image)
                    .OrderBy(pp => pp.ProductVariantId == item.ProductVariantId && item.ProductVariantId != null ? 0 : 1)
                    .ThenBy(pp => pp.DisplayOrder)
                    .Select(pp => pp.Media!.Url)
                    .FirstOrDefault(),
            };

        return await PaginatedList<CustomerWishlistItemDto>.CreateAsync(
            query.AsNoTracking(), request.Page, request.PageSize, cancellationToken);
    }
}

/// <summary>Shared source for the Wishlist tab listing and its badge count.</summary>
internal static class CustomerWishlistItems
{
    /// <summary>Items in the customer's active (not soft-deleted) wishlist(s).</summary>
    public static IQueryable<WishlistItem> ForCustomer(IApplicationDbContext db, Guid customerId)
        => from item in db.WishlistItems.AsNoTracking()
           join w in db.Wishlists.AsNoTracking() on item.WishlistId equals w.Id
           where w.CustomerId == customerId
           select item;
}
