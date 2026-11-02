using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.InventoryScanner;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Inventory;
using HKPartsEcommerce.Infrastructure.Persistence;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.InventoryScanner;

/// <summary>
/// Barcode inventory scanner — lookup, update (concurrency, permissions, audit) and history, against the
/// SQL Server test database (spec §11 backend cases 1–12).
/// </summary>
public class InventoryScannerTests : CatalogTestBase
{
    // ---- Seed helpers ---------------------------------------------------------------------------

    private Guid SeedStore(string name = "Main")
    {
        using var db = NewContext();
        var store = new Store { Name = name, IsEnabled = true, ContactEmail = "store@example.com" };
        db.Stores.Add(store);
        db.SaveChanges();
        return store.Id;
    }

    private void SeedStock(Guid productId, Guid storeId, int qty, Guid? variantId = null)
    {
        using var db = NewContext();
        db.InventoryLevels.Add(new InventoryLevel { ProductId = productId, ProductVariantId = variantId, StoreId = storeId, StockQuantity = qty });
        db.SaveChanges();
    }

    /// <summary>A published simple product with 25 in stock at the (only) store, price 49.99.</summary>
    private (Guid ProductId, Guid StoreId) SeedScannable(string sku = "HK-TEST-001", string? gtin = "012345678905", int qty = 25, decimal price = 49.99m)
    {
        var storeId = SeedStore();
        var productId = SeedProduct(p =>
        {
            p.Name = "Test Brake Pad Set";
            p.Sku = sku;
            p.Gtin = gtin;
            p.Price = price;
            p.Msrp = 59.99m;
            p.IsPublished = true;
        });
        SeedStock(productId, storeId, qty);
        return (productId, storeId);
    }

    private async Task<ScannerLookupResult> LookupResultAsync(string code)
    {
        await using var db = NewContext();
        return await new LookupScannedProductQueryHandler(db).Handle(new LookupScannedProductQuery(code), CancellationToken.None);
    }

    /// <summary>The single matched product (fails the test when there isn't exactly one).</summary>
    private async Task<ScannedProductDto> LookupAsync(string code)
    {
        var result = await LookupResultAsync(code);
        Assert.True(result.Found, result.Message);
        return result.Product!;
    }

    private async Task<InventoryScanLogDto> UpdateAsync(UpdateScannedInventoryCommand command, AppDbContext? db = null)
    {
        await using var ctx = db ?? NewContext();
        var handler = new UpdateScannedInventoryCommandHandler(ctx,
            new InventoryService(ctx, new FakeAdminAlertService(), new FakeNoopPublisher()),
            NullLogger<UpdateScannedInventoryCommandHandler>.Instance);
        return await handler.Handle(command, CancellationToken.None);
    }

    private static UpdateScannedInventoryCommand Cmd(Guid productId, int expectedQty, decimal expectedPrice, int newQty,
        decimal? newPrice = null, bool canChangePrice = true) =>
        new(productId, null, expectedQty, expectedPrice, newQty, newPrice, "Cycle count", "HK-TEST-001",
            InventoryScanSource.Manual, InventoryMatchType.Sku)
        { CanChangePrice = canChangePrice, ClientIp = "10.0.0.5", UserAgent = "xunit" };

    private async Task<(int Qty, decimal? Price, int Logs)> StateAsync(Guid productId)
    {
        await using var db = NewContext();
        var qty = await db.InventoryLevels.Where(l => l.ProductId == productId).SumAsync(l => l.StockQuantity);
        var price = await db.Products.Where(p => p.Id == productId).Select(p => p.Price).SingleAsync();
        var logs = await db.InventoryAuditLogs.CountAsync(l => l.ProductId == productId);
        return (qty, price, logs);
    }

    // ---- Lookup (cases 1–4) ---------------------------------------------------------------------

    [Fact]
    public async Task Lookup_by_sku_matches_by_sku()
    {
        var (productId, _) = SeedScannable();

        var dto = await LookupAsync("HK-TEST-001");

        Assert.Equal(productId, dto.ProductId);
        Assert.Equal(InventoryMatchType.Sku, dto.MatchedBy);
        Assert.Equal(25, dto.QtyInStock);
        Assert.Equal(49.99m, dto.Price);
        Assert.True(dto.CanEditQty);
    }

