using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Ebay;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Ebay;
using HKPartsEcommerce.Infrastructure.Inventory;
using HKPartsEcommerce.Infrastructure.Persistence;
using HKPartsEcommerce.Infrastructure.Persistence.Interceptors;
using HKPartsEcommerce.Infrastructure.Pricing;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Ebay;

/// <summary>
/// eBay Listings acceptance list (1–20) against the real SQL Server test DB, with eBay itself replaced by
/// <see cref="FakeEbayClient"/> (and, for the transport tests, a stubbed HTTP handler under the real client).
/// </summary>
public class EbayAcceptanceTests : CatalogTestBase
{
    private const string LeafCategory = "33559";
    private const string ParentCategory = "6000";

    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private readonly FixedClock _clock = new(Now);
    private readonly FakeEbayClient _ebay = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly Guid _store;

    public EbayAcceptanceTests()
    {
        _store = SeedStore("Main");
        SeedAccount(_store);
        using var db = NewContext();
        db.EbayCategories.AddRange(
            new EbayCategory { CategoryId = ParentCategory, Name = "eBay Motors", Level = 1, IsLeaf = false },
            new EbayCategory { CategoryId = LeafCategory, ParentCategoryId = ParentCategory, Name = "Brake Pads", Level = 2, IsLeaf = true });
        db.SaveChanges();
    }

    // ---- seed helpers -------------------------------------------------------------------------------------

    private Guid SeedStore(string name)
    {
        using var db = NewContext();
        var store = new Store { Name = name, IsEnabled = true };
        db.Stores.Add(store);
        db.SaveChanges();
        return store.Id;
    }

    private void SeedAccount(Guid storeId, Action<EbayAccount>? configure = null)
    {
        using var db = NewContext();
        var account = new EbayAccount
        {
            StoreId = storeId,
            IsEnabled = true,
            Environment = EbayEnvironment.Production,
            CategoryTreeId = "0",
            AccessToken = "token",
            AccessTokenExpiresAtUtc = Now.AddHours(1),
            RefreshToken = "refresh",
            RefreshTokenExpiresAtUtc = Now.AddDays(100),
            DefaultPaymentPolicyId = "PAY-1",
            DefaultFulfillmentPolicyId = "SHIP-1",
            DefaultReturnPolicyId = "RET-1",
            MerchantLocationKey = "hk-main",
        };
        configure?.Invoke(account);
        db.EbayAccounts.Add(account);
        db.SaveChanges();
    }

    private Guid SeedListable(Action<Product>? configure = null, int stock = 10, Guid? storeId = null)
    {
        var id = SeedProduct(p =>
        {
            p.Name = "Ceramic Brake Pad Set";
            p.Sku = $"BP-{Guid.NewGuid():N}"[..12];
            p.Price = 50m;
            p.IsPublished = true;
            p.FullDescription = "<p>Quiet, low-dust pads.</p>";
            configure?.Invoke(p);
        });
        SetStock(id, stock, storeId ?? _store);
        return id;
    }

    private void SetStock(Guid productId, int qty, Guid? storeId = null)
    {
        using var db = NewContext();
        var sid = storeId ?? _store;
        var level = db.InventoryLevels.FirstOrDefault(l => l.ProductId == productId && l.StoreId == sid && l.ProductVariantId == null);
        if (level is null)
            db.InventoryLevels.Add(new InventoryLevel { ProductId = productId, StoreId = sid, StockQuantity = qty });
        else
            level.StockQuantity = qty;
        db.SaveChanges();
    }

    private int Stock(Guid productId)
    {
        using var db = NewContext();
        return db.InventoryLevels.Where(l => l.ProductId == productId && l.StoreId == _store && l.ProductVariantId == null).Sum(l => l.StockQuantity);
    }

    private EbayListingService ListingService(AppDbContext db) =>
        new(db, _ebay, new EbayMetadataService(_ebay, _cache, db), new FakeUrls(), new PriceResolver(db, _clock), _clock,
            NullLogger<EbayListingService>.Instance);

