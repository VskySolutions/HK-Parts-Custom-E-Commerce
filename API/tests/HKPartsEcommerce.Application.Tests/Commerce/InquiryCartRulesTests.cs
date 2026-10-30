using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Cart;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Commerce;

/// <summary>
/// A cart is either all-buyable or all quote-only (REQ-INQ-001). The two check out through different
/// flows — one takes payment, the other creates an inquiry — so a mixed cart would mean a single
/// "check out" click producing both an order and a lead. The rule is enforced when the item is added,
/// where the buyer can still act on it, rather than at the checkout door.
/// </summary>
public class InquiryCartRulesTests : CatalogTestBase
{
    /// <summary>Commerce mode stubbed directly: these tests are about the cart rule, not settings parsing.</summary>
    private sealed class StubCommerceMode : ICommerceModeService
    {
        private readonly CommerceModeSettings _settings;
        public StubCommerceMode(CommerceMode mode) =>
            _settings = CommerceModeSettings.Default with { Mode = mode };

        public Task<CommerceModeSettings> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_settings);

        public Task<bool> IsInquiryOnlyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_settings.IsInquiryOnly);
    }

    /// <summary>No group pricing in play: every line keeps its base price.</summary>
    private sealed class NoGroupPricing : ICustomerGroupService
    {
        public Task<Guid?> GetCurrentGroupIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(null);

        public Task<Guid?> GetGroupIdForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(null);

        public Task<decimal> ResolvePriceAsync(
            Guid productId, Guid? variantId, decimal basePrice, Guid? groupId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(basePrice);

        public Task<IReadOnlyDictionary<GroupPriceKey, decimal>> ResolvePricesAsync(
            IReadOnlyCollection<GroupPriceRequest> items, Guid? groupId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<GroupPriceKey, decimal>>(
                items.ToDictionary(i => new GroupPriceKey(i.ProductId, i.VariantId), i => i.BasePrice));
    }

    private const string Session = "test-session";

    private Guid SeedBuyable(string name = "Buyable") =>
        SeedProduct(p => { p.Name = name; p.IsPublished = true; p.Price = 10m; });

    private Guid SeedQuoteOnly(string name = "Quote only") =>
        SeedProduct(p => { p.Name = name; p.IsPublished = true; p.Price = 100m; p.IsInquiryOnly = true; });

    private async Task AddAsync(Guid productId, CommerceMode mode = CommerceMode.Standard)
    {
        using var db = NewContext();
        var handler = new AddItemCommandHandler(
            db, new FakeCurrentUser(), new NoGroupPricing(), new StubCommerceMode(mode));
        await handler.Handle(new AddItemCommand(productId, null, 1, Session), CancellationToken.None);
    }

    [Fact]
    public async Task Quote_only_item_cannot_join_a_buyable_cart()
    {
        await AddAsync(SeedBuyable());

        var ex = await Assert.ThrowsAsync<ConflictException>(() => AddAsync(SeedQuoteOnly()));

        Assert.Contains("on their own", ex.Message);
    }

    [Fact]
    public async Task Buyable_item_cannot_join_a_quote_only_cart()
    {
        await AddAsync(SeedQuoteOnly());

        var ex = await Assert.ThrowsAsync<ConflictException>(() => AddAsync(SeedBuyable()));

        Assert.Contains("quote-only item", ex.Message);
    }

    [Fact]
    public async Task Two_quote_only_items_share_a_cart()
    {
        await AddAsync(SeedQuoteOnly("First"));
        await AddAsync(SeedQuoteOnly("Second"));

        using var db = NewContext();
        var cart = db.Carts.Single(c => c.SessionId == Session);
        Assert.Equal(2, db.CartItems.Count(i => i.CartId == cart.Id));
    }

    [Fact]
    public async Task Two_buyable_items_share_a_cart()
    {
        await AddAsync(SeedBuyable("First"));
        await AddAsync(SeedBuyable("Second"));

        using var db = NewContext();
        var cart = db.Carts.Single(c => c.SessionId == Session);
        Assert.Equal(2, db.CartItems.Count(i => i.CartId == cart.Id));
    }

    [Fact]
    public async Task Inquiry_only_tenant_never_blocks_a_mix()
    {
        // Everything is quote-only there, so the flag on individual products is irrelevant — applying the
        // rule would refuse perfectly ordinary carts.
        await AddAsync(SeedQuoteOnly(), CommerceMode.InquiryOnly);
        await AddAsync(SeedBuyable(), CommerceMode.InquiryOnly);

        using var db = NewContext();
        var cart = db.Carts.Single(c => c.SessionId == Session);
        Assert.Equal(2, db.CartItems.Count(i => i.CartId == cart.Id));
    }
}
