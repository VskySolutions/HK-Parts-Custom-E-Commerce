using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Tax;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Tax;

/// <summary>
/// Flat-rate tax calculation (REQ-TAX-001) exercised against the real SQL Server test database. Locks in
/// the taxable-base rules the QA/UAT scenarios depend on: tax on the discounted merchandise value
/// (Scenarios 2 &amp; 3), shipping included in the base, and zero tax for an exempt customer (Scenario 7).
/// The FlatRate provider needs no external client, so this runs fully offline.
/// </summary>
public class TaxCalculationServiceTests : CatalogTestBase
{
    private static readonly TaxAddress Origin = new("US", "Florida", "33101", "Miami");
    private static readonly TaxAddress Destination = new("US", "Florida", "33101", "Miami");

    private void SeedFlatRate(decimal percent)
    {
        using var db = NewContext();
        db.TaxProviderConfigurations.Add(new TaxProviderConfiguration
        {
            ActiveProvider = TaxProviderType.FlatRate,
            FlatRatePercent = percent,
            CacheTtlMinutes = 0,
        });
        db.SaveChanges();
    }

    private TaxCalculationService NewService() =>
        new(NewContext(), Array.Empty<ITaxProviderClient>(), new MemoryCache(new MemoryCacheOptions()),
            new FakeAdminAlertService(), NullLogger<TaxCalculationService>.Instance);

    private static TaxLineInput Line(decimal amount, int quantity = 1, decimal discount = 0m) =>
        new(Guid.NewGuid(), TaxCategoryCode: null, amount, quantity, discount);

    [Fact]
    public async Task FlatRate_TaxesSubtotal_WhenNoDiscount()
    {
        SeedFlatRate(8m);
        var req = new TaxCalculationRequest(Origin, Destination, new() { Line(100m) }, 0m);

        var result = await NewService().CalculateAsync(req);

        Assert.Equal(8.00m, result.TotalTax); // 100 * 8%
    }

    [Fact]
    public async Task FlatRate_ProductDiscount_ReducesTaxableAmount()
    {
        SeedFlatRate(8m);
        // $100 line with $20 of discount allocated → taxable $80.
        var req = new TaxCalculationRequest(Origin, Destination, new() { Line(100m, 1, 20m) }, 0m);

        var result = await NewService().CalculateAsync(req);

        Assert.Equal(6.40m, result.TotalTax); // 80 * 8%
    }

    [Fact]
    public async Task FlatRate_TaxesNetOfAllocatedDiscounts_AcrossMultipleLines()
    {
        SeedFlatRate(8m);
        // $80 + $40 merchandise, a coupon allocated $15 + $5 across the two lines → taxable $100.
        var req = new TaxCalculationRequest(Origin, Destination,
            new() { Line(80m, 1, 15m), Line(40m, 1, 5m) }, 0m);

        var result = await NewService().CalculateAsync(req);

        Assert.Equal(8.00m, result.TotalTax); // (80-15)+(40-5)=100 → 8%
    }

    [Fact]
    public async Task FlatRate_IncludesShipping_InTaxableBase()
    {
        SeedFlatRate(8m);
        var req = new TaxCalculationRequest(Origin, Destination, new() { Line(100m) }, 10m);

        var result = await NewService().CalculateAsync(req);

        Assert.Equal(8.80m, result.TotalTax); // (100 + 10) * 8%
    }

    [Fact]
    public async Task ExemptCustomer_PaysNoTax()
    {
        SeedFlatRate(8m);
        var req = new TaxCalculationRequest(Origin, Destination, new() { Line(100m) }, 10m,
            new TaxExemption(IsExempt: true));

        var result = await NewService().CalculateAsync(req);

        Assert.Equal(0m, result.TotalTax);
    }
}