    private Task<EbayListingDto> CreateDraft(Guid productId, string category = LeafCategory)
    {
        var db = NewContext();
        return new CreateEbayListingHandler(db, new PriceResolver(db, _clock), new FakeUrls(), new FakeCurrentUser(), _clock)
            .Handle(new CreateEbayListingCommand(_store, productId, category), default);
    }

    private Task<EbayListingDto> Save(EbayListingDto dto, EbayListingStatus status, List<EbayAspectInput>? aspects = null, string? title = null)
    {
        var db = NewContext();
        var handler = new SaveEbayListingHandler(db, ListingService(db), new EbayMetadataService(_ebay, _cache, db),
            new FakeUrls(), new FakeCurrentUser(), _clock);
        return handler.Handle(new SaveEbayListingCommand(
            _store, dto.Id, status, title ?? dto.Title, dto.Subtitle, dto.Sku, dto.Price, dto.Quantity, "1000",
            dto.Description ?? "<p>desc</p>", dto.PaymentPolicyId, dto.FulfillmentPolicyId, dto.ReturnPolicyId,
            aspects ?? new List<EbayAspectInput> { new("Brand", new List<string> { "Acme" }) }, dto.RowVersion), default);
    }

    private Task<EbayListingDto> Get(Guid id) =>
        new GetEbayListingHandler(NewContext(), new FakeUrls()).Handle(new GetEbayListingQuery(_store, id), default);

    private Task End(Guid id)
    {
        var db = NewContext();
        return new EndEbayListingHandler(db, ListingService(db), new FakeCurrentUser(), _clock)
            .Handle(new EndEbayListingCommand(_store, id), default);
    }

    private async Task<EbayListingDto> Published(Guid productId)
    {
        var draft = await CreateDraft(productId);
        return await Save(draft, EbayListingStatus.Active);
    }

    private bool ChannelEbay(Guid productId)
    {
        using var db = NewContext();
        return db.Products.Single(p => p.Id == productId).ChannelEbay;
    }

    // ---- 1–5: eligibility and drafts ----------------------------------------------------------------------

    [Fact]
    public async Task T01_step1_lists_only_eligible_products()
    {
        var simple = SeedListable(p => p.Name = "Eligible simple");
        var bundle = SeedListable(p => { p.Name = "Eligible bundle"; p.ProductType = ProductType.Grouped; });
        var variants = SeedListable(p => { p.Name = "Has variants"; p.ProductType = ProductType.WithVariants; });
        var unpublished = SeedListable(p => { p.Name = "Unpublished"; p.IsPublished = false; });
        var noStock = SeedListable(p => p.Name = "No stock", stock: 0);
        var forcedOut = SeedListable(p => { p.Name = "Forced out"; p.StockStatusOverride = StockStatus.OutOfStock; });
        var listed = SeedListable(p => p.Name = "Already listed");
        await CreateDraft(listed);

        var other = SeedStore("Other");
        var otherStoreOnly = SeedListable(p => p.Name = "Other store only", storeId: _store);
        using (var db = NewContext())
        {
            db.ProductStoreMappings.Add(new ProductStoreMapping { ProductId = otherStoreOnly, StoreId = other });
            db.SaveChanges();
        }

        var result = await new ListEligibleEbayProductsHandler(NewContext()).Handle(new ListEligibleEbayProductsQuery(_store, null), default);
        var ids = result.Select(r => r.Id).ToHashSet();

        Assert.Contains(simple, ids);
        Assert.Contains(bundle, ids);
        Assert.DoesNotContain(variants, ids);
        Assert.DoesNotContain(unpublished, ids);
        Assert.DoesNotContain(noStock, ids);
        Assert.DoesNotContain(forcedOut, ids);
        Assert.DoesNotContain(listed, ids);
        Assert.DoesNotContain(otherStoreOnly, ids);

        var searched = await new ListEligibleEbayProductsHandler(NewContext()).Handle(new ListEligibleEbayProductsQuery(_store, "bundle"), default);
        Assert.Equal(new[] { bundle }, searched.Select(r => r.Id));
    }