    [Fact]
    public async Task Lookup_by_barcode_matches_by_barcode()
    {
        var (productId, _) = SeedScannable();

        var dto = await LookupAsync("012345678905");

        Assert.Equal(productId, dto.ProductId);
        Assert.Equal(InventoryMatchType.Barcode, dto.MatchedBy);
    }

    [Fact]
    public async Task Lookup_trims_and_is_case_insensitive()
    {
        var (productId, _) = SeedScannable();

        Assert.Equal(productId, (await LookupAsync("  hk-test-001 ")).ProductId);
    }

    [Fact]
    public async Task Lookup_handles_code39_characters()
    {
        var (productId, _) = SeedScannable(sku: "AB/12+3 %X", gtin: null);

        Assert.Equal(productId, (await LookupAsync("AB/12+3 %X")).ProductId);
    }

    [Fact]
    public async Task Lookup_retries_upc_a_as_ean13()
    {
        var (productId, _) = SeedScannable(gtin: "0012345678905");

        var dto = await LookupAsync("012345678905");

        Assert.Equal(productId, dto.ProductId);
        Assert.Equal(InventoryMatchType.Barcode, dto.MatchedBy);
    }

    [Fact]
    public async Task Lookup_unknown_code_is_not_found()
    {
        SeedScannable();

        // Returned, not thrown: no product, no candidates, a message for the 404 body.
        var result = await LookupResultAsync("NOPE-404");
        Assert.False(result.Found);
        Assert.False(result.IsAmbiguous);
        Assert.Contains("NOPE-404", result.Message);
    }

