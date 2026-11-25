using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Tax;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Tax;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Tax;

/// <summary>
/// PHP tax parity (legacy EcommTax): No Tax / By State / By ZIP methods, "Tax Shipping Amount", the customer group
/// tax-method override, plus the state-rate grid and ZIP import handlers, against the real SQL Server test DB.
/// </summary>
public class PhpTaxMethodTests : CatalogTestBase
{
    private static readonly TaxAddress Store = new("US", "Florida", "33101", "Miami");

    private void SeedConfig(TaxProviderType method, decimal flat = 0m, bool taxShipping = true, bool enabled = true)
    {
        using var db = NewContext();
        db.TaxProviderConfigurations.Add(new TaxProviderConfiguration
        {
            ActiveProvider = method,
            FlatRatePercent = flat,
            TaxShipping = taxShipping,
            IsEnabled = enabled,
            CacheTtlMinutes = 0,
        });
        db.SaveChanges();
    }

    private void SeedState(string code, string name, decimal? rate)
    {
        using var db = NewContext();
        db.TaxStateRates.Add(new TaxStateRate { CountryCode = "US", StateCode = code, StateName = name, RatePercent = rate });
        db.SaveChanges();
    }

    private void SeedZip(string zip, decimal rate)
    {
        using var db = NewContext();
        db.TaxZipRates.Add(new TaxZipRate { PostalCode = zip, RatePercent = rate });
        db.SaveChanges();
    }

    private Guid SeedGroup(TaxProviderType? method)
    {
        using var db = NewContext();
        var group = new CustomerGroup { Name = "Group " + Guid.NewGuid().ToString("N")[..6], TaxMethod = method };
        db.CustomerGroups.Add(group);
        db.SaveChanges();
        return group.Id;
    }

    private TaxCalculationService NewService() =>
        new(NewContext(), Array.Empty<ITaxProviderClient>(), new MemoryCache(new MemoryCacheOptions()),
            new FakeAdminAlertService(), NullLogger<TaxCalculationService>.Instance);

    private static TaxCalculationRequest Request(TaxAddress to, decimal amount, decimal shipping = 0m, Guid? groupId = null) =>
        new(Store, to, new() { new TaxLineInput(Guid.NewGuid(), null, amount, 1) }, shipping, CustomerGroupId: groupId);

    [Fact]
    public async Task None_ChargesNoTax()
    {
        SeedConfig(TaxProviderType.None, flat: 8m);

        var result = await NewService().CalculateAsync(Request(new TaxAddress("US", "Texas", "75001"), 100m, 10m));

        Assert.Equal(0m, result.TotalTax);
    }

    [Fact]
    public async Task ByState_UsesShipToStateRate_WhetherStoredAsNameOrCode()
    {
        SeedConfig(TaxProviderType.ByState);
        SeedState("TX", "Texas", 6.25m);

        var byName = await NewService().CalculateAsync(Request(new TaxAddress("US", "Texas", "75001"), 100m, 10m));
        var byCode = await NewService().CalculateAsync(Request(new TaxAddress("US", "tx", "75001"), 100m));

        Assert.Equal(6.88m, byName.TotalTax); // (100 + 10 shipping) * 6.25% = 6.875 → 6.88
        Assert.Equal(6.25m, byCode.TotalTax);
        Assert.Equal("State", byName.Jurisdictions.Single().Type);
    }

    [Fact]
    public async Task ByState_NoRateOnFile_ChargesNoTax()
    {
        SeedConfig(TaxProviderType.ByState);
        SeedState("OR", "Oregon", null);

        var blank = await NewService().CalculateAsync(Request(new TaxAddress("US", "Oregon", "97201"), 100m));
        var missing = await NewService().CalculateAsync(Request(new TaxAddress("US", "Nevada", "89501"), 100m));

        Assert.Equal(0m, blank.TotalTax);
        Assert.Equal(0m, missing.TotalTax);
    }

    [Fact]
    public async Task TaxShippingOff_LeavesShippingOutOfTheBase()
    {
        SeedConfig(TaxProviderType.ByState, taxShipping: false);
        SeedState("TX", "Texas", 6.25m);

        var result = await NewService().CalculateAsync(Request(new TaxAddress("US", "Texas", "75001"), 100m, 10m));

        Assert.Equal(6.25m, result.TotalTax);
    }