    [Fact]
    public async Task T02_already_listed_is_a_conflict()
    {
        var p = SeedListable();
        await CreateDraft(p);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => CreateDraft(p));
        Assert.Equal("This product is already listed on eBay", ex.Message);
    }

    [Fact]
    public async Task T03_out_of_stock_is_rejected()
    {
        var p = SeedListable(stock: 0);
        var ex = await Assert.ThrowsAsync<HKPartsEcommerce.Application.Common.Exceptions.ValidationException>(() => CreateDraft(p));
        Assert.Contains(ex.Errors.SelectMany(e => e.Value),
            m => m == "You can not list this product, as the inventory level is 0 or is listed as out of stock");
    }

    [Fact]
    public async Task T04_draft_is_prefilled()
    {
        var longName = new string('X', 70) + " brake pad set long";
        var p = SeedListable(product =>
        {
            product.Name = longName;
            product.Sku = "BP-1001";
            product.SalePrice = 45m; // the store final price (sale) — not the list price
        }, stock: 7);

        var draft = await CreateDraft(p);

        Assert.Equal(EbayListingStatus.Draft, draft.Status);
        Assert.Equal(80, draft.Title.Length);
        Assert.StartsWith(new string('X', 70), draft.Title);
        Assert.Equal("BP-1001", draft.Sku);
        Assert.Equal("<p>Quiet, low-dust pads.</p>", draft.Description);
        Assert.Equal(45m, draft.Price);
        Assert.Equal(7, draft.Quantity);
        Assert.Equal(("PAY-1", "SHIP-1", "RET-1"), (draft.PaymentPolicyId, draft.FulfillmentPolicyId, draft.ReturnPolicyId));
        Assert.True(ChannelEbay(p));
        Assert.Empty(_ebay.Calls);
    }

    [Fact]
    public async Task T05_non_leaf_category_cannot_be_selected()
    {
        var p = SeedListable();
        var validator = new CreateEbayListingValidator(NewContext());
        var bad = await validator.ValidateAsync(new CreateEbayListingCommand(_store, p, ParentCategory));
        var good = await validator.ValidateAsync(new CreateEbayListingCommand(_store, p, LeafCategory));
        Assert.Contains(bad.Errors, e => e.PropertyName == "CategoryId");
        Assert.True(good.IsValid);

        var settings = await new SaveEbaySettingsValidator(NewContext()).ValidateAsync(new SaveEbaySettingsCommand(
            _store, true, EbayEnvironment.Production, "EBAY_US", false, ParentCategory, null, null, null, false, false));
        Assert.Contains(settings.Errors, e => e.PropertyName == "DefaultCategoryId");
    }

    // ---- 6–10: save / publish / revise --------------------------------------------------------------------

    [Fact]
    public async Task T06_saving_a_draft_makes_no_ebay_calls()
    {
        var draft = await CreateDraft(SeedListable());
        var saved = await Save(draft, EbayListingStatus.Draft, title: "Edited title");
        Assert.Equal("Edited title", saved.Title);
        Assert.Equal(EbayListingStatus.Draft, saved.Status);
        Assert.Empty(_ebay.Calls);
    }

    [Fact]
    public async Task T07_first_active_save_publishes()
    {
        var p = SeedListable();
        using (var db = NewContext())
        {
            db.EbayAccounts.Single(a => a.StoreId == _store).FitmentEnabled = true;
            db.VehicleFitments.Add(new VehicleFitment { ProductId = p, Year = 2018, Make = "Honda", Model = "Civic" });
            db.SaveChanges();
        }
        _ebay.Compatibility = new EbayCompatibilityInfo(true, 3000);

        var published = await Published(p);

        Assert.Equal(new[]
        {
            nameof(IEbayClient.CreateOrReplaceInventoryItemAsync),
            nameof(IEbayClient.CreateOrReplaceProductCompatibilityAsync),
            nameof(IEbayClient.CreateOfferAsync),
            nameof(IEbayClient.PublishOfferAsync),
        }, _ebay.WriteCalls);
        Assert.Equal(EbayListingStatus.Active, published.Status);
        Assert.Equal("OFFER-1", published.OfferId);
        Assert.Equal("110551234567", published.EbayItemId);
        Assert.Equal("https://www.ebay.com/itm/110551234567", published.EbayItemUrl);
        Assert.NotNull(published.PublishedAtUtc);
        Assert.Equal(new[] { "Acme" }, _ebay.LastInventoryItem!.Aspects["Brand"]);
    }

    [Fact]
    public async Task T08_publish_error_is_422_with_errors_only_and_keeps_data()
    {
        var draft = await CreateDraft(SeedListable());
        _ebay.PublishError = new EbayApiException(400, new[]
        {
            new ExternalError("25007", "Please add at least one valid shipping service option."),
            new ExternalError("25021", "Item condition is required for this category.", "conditionId"),
        });

        var ex = await Assert.ThrowsAsync<ExternalBusinessException>(() => Save(draft, EbayListingStatus.Active, title: "My edited title"));

        Assert.Equal(SaveEbayListingHandler.VerificationErrorTitle, ex.Message);
        Assert.Equal(2, ex.Errors.Count);
        var after = await Get(draft.Id);
        Assert.Equal(EbayListingStatus.Error, after.Status);
        Assert.Equal("My edited title", after.Title);
        Assert.Contains("valid shipping service", after.LastError);
        Assert.Equal("OFFER-1", after.OfferId); // the offer survives, so a retry reuses it
    }

    [Fact]
    public async Task T08_retry_after_failed_publish_reuses_the_offer()
    {
        var draft = await CreateDraft(SeedListable());
        _ebay.PublishError = new EbayApiException(400, new[] { new ExternalError("1", "boom") });
        await Assert.ThrowsAsync<ExternalBusinessException>(() => Save(draft, EbayListingStatus.Active));

        _ebay.PublishError = null;
        _ebay.Calls.Clear();
        var retried = await Save(await Get(draft.Id), EbayListingStatus.Active);

        Assert.Equal(EbayListingStatus.Active, retried.Status);
        Assert.Null(retried.LastError);
        Assert.DoesNotContain(nameof(IEbayClient.CreateOfferAsync), _ebay.Calls);
        Assert.Contains(nameof(IEbayClient.UpdateOfferAsync), _ebay.Calls);
    }

    [Fact]
    public async Task T09_saving_a_published_listing_revises()
    {
        var published = await Published(SeedListable());
        _ebay.Calls.Clear();

        var revised = await Save(published, EbayListingStatus.Active, title: "Revised title");

        Assert.Equal(new[] { nameof(IEbayClient.CreateOrReplaceInventoryItemAsync), nameof(IEbayClient.UpdateOfferAsync) }, _ebay.WriteCalls);
        Assert.Equal("Revised title", revised.Title);
        Assert.NotNull(revised.LastSyncedAtUtc);
    }

    [Fact]
    public async Task T10_missing_required_aspect_is_400_before_any_ebay_write()
    {
        _ebay.Aspects = new List<EbayAspectDefinition>
        {
            new("Brand", true, false, true, new[] { "Acme", "Bosch" }),
            new("Placement on Vehicle", true, true, false, new[] { "Front", "Rear" }),
        };
        var draft = await CreateDraft(SeedListable());

        var ex = await Assert.ThrowsAsync<HKPartsEcommerce.Application.Common.Exceptions.ValidationException>(() =>
            Save(draft, EbayListingStatus.Active, new List<EbayAspectInput> { new("Brand", new List<string> { "Acme" }) }));

        Assert.Contains("Aspects[Placement on Vehicle]", ex.Errors.Keys);
        Assert.Empty(_ebay.WriteCalls);
    }

    // ---- 11–12: end ---------------------------------------------------------------------------------------

    [Fact]
    public async Task T11_ending_a_published_listing_withdraws_and_allows_relisting()
    {
        var p = SeedListable();
        var published = await Published(p);
        _ebay.Calls.Clear();

        await End(published.Id);

        Assert.Equal(new[] { nameof(IEbayClient.WithdrawOfferAsync) }, _ebay.Calls);
        var ended = await Get(published.Id);
        Assert.Equal(EbayListingStatus.Ended, ended.Status);
        Assert.NotNull(ended.EndedAtUtc);
        Assert.False(ChannelEbay(p));

        var list = await new ListEbayListingsHandler(NewContext()).Handle(new ListEbayListingsQuery(_store), default);
        Assert.DoesNotContain(list.Items, i => i.Id == published.Id);
        var withEnded = await new ListEbayListingsHandler(NewContext()).Handle(new ListEbayListingsQuery(_store, IncludeEnded: true), default);
        Assert.Contains(withEnded.Items, i => i.Id == published.Id);

        var relisted = await CreateDraft(p);
        Assert.NotEqual(published.Id, relisted.Id);
        Assert.True(ChannelEbay(p));
    }

    [Fact]
    public async Task T12_ending_a_draft_is_local_only()
    {
        var draft = await CreateDraft(SeedListable());
        await End(draft.Id);
        Assert.Empty(_ebay.Calls);
        Assert.Equal(EbayListingStatus.Ended, (await Get(draft.Id)).Status);
    }

    // ---- 13–14: inventory jobs ----------------------------------------------------------------------------

    [Fact]
    public async Task T13_stock_change_pushes_the_new_quantity()
    {
        var p = SeedListable(stock: 10);
        var published = await Published(p);
        Assert.Equal(10, published.Quantity);

        // The save interceptor raises the sync for the changed product...
        var jobs = new RecordingJobQueue();
        var options = new DbContextOptionsBuilder<AppDbContext>(_options).AddInterceptors(new EbayTriggerInterceptor(jobs)).Options;
        using (var db = new AppDbContext(options))
        {
            db.Database.UseTransaction(_transaction);
            db.InventoryLevels.Single(l => l.ProductId == p && l.StoreId == _store).StockQuantity = 7;
            await db.SaveChangesAsync();
        }

        Assert.Contains(jobs.InventorySyncs, s => s.StoreId == _store && s.Products!.Contains(p));

        // ...which pushes 7 to eBay.
        var result = await ListingService(NewContext()).SyncInventoryAsync(_store, new[] { p });
        Assert.Equal(1, result.Updated);
        var update = Assert.Single(Assert.Single(_ebay.BulkUpdates));
        Assert.Equal(7, update.Quantity);
        Assert.Null(update.Price); // price is not pushed unless SyncPriceFromStore
        Assert.Equal(7, (await Get(published.Id)).Quantity);

        // Nothing changed → nothing sent.
        await ListingService(NewContext()).SyncInventoryAsync(_store);
        Assert.Single(_ebay.BulkUpdates);
    }

    [Fact]
    public async Task T14_stock_zero_ends_the_listing_and_bundles_containing_it()
    {
        var part = SeedListable(stock: 3);
        var bundle = SeedListable(b => { b.Name = "Pad + rotor kit"; b.ProductType = ProductType.Grouped; }, stock: 5);
        using (var db = NewContext())
        {
            db.GroupedProductMembers.Add(new GroupedProductMember { GroupedProductId = bundle, MemberProductId = part });
            db.SaveChanges();
        }

        var partListing = await Published(part);
        var bundleListing = await Published(bundle);
        SetStock(part, 0);

        var ended = await ListingService(NewContext()).EndOutOfStockAsync(new[] { part });

        Assert.Equal(2, ended);
        Assert.Equal(EbayListingStatus.Ended, (await Get(partListing.Id)).Status);
        Assert.Equal(EbayListingStatus.Ended, (await Get(bundleListing.Id)).Status);
        Assert.False(ChannelEbay(part));
        Assert.False(ChannelEbay(bundle));
    }

    // ---- 15–17: order import ------------------------------------------------------------------------------

    private static EbayOrder Order(string id, string paymentStatus, params EbayOrderLineItem[] lines) => new(
        id, paymentStatus, Now.AddHours(-1), "buyer1", "buyer@example.com", "Please ship fast",
        new EbayAddress("Jane Q Public", "1 Main St", null, "Austin", "TX", "78701", "US", "555-0100"),
        "buyer@example.com", "USPSPriority",
        lines.Sum(l => l.LineItemCost), 9.95m, 4.00m, lines.Sum(l => l.LineItemCost) + 13.95m, "USD", "CREDIT_CARD", lines);

    private EbayOrderImportService ImportService()
    {
        var db = NewContext();
        return new EbayOrderImportService(db, _ebay, new InventoryService(db, new FakeAdminAlertService(), new FakeNoopPublisher()),
            _clock, NullLogger<EbayOrderImportService>.Instance);
    }

    [Fact]
    public async Task T15_T16_order_with_two_lines_imports_once()
    {
        var a = SeedListable(stock: 10);
        var b = SeedListable(stock: 10);
        var la = await Published(a);
        var lb = await Published(b);
        using (var db = NewContext())
        {
            // Distinct eBay item ids so lines match by listing id.
            db.EbayListings.Single(l => l.Id == lb.Id).EbayListingIdExternal = "110559999999";
            db.SaveChanges();
        }

        _ebay.Orders = new List<EbayOrder>
        {
            Order("12-34567-89012", "PAID",
                new EbayOrderLineItem("L1", "110551234567", la.Sku, "Pads A", 2, 79.98m, 39.99m),
                new EbayOrderLineItem("L2", "110559999999", lb.Sku, "Pads B", 1, 41.50m, 41.50m)),
        };

        var first = await ImportService().ImportAsync(_store);
        Assert.Equal(1, first.Imported);

        using (var db = NewContext())
        {
            var order = db.Orders.Include(o => o.Lines).Include(o => o.Payments).Include(o => o.ShippingAddress)
                .Single(o => o.EbayOrderId == "12-34567-89012");
            Assert.Equal(2, order.Lines.Count);
            Assert.Equal(39.99m, order.Lines.Single(l => l.ProductId == a).UnitPrice); // eBay's price, not the store's 50
            Assert.Equal(41.50m, order.Lines.Single(l => l.ProductId == b).UnitPrice);
            var payment = Assert.Single(order.Payments);
            Assert.Equal(PaymentMethodType.Ebay, payment.Method);
            Assert.Equal("eBay - CREDIT_CARD", payment.GatewayName);
            Assert.Equal(PaymentStatus.Captured, payment.Status);
            Assert.Equal(order.TotalAmount, payment.Amount);
            Assert.Equal("12-34567-89012", payment.TransactionId);
            Assert.Equal(OrderStatus.Processing, order.Status);
            Assert.Equal(("Jane", "Q Public"), (order.ShippingAddress!.FirstName, order.ShippingAddress.LastName));
            Assert.Equal("Please ship fast", order.EbayBuyerMessage);
            Assert.Equal(2, db.EbayListings.Single(l => l.Id == la.Id).QuantitySold);
            Assert.Equal(Now, db.EbayAccounts.Single(x => x.StoreId == _store).OrdersLastSyncedAtUtc);
        }

        Assert.Equal(8, Stock(a));
        Assert.Equal(9, Stock(b));

        // T16: a second run sees the same order and skips it.
        var second = await ImportService().ImportAsync(_store);
        Assert.Equal((0, 1), (second.Imported, second.AlreadyImported));
        using (var db = NewContext())
            Assert.Equal(1, db.Orders.Count(o => o.EbayOrderId == "12-34567-89012"));
        Assert.Equal(8, Stock(a));
    }

    [Fact]
    public async Task T17_unpaid_orders_and_unmatched_lines_are_skipped()
    {
        var a = SeedListable(stock: 10);
        var la = await Published(a);

        _ebay.Orders = new List<EbayOrder>
        {
            Order("UNPAID-1", "PENDING", new EbayOrderLineItem("U1", "110551234567", la.Sku, "Pads", 1, 39.99m, 39.99m)),
            Order("MIXED-1", "PAID",
                new EbayOrderLineItem("M1", "110551234567", la.Sku, "Pads", 1, 39.99m, 39.99m),
                new EbayOrderLineItem("M2", "999", "NOT-OURS", "Someone else's item", 1, 10m, 10m)),
        };

        var result = await ImportService().ImportAsync(_store);

        Assert.Equal((1, 1), (result.Imported, result.NotPaid));
        Assert.Contains(result.Messages, m => m.Contains("M2") && m.Contains("skipped"));
        using var db = NewContext();
        Assert.False(db.Orders.Any(o => o.EbayOrderId == "UNPAID-1"));
        Assert.Single(db.Orders.Include(o => o.Lines).Single(o => o.EbayOrderId == "MIXED-1").Lines);
    }

    [Fact]
    public async Task Order_import_pages_through_every_page_and_skips_sandbox_unless_forced()
    {
        var a = SeedListable(stock: 1000);
        var la = await Published(a);
        _ebay.OrderPageSize = 2;
        _ebay.Orders = Enumerable.Range(1, 5)
            .Select(i => Order($"PAGE-{i}", "PAID", new EbayOrderLineItem($"P{i}", "110551234567", la.Sku, "Pads", 1, 39.99m, 39.99m)))
            .ToList();

        Assert.Equal(5, (await ImportService().ImportAsync(_store)).Imported);
        Assert.Equal(3, _ebay.Calls.Count(c => c == nameof(IEbayClient.GetOrdersAsync)));

        using (var db = NewContext())
        {
            db.EbayAccounts.Single(x => x.StoreId == _store).Environment = EbayEnvironment.Sandbox;
            db.SaveChanges();
        }

        Assert.True((await ImportService().ImportAsync(_store)).Skipped);
        Assert.False((await ImportService().ImportAsync(_store, force: true)).Skipped);
    }

    // ---- 18: shipment sync --------------------------------------------------------------------------------

    [Fact]
    public async Task T18_tracking_is_reported_to_ebay_once()
    {
        var a = SeedListable(stock: 10);
        var la = await Published(a);
        _ebay.Orders = new List<EbayOrder>
        {
            Order("SHIP-1", "PAID", new EbayOrderLineItem("S1", "110551234567", la.Sku, "Pads", 2, 79.98m, 39.99m)),
        };
        await ImportService().ImportAsync(_store);

        Guid shipmentId;
        var jobs = new RecordingJobQueue();
        var options = new DbContextOptionsBuilder<AppDbContext>(_options).AddInterceptors(new EbayTriggerInterceptor(jobs)).Options;
        using (var db = new AppDbContext(options))
        {
            db.Database.UseTransaction(_transaction);
            var order = db.Orders.Single(o => o.EbayOrderId == "SHIP-1");
            var shipment = new Shipment { OrderId = order.Id, ShipmentNumber = "SH-1", Carrier = "FedEx", TrackingNumber = "7712 3456 7890" };
            db.Shipments.Add(shipment);
            await db.SaveChangesAsync();
            shipmentId = shipment.Id;
        }

        Assert.Equal(new[] { shipmentId }, jobs.Shipments);

        var sync = new EbayShipmentSyncService(NewContext(), _ebay, NullLogger<EbayShipmentSyncService>.Instance);
        Assert.True(await sync.SyncAsync(shipmentId));
        Assert.False(await new EbayShipmentSyncService(NewContext(), _ebay, NullLogger<EbayShipmentSyncService>.Instance).SyncAsync(shipmentId));

        var call = Assert.Single(_ebay.Fulfillments);
        Assert.Equal("SHIP-1", call.OrderId);
        Assert.Equal("FEDEX", call.Carrier);
        Assert.Equal(new[] { ("S1", 2) }, call.Lines);
    }

    // ---- 19: token refresh --------------------------------------------------------------------------------

    [Fact]
    public async Task T19_expiring_token_is_refreshed_and_failed_refresh_is_503()
    {
        using (var db = NewContext())
        {
            db.EbayAccounts.Single(a => a.StoreId == _store).AccessTokenExpiresAtUtc = Now.AddMinutes(3); // inside the 5-minute window
            db.SaveChanges();
        }

        var oauth = new FakeOAuthClient { Now = Now };
        var session = await new EbayAuthService(NewContext(), oauth, _clock, NullLogger<EbayAuthService>.Instance).GetSessionAsync(_store);
        Assert.Equal(1, oauth.RefreshCalls);
        Assert.Equal("refreshed-access", session.AccessToken);
        using (var db = NewContext())
            Assert.Equal(Now.AddHours(2), db.EbayAccounts.Single(a => a.StoreId == _store).AccessTokenExpiresAtUtc);

        // A fresh token is not refreshed again.
        await new EbayAuthService(NewContext(), oauth, _clock, NullLogger<EbayAuthService>.Instance).GetSessionAsync(_store);
        Assert.Equal(1, oauth.RefreshCalls);

        using (var db = NewContext())
        {
            db.EbayAccounts.Single(a => a.StoreId == _store).AccessTokenExpiresAtUtc = Now.AddMinutes(-1);
            db.SaveChanges();
        }

        var failing = new FakeOAuthClient { Now = Now, FailRefresh = true };
        var ex = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            new EbayAuthService(NewContext(), failing, _clock, NullLogger<EbayAuthService>.Instance).GetSessionAsync(_store));
        Assert.Equal("Reconnect eBay", ex.Message);
    }

    // ---- 20: API logging (real client over a stubbed transport) -------------------------------------------

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task T20_api_log_rows_are_written_only_when_enabled_and_redacted(bool loggingEnabled)
    {
        var handler = new StubHttpHandler(_ => Json(HttpStatusCode.OK,
            "{\"paymentPolicies\":[{\"paymentPolicyId\":\"P1\",\"name\":\"Pay\"}],\"token\":\"" + FakeAuth.Token + "\"}"));
        var sink = new RecordingLogSink();
        var client = new EbayClient(new HttpClient(handler), new FakeAuth { LoggingEnabled = loggingEnabled }, sink, _clock);

        var policies = await client.GetPoliciesAsync(_store, EbayPolicyType.Payment);
        Assert.Equal("P1", Assert.Single(policies).Id);
        Assert.Equal("Bearer", handler.Requests.Single().Headers.Authorization!.Scheme);

        if (!loggingEnabled)
        {
            Assert.Empty(sink.Rows);
            return;
        }

        var row = Assert.Single(sink.Rows);
        Assert.Equal("account.getpaymentPolicies", row.Operation);
        Assert.True(row.Success);
        Assert.Equal(200, row.ResponseStatus);
        Assert.DoesNotContain("SECRET-ACCESS-TOKEN", row.ResponseBody + row.RequestBody + row.Url);
        Assert.Contains("REDACTED", row.ResponseBody);
    }

    [Fact]
    public async Task Client_keeps_error_severity_messages_only()
    {
        var handler = new StubHttpHandler(_ => Json(HttpStatusCode.BadRequest, """
            {"errors":[{"errorId":25007,"category":"REQUEST","message":"Shipping","longMessage":"Please add a shipping service.","parameters":[{"name":"field","value":"fulfillmentPolicyId"}]},
                       {"errorId":21919303,"category":"WARNING","message":"This is just a warning"}],
             "warnings":[{"errorId":1,"message":"ignored"}]}
            """));
        var client = new EbayClient(new HttpClient(handler), new FakeAuth(), new RecordingLogSink(), _clock);

        var ex = await Assert.ThrowsAsync<EbayApiException>(() => client.PublishOfferAsync(_store, "OFFER-1"));

        var error = Assert.Single(ex.Errors);
        Assert.Equal(("25007", "Please add a shipping service.", "fulfillmentPolicyId"), (error.Code, error.Message, error.Field));
    }

    [Fact]
    public void Redactor_masks_tokens_in_json_forms_and_headers()
    {
        Assert.DoesNotContain("abc123", EbayRedactor.Redact("{\"access_token\":\"abc123\",\"x\":1}"));
        Assert.DoesNotContain("abc123", EbayRedactor.Redact("grant_type=refresh_token&refresh_token=abc123&scope=x"));
        Assert.DoesNotContain("abc123", EbayRedactor.Redact("Authorization: Bearer abc123"));
        Assert.Equal("{\"name\":\"ok\"}", EbayRedactor.Redact("{\"name\":\"ok\"}"));
    }

    // ---- OAuth state ----------------------------------------------------------------------------------------

    [Fact]
    public void Oauth_state_is_single_use_and_tamper_proof()
    {
        var protector = new EbayOAuthStateProtector(
            new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(), new MemoryCache(new MemoryCacheOptions()));
        var state = protector.Create(_store, Guid.NewGuid());

        Assert.Equal(_store, protector.Validate(state));
        Assert.Throws<ForbiddenAccessException>(() => protector.Validate(state));               // replay
        Assert.Throws<ForbiddenAccessException>(() => protector.Validate(state[..^4] + "AAAA")); // tamper
    }
}