    [Fact]
    public async Task Lookup_duplicate_barcode_conflicts_with_both_candidates()
    {
        SeedScannable(sku: "A-1", gtin: "4006381333931");
        SeedProduct(p => { p.Name = "Twin"; p.Sku = "A-2"; p.Gtin = "4006381333931"; p.Price = 1m; });

        var result = await LookupResultAsync("4006381333931");

        Assert.False(result.Found);
        Assert.True(result.IsAmbiguous);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, c => Assert.Equal(InventoryMatchType.Barcode, c.MatchedBy));
    }

    [Fact]
    public async Task Lookup_same_code_as_sku_and_gtin_on_one_product_is_one_match()
    {
        var (productId, _) = SeedScannable(sku: "SAME-1", gtin: "SAME-1");

        var dto = await LookupAsync("SAME-1");

        Assert.Equal(productId, dto.ProductId);
        Assert.Equal(InventoryMatchType.Sku, dto.MatchedBy);
    }

    [Fact]
    public async Task Update_qty_only_writes_stock_and_qty_audit_row()
    {
        var (productId, storeId) = SeedScannable();

        var log = await UpdateAsync(Cmd(productId, 25, 49.99m, newQty: 30));

        Assert.Equal(InventoryAuditAction.QtyUpdate, log.Action);
        Assert.Equal(25, log.OldQty);
        Assert.Equal(30, log.NewQty);
        Assert.Equal(5, log.QtyChange);
        Assert.Equal(49.99m, log.NewPrice);
        Assert.NotEqual(Guid.Empty, log.Id);

        var (qty, price, logs) = await StateAsync(productId);
        Assert.Equal(30, qty);
        Assert.Equal(49.99m, price);
        Assert.Equal(1, logs);

        await using var db = NewContext();
        var row = await db.InventoryAuditLogs.SingleAsync(l => l.Id == log.Id);
        Assert.Equal(storeId, row.StoreId);
        Assert.Equal(InventoryScanSource.Manual, row.Source);
        Assert.Equal(InventoryMatchType.Sku, row.MatchedBy);
        Assert.Equal("10.0.0.5", row.ClientIp);
        Assert.Equal("xunit", row.UserAgent);
        Assert.Equal("Cycle count", row.Notes);
    }

    [Fact]
    public async Task Update_price_only_is_price_update()
    {
        var (productId, _) = SeedScannable();

        var log = await UpdateAsync(Cmd(productId, 25, 49.99m, newQty: 25, newPrice: 45m));

        Assert.Equal(InventoryAuditAction.PriceUpdate, log.Action);
        Assert.Equal(0, log.QtyChange);
        var (qty, price, _) = await StateAsync(productId);
        Assert.Equal(25, qty);
        Assert.Equal(45m, price);
    }

    [Fact]
    public async Task Update_both_is_both()
    {
        var (productId, _) = SeedScannable();

        var log = await UpdateAsync(Cmd(productId, 25, 49.99m, newQty: 20, newPrice: 45m));

        Assert.Equal(InventoryAuditAction.Both, log.Action);
        Assert.Equal(-5, log.QtyChange);
    }

    [Fact]
    public async Task Update_with_no_change_is_rejected_without_audit_row()
    {
        var (productId, _) = SeedScannable();

        await Assert.ThrowsAsync<ValidationException>(() => UpdateAsync(Cmd(productId, 25, 49.99m, newQty: 25, newPrice: 49.99m)));
        await Assert.ThrowsAsync<ValidationException>(() => UpdateAsync(Cmd(productId, 25, 49.99m, newQty: 25)));

        Assert.Equal(0, (await StateAsync(productId)).Logs);
    }

    [Fact]
    public async Task Update_with_stale_expected_qty_conflicts_and_writes_nothing()
    {
        var (productId, _) = SeedScannable();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => UpdateAsync(Cmd(productId, expectedQty: 20, 49.99m, newQty: 30)));

        Assert.Equal(25, ex.Extensions!["currentQty"]);
        Assert.Equal(49.99m, ex.Extensions["currentPrice"]);
        var (qty, price, logs) = await StateAsync(productId);
        Assert.Equal(25, qty);
        Assert.Equal(49.99m, price);
        Assert.Equal(0, logs);
    }

    [Fact]
    public async Task Update_with_stale_expected_price_conflicts()
    {
        var (productId, _) = SeedScannable();

        await Assert.ThrowsAsync<ConflictException>(() => UpdateAsync(Cmd(productId, 25, expectedPrice: 40m, newQty: 30)));
    }

    [Fact]
    public async Task Price_change_without_catalog_access_is_forbidden_but_qty_change_is_allowed()
    {
        var (productId, _) = SeedScannable();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            UpdateAsync(Cmd(productId, 25, 49.99m, newQty: 25, newPrice: 10m, canChangePrice: false)));
        Assert.Equal(49.99m, (await StateAsync(productId)).Price);

        var log = await UpdateAsync(Cmd(productId, 25, 49.99m, newQty: 26, canChangePrice: false));
        Assert.Equal(InventoryAuditAction.QtyUpdate, log.Action);
    }

    [Fact]
    public async Task Update_unknown_product_is_not_found()
    {
        SeedStore();

        await Assert.ThrowsAsync<NotFoundException>(() => UpdateAsync(Cmd(Guid.NewGuid(), 0, 0m, newQty: 1)));
    }

    [Fact]
    public async Task Audit_user_comes_from_the_authenticated_caller()
    {
        var (productId, _) = SeedScannable();
        var userId = Guid.NewGuid();
        var db = new AppDbContext(_options, new FakeCurrentUser { UserId = userId });
        db.Database.UseTransaction(_transaction);

        var log = await UpdateAsync(Cmd(productId, 25, 49.99m, newQty: 24), db);

        await using var check = NewContext();
        Assert.Equal(userId, (await check.InventoryAuditLogs.SingleAsync(l => l.Id == log.Id)).CreatedById);
    }

    [Fact]
    public async Task Qty_change_on_variant_parent_is_rejected()
    {
        var storeId = SeedStore();
        var productId = SeedProduct(p => { p.Sku = "PARENT-1"; p.Price = 5m; p.ProductType = ProductType.WithVariants; });
        _ = storeId;

        var dto = await LookupAsync("PARENT-1");
        Assert.False(dto.CanEditQty);

        await Assert.ThrowsAsync<ValidationException>(() => UpdateAsync(Cmd(productId, 0, 5m, newQty: 3)));
    }

    [Fact]
    public async Task Variant_scan_updates_variant_stock_and_price()
    {
        var storeId = SeedStore();
        var productId = SeedProduct(p => { p.Sku = "PARENT-2"; p.Price = 5m; p.ProductType = ProductType.WithVariants; });
        var variantId = SeedVariant(productId, v => { v.Sku = "PARENT-2-RED"; v.Gtin = "4006381333948"; });
        SeedStock(productId, storeId, 4, variantId);

        var dto = await LookupAsync("4006381333948");
        Assert.Equal(variantId, dto.ProductVariantId);
        Assert.Equal(5m, dto.Price); // inherits the product price

        var log = await UpdateAsync(new UpdateScannedInventoryCommand(productId, variantId, 4, 5m, 9, 6.5m) { CanChangePrice = true });

        Assert.Equal(InventoryAuditAction.Both, log.Action);
        await using var db = NewContext();
        Assert.Equal(9, await db.InventoryLevels.Where(l => l.ProductVariantId == variantId).SumAsync(l => l.StockQuantity));
        Assert.Equal(6.5m, await db.ProductVariants.Where(v => v.Id == variantId).Select(v => v.Price).SingleAsync());
        Assert.Equal(5m, await db.Products.Where(p => p.Id == productId).Select(p => p.Price).SingleAsync());
    }

    // ---- History / log --------------------------------------------------------------------------

    [Fact]
    public async Task History_is_paged_newest_first_and_log_reads_back_by_id()
    {
        var (productId, _) = SeedScannable();
        var first = await UpdateAsync(Cmd(productId, 25, 49.99m, newQty: 26));
        var second = await UpdateAsync(Cmd(productId, 26, 49.99m, newQty: 27));

        await using var db = NewContext();
        var page = await new GetInventoryScanHistoryQueryHandler(db)
            .Handle(new GetInventoryScanHistoryQuery(productId, null, 1, 1), CancellationToken.None);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(second.Id, Assert.Single(page.Items).Id);

        var log = await new GetInventoryScanLogQueryHandler(db).Handle(new GetInventoryScanLogQuery(first.Id), CancellationToken.None);
        Assert.Equal(26, log.NewQty);
        Assert.Equal(27, log.Product!.QtyInStock);
    }
}