    [Fact]
    public async Task ByZip_MatchesFirstFiveCharactersOfShipToZip()
    {
        SeedConfig(TaxProviderType.ByZip);
        SeedZip("33101", 7m);

        var zipPlus4 = await NewService().CalculateAsync(Request(new TaxAddress("US", "Florida", " 33101-1234 "), 100m));
        var other = await NewService().CalculateAsync(Request(new TaxAddress("US", "Florida", "33102"), 100m));

        Assert.Equal(7.00m, zipPlus4.TotalTax);
        Assert.Equal(0m, other.TotalTax);
    }

    [Fact]
    public async Task GroupTaxExempt_OverridesStoreMethod()
    {
        SeedConfig(TaxProviderType.FlatRate, flat: 8m);
        var exemptGroup = SeedGroup(TaxProviderType.None);
        var defaultGroup = SeedGroup(null);

        var exempt = await NewService().CalculateAsync(Request(new TaxAddress("US", "Texas", "75001"), 100m, groupId: exemptGroup));
        var inherits = await NewService().CalculateAsync(Request(new TaxAddress("US", "Texas", "75001"), 100m, groupId: defaultGroup));

        Assert.Equal(0m, exempt.TotalTax);
        Assert.Equal(8.00m, inherits.TotalTax);
    }

    [Fact]
    public async Task GroupByState_OverridesStoreFlatRate()
    {
        SeedConfig(TaxProviderType.FlatRate, flat: 8m);
        SeedState("TX", "Texas", 6.25m);
        var group = SeedGroup(TaxProviderType.ByState);

        var result = await NewService().CalculateAsync(Request(new TaxAddress("US", "Texas", "75001"), 100m, groupId: group));

        Assert.Equal(6.25m, result.TotalTax);
    }

    [Fact]
    public async Task Disabled_ChargesNoTax()
    {
        SeedConfig(TaxProviderType.FlatRate, flat: 8m, enabled: false);

        var result = await NewService().CalculateAsync(Request(new TaxAddress("US", "Texas", "75001"), 100m));

        Assert.Equal(0m, result.TotalTax);
    }

    [Fact]
    public async Task Avalara_WithoutAClient_FallsBackToFlatRateAndFlagsIt()
    {
        SeedConfig(TaxProviderType.Avalara, flat: 5m);

        var result = await NewService().CalculateAsync(Request(new TaxAddress("US", "Texas", "75001"), 100m));

        Assert.True(result.FallbackApplied);
        Assert.Equal(5.00m, result.TotalTax);
    }

    [Fact]
    public async Task StateGrid_IsSeededWithEveryState_AndSaves()
    {
        using (var db = NewContext())
        {
            var list = await new ListTaxStateRatesQueryHandler(db).Handle(new ListTaxStateRatesQuery(), default);
            Assert.Contains(list, s => s.StateCode == "TX" && s.StateName == "Texas" && s.RatePercent == null);
            Assert.Equal(TaxStates.Us.Count, list.Count);
        }

        using (var db = NewContext())
        {
            var handler = new UpdateTaxStateRatesCommandHandler(db, new ListOnlyMediator(db));
            var saved = await handler.Handle(new UpdateTaxStateRatesCommand(new()
            {
                new TaxStateRateInput("TX", 6.25m),
                new TaxStateRateInput("FL", 0m),
            }), default);
            Assert.Equal(6.25m, saved.Single(s => s.StateCode == "TX").RatePercent);
            Assert.Null(saved.Single(s => s.StateCode == "FL").RatePercent); // 0 = no rate, like a blank
        }
    }

    [Fact]
    public async Task ZipImport_AddsUpdatesAndSkipsBadRows()
    {
        SeedZip("33101", 7m);
        using var db = NewContext();

        var result = await new ImportTaxZipRatesCommandHandler(db).Handle(
            new ImportTaxZipRatesCommand("zip,rate\n33101,7.5\n75001,8.25%\n,3\n90210,abc\n"), default);

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Updated);
        Assert.Equal(2, result.Skipped);
        using var check = NewContext();
        Assert.Equal(7.5m, await check.TaxZipRates.Where(r => r.PostalCode == "33101").Select(r => r.RatePercent).SingleAsync());
        Assert.Equal(8.25m, await check.TaxZipRates.Where(r => r.PostalCode == "75001").Select(r => r.RatePercent).SingleAsync());
    }

    /// <summary>Serves only the list query the state-rate update re-reads with.</summary>
    private sealed class ListOnlyMediator : IMediator
    {
        private readonly IApplicationDbContext _db;
        public ListOnlyMediator(IApplicationDbContext db) => _db = db;

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is ListTaxStateRatesQuery q)
                return (TResponse)(object)await new ListTaxStateRatesQueryHandler(_db).Handle(q, cancellationToken);
            throw new NotSupportedException(request.GetType().Name);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
