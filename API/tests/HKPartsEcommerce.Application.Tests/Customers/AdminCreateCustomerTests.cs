using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Application.Tests.Common;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

/// <summary>
/// Admin customer creation: the optional tax exempt reason is trimmed and persisted, and stays null
/// when omitted or blank. Backed by the real SQL Server test DB.
/// </summary>
public class AdminCreateCustomerTests : CatalogTestBase
{
    private static AdminCreateCustomerCommandHandler Handler(IApplicationDbContext db) =>
        new(db, new FakePasswordHasher(), new FakeJwtTokenService(), new FixedClock(DateTime.UtcNow),
            new FakeEmailTemplateSender(), new FakeUrlBuilder());

    private static AdminCreateCustomerCommand Command(string? taxExemptReason = null) =>
        new($"u{Guid.NewGuid():N}@test.local", "Passw0rd!x", "Ann", null, null,
            TaxExemptReason: taxExemptReason);

    [Fact]
    public async Task Create_persists_a_trimmed_tax_exempt_reason()
    {
        Guid id;
        using (var db = NewContext())
            id = await Handler(db).Handle(Command("  Reseller certificate  "), CancellationToken.None);

        using var check = NewContext();
        Assert.Equal("Reseller certificate", check.Customers.Single(c => c.Id == id).TaxExemptReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Create_stores_null_when_the_reason_is_blank(string? reason)
    {
        Guid id;
        using (var db = NewContext())
            id = await Handler(db).Handle(Command(reason), CancellationToken.None);

        using var check = NewContext();
        Assert.Null(check.Customers.Single(c => c.Id == id).TaxExemptReason);
    }
}