/// <summary>
/// Spec §11 case 10: two staff saving the same item from the same starting values — exactly one wins and
/// the other gets 409. Needs real commits on two connections, so it seeds committed rows and cleans up.
/// </summary>
public class InventoryScannerConcurrencyTests : CatalogTestBase
{
    [Fact]
    public async Task Two_concurrent_updates_with_same_expected_values_one_succeeds_one_conflicts()
    {
        EnsureDatabaseCreated();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(TestConnectionString).Options;
        var sku = "RACE-" + Guid.NewGuid().ToString("n")[..8];

        Guid productId, storeId, taxId;
        await using (var seed = new AppDbContext(options))
        {
            var tax = new TaxCategory { Name = "Race tax " + sku };
            var store = new Store { Name = "  Race store " + sku, IsEnabled = true, ContactEmail = "race@example.com" };
            seed.TaxCategories.Add(tax);
            seed.Stores.Add(store);
            await seed.SaveChangesAsync();
            var product = new Product { Name = "Race", Sku = sku, Price = 10m, ProductType = ProductType.Simple, TaxCategoryId = tax.Id };
            seed.Products.Add(product);
            await seed.SaveChangesAsync();
            seed.InventoryLevels.Add(new InventoryLevel { ProductId = product.Id, StoreId = store.Id, StockQuantity = 10 });
            await seed.SaveChangesAsync();
            (productId, storeId, taxId) = (product.Id, store.Id, tax.Id);
        }

        try
        {
            // The race store must be the scanner's primary store (first by name): the leading spaces sort it
            // first, and the per-run test database holds no other committed stores.
            await using (var check = new AppDbContext(options))
                Assert.Equal(storeId, await check.Stores.OrderBy(s => s.Name).Select(s => s.Id).FirstAsync());

            async Task<Exception?> Run(int newQty)
            {
                await using var db = new AppDbContext(options);
                var handler = new UpdateScannedInventoryCommandHandler(db,
                    new InventoryService(db, new FakeAdminAlertService(), new FakeNoopPublisher()),
                    NullLogger<UpdateScannedInventoryCommandHandler>.Instance);
                try
                {
                    await handler.Handle(new UpdateScannedInventoryCommand(productId, null, 10, 10m, newQty), CancellationToken.None);
                    return null;
                }
                catch (Exception ex) { return ex; }
            }

            var results = await Task.WhenAll(Run(11), Run(12));

            Assert.Single(results, r => r is null);
            Assert.Single(results, r => r is ConflictException);
            await using var verify = new AppDbContext(options);
            Assert.Equal(1, await verify.InventoryAuditLogs.CountAsync(l => l.ProductId == productId));
        }
        finally
        {
            await using var conn = new SqlConnection(TestConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
DELETE FROM InventoryAuditLogs WHERE ProductId = @p;
DELETE FROM InventoryLevels WHERE ProductId = @p;
DELETE FROM Products WHERE Id = @p;
DELETE FROM Stores WHERE Id = @s;
DELETE FROM TaxCategories WHERE Id = @t;";
            cmd.Parameters.AddWithValue("@p", productId);
            cmd.Parameters.AddWithValue("@s", storeId);
            cmd.Parameters.AddWithValue("@t", taxId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

/// <summary>Pure rules: validation (spec §8) and the business-day window (case 13). No database.</summary>
public class InventoryScannerRuleTests
{
    private static readonly UpdateScannedInventoryCommandValidator Validator = new();

    private static UpdateScannedInventoryCommand Valid() => new(Guid.NewGuid(), null, 5, 10m, 6);

    [Fact]
    public void Valid_command_passes() => Assert.True(Validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    public void Qty_out_of_range_fails(int qty) => Assert.False(Validator.Validate(Valid() with { NewQty = qty }).IsValid);

    [Fact]
    public void Negative_price_fails() => Assert.False(Validator.Validate(Valid() with { NewPrice = -0.01m }).IsValid);

    [Fact]
    public void Notes_over_500_fail() => Assert.False(Validator.Validate(Valid() with { Notes = new string('x', 501) }).IsValid);

    [Fact]
    public void Unknown_source_fails() => Assert.False(Validator.Validate(Valid() with { Source = (InventoryScanSource)99 }).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_lookup_code_fails(string code) =>
        Assert.False(new LookupScannedProductQueryValidator().Validate(new LookupScannedProductQuery(code)).IsValid);

    [Fact]
    public void Lookup_code_over_100_fails() =>
        Assert.False(new LookupScannedProductQueryValidator().Validate(new LookupScannedProductQuery(new string('1', 101))).IsValid);

    [Fact]
    public void Business_day_excludes_late_last_night_in_the_business_zone()
    {
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        // 2026-10-31 10:00 New York (EDT, UTC-4) = 14:00 UTC.
        var (start, end) = GetInventoryScannerDashboardQueryHandler.BusinessDayUtc(new DateTime(2026, 10, 31, 14, 0, 0, DateTimeKind.Utc), ny);

        Assert.Equal(new DateTime(2026, 10, 31, 4, 0, 0), start);
        Assert.Equal(new DateTime(2026, 11, 1, 4, 0, 0), end);
        // 23:30 yesterday New York = 03:30 UTC today: before the window although it's "today" in UTC.
        Assert.True(new DateTime(2026, 10, 31, 3, 30, 0) < start);
    }

    [Fact]
    public void Business_day_spans_25_hours_on_dst_fall_back()
    {
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var (start, end) = GetInventoryScannerDashboardQueryHandler.BusinessDayUtc(new DateTime(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc), ny);

        Assert.Equal(TimeSpan.FromHours(25), end - start);
    }
}
