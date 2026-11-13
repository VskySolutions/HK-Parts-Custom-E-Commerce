using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

/// <summary>Admin Customer → Wishlist tab: listing (GetCustomerWishlistItems) and the badge count (GetCustomerDetail).</summary>
public class CustomerWishlistTests : CatalogTestBase
{
    private static readonly DateTime T0 = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private Guid SeedCustomer()
    {
        using var db = NewContext();
        var user = new User
        {
            Username = $"u{Guid.NewGuid():N}", Email = $"{Guid.NewGuid():N}@test.local", PasswordHash = "x",
            Customer = new Customer { FirstName = "Wish", LastName = "Owner" },
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Customer!.Id;
    }

    /// <summary>Seeds a wishlist with the given (productId, variantId) items; item i is added at T0 + i minutes.</summary>
    private Guid SeedWishlist(Guid customerId, bool deleted = false, params (Guid ProductId, Guid? VariantId)[] items)
    {
        using var db = NewContext();
        var wishlist = new Wishlist { CustomerId = customerId };
        db.Wishlists.Add(wishlist);
        db.SaveChanges();

        for (var i = 0; i < items.Length; i++)
            db.WishlistItems.Add(new WishlistItem { WishlistId = wishlist.Id, ProductId = items[i].ProductId, ProductVariantId = items[i].VariantId });
        db.SaveChanges();

        // CreatedOnUtc is stamped on save; pin it so the newest-first order is deterministic.
        var rows = db.WishlistItems.Where(x => x.WishlistId == wishlist.Id).ToList();
        for (var i = 0; i < items.Length; i++)
            rows.Single(r => r.ProductId == items[i].ProductId && r.ProductVariantId == items[i].VariantId).CreatedOnUtc = T0.AddMinutes(i);
        if (deleted) db.Wishlists.Remove(wishlist); // soft delete via ISoftDeletable interceptor
        db.SaveChanges();
        return wishlist.Id;
    }

    private async Task<Application.Common.Models.PaginatedList<CustomerWishlistItemDto>> List(Guid customerId, int page = 1, int pageSize = 10)
    {
        using var db = NewContext();
        return await new GetCustomerWishlistItemsQueryHandler(db)
            .Handle(new GetCustomerWishlistItemsQuery(customerId, page, pageSize), CancellationToken.None);
    }

    private async Task<int> BadgeCount(Guid customerId)
    {
        using var db = NewContext();
        var detail = await new GetCustomerDetailQueryHandler(db).Handle(new GetCustomerDetailQuery(customerId), CancellationToken.None);
        return detail.WishlistItemCount;
    }

    [Fact]
    public async Task Lists_items_newest_first_with_variant_sku_preferred_and_counts_them()
    {
        var customerId = SeedCustomer();
        var older = SeedProduct(p => { p.Name = "Older"; p.Sku = "P-OLD"; p.IsPublished = true; });
        var parent = SeedProduct(p => { p.Name = "Parent"; p.Sku = "P-PARENT"; p.IsPublished = true; });
        var variant = SeedVariant(parent, v => v.Sku = "V-RED");
        SeedWishlist(customerId, items: new[] { (older, (Guid?)null), (parent, (Guid?)variant) });

        var page = await List(customerId);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { "Parent", "Older" }, page.Items.Select(i => i.ProductName));
        Assert.Equal(new[] { "V-RED", "P-OLD" }, page.Items.Select(i => i.Sku));
        Assert.Equal(2, await BadgeCount(customerId));
    }

    [Fact]
    public async Task Variant_without_its_own_sku_falls_back_to_product_sku()
    {
        var customerId = SeedCustomer();
        var product = SeedProduct(p => { p.Name = "Base"; p.Sku = "P-BASE"; });
        var variant = SeedVariant(product, v => v.Sku = null);
        SeedWishlist(customerId, items: new[] { (product, (Guid?)variant) });

        var item = Assert.Single((await List(customerId)).Items);
        Assert.Equal("P-BASE", item.Sku);
    }

    [Fact]
    public async Task Null_variant_and_no_sku_anywhere_returns_null_sku()
    {
        var customerId = SeedCustomer();
        var product = SeedProduct(p => { p.Name = "No SKU"; p.Sku = null; });
        SeedWishlist(customerId, items: new[] { (product, (Guid?)null) });

        var item = Assert.Single((await List(customerId)).Items);
        Assert.Equal("No SKU", item.ProductName);
        Assert.Null(item.Sku);
    }

    [Fact]
    public async Task Customer_without_a_wishlist_gets_an_empty_page_and_zero_count()
    {
        var customerId = SeedCustomer();

        var page = await List(customerId);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, await BadgeCount(customerId));
    }

    [Fact]
    public async Task Items_of_a_deleted_wishlist_are_excluded()
    {
        var customerId = SeedCustomer();
        var gone = SeedProduct(p => p.Name = "In deleted list");
        var kept = SeedProduct(p => p.Name = "In active list");
        SeedWishlist(customerId, deleted: true, items: new[] { (gone, (Guid?)null) });
        SeedWishlist(customerId, items: new[] { (kept, (Guid?)null) });

        var item = Assert.Single((await List(customerId)).Items);
        Assert.Equal("In active list", item.ProductName);
        Assert.Equal(1, await BadgeCount(customerId));
    }

    [Fact]
    public async Task Deleted_or_unpublished_product_and_deleted_variant_still_show_stored_values()
    {
        var customerId = SeedCustomer();
        var deletedProduct = SeedProduct(p => { p.Name = "Deleted product"; p.Sku = "P-DEL"; p.IsPublished = true; });
        var unpublished = SeedProduct(p => { p.Name = "Draft product"; p.Sku = "P-DRAFT"; p.IsPublished = false; });
        var parent = SeedProduct(p => { p.Name = "Parent"; p.Sku = "P-PAR"; });
        var deletedVariant = SeedVariant(parent, v => v.Sku = "V-DEL");
        var missingProductId = Guid.NewGuid(); // no row at all
        SeedWishlist(customerId, items: new[]
        {
            (deletedProduct, (Guid?)null), (unpublished, (Guid?)null), (parent, (Guid?)deletedVariant), (missingProductId, (Guid?)null),
        });
        using (var db = NewContext())
        {
            db.Products.Remove(db.Products.Single(p => p.Id == deletedProduct));
            db.ProductVariants.Remove(db.ProductVariants.Single(v => v.Id == deletedVariant));
            db.SaveChanges();
        }

        var items = (await List(customerId)).Items;

        Assert.Equal(4, items.Count);
        Assert.Null(items[0].ProductName); // missing product row → UI shows "—"
        Assert.Null(items[0].Sku);
        Assert.Equal(("Parent", "V-DEL"), (items[1].ProductName, items[1].Sku));
        Assert.Equal(("Draft product", "P-DRAFT"), (items[2].ProductName, items[2].Sku));
        Assert.Equal(("Deleted product", "P-DEL"), (items[3].ProductName, items[3].Sku));
    }

    [Fact]
    public async Task Pages_through_items()
    {
        var customerId = SeedCustomer();
        var products = Enumerable.Range(0, 3).Select(i => SeedProduct(p => p.Name = $"P{i}")).ToArray();
        SeedWishlist(customerId, items: products.Select(p => (p, (Guid?)null)).ToArray());

        var second = await List(customerId, page: 2, pageSize: 2);

        Assert.Equal(3, second.TotalCount);
        Assert.Equal(2, second.TotalPages);
        Assert.Equal("P0", Assert.Single(second.Items).ProductName);
    }

    [Fact]
    public async Task Unknown_customer_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => List(Guid.NewGuid()));
}
