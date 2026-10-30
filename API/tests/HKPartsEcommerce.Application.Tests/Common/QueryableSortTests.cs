using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Domain.Entities;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Common;

/// <summary>
/// Ordering guarantees of <see cref="QueryableSortExtensions.ApplySort"/>. The load-bearing one is the Id
/// tiebreaker: paging is OFFSET/FETCH over the ordering, so without a total order a row tied on the sort
/// key can appear on two pages or on none.
/// </summary>
public class QueryableSortTests : CatalogTestBase
{
    private static readonly IReadOnlyDictionary<string, string> SortMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["name"] = "Name" };

    /// <summary>Seeds categories that all tie on the sort key, leaving only the tiebreaker to order them.</summary>
    private void SeedTied(int count, string name = "Tied", int displayOrder = 0)
    {
        using var db = NewContext();
        for (var i = 0; i < count; i++)
            db.Categories.Add(new Category { Name = name, DisplayOrder = displayOrder });
        db.SaveChanges();
    }

    [Fact]
    public void The_id_tiebreaker_reaches_the_sql()
    {
        using var db = NewContext();
        var sql = db.Categories
            .ApplySort(null, false, SortMap, defaultSort: q => q.OrderBy(c => c.Name))
            .ToQueryString();

        // Asserted on the SQL rather than on returned ids: SQL Server orders uniqueidentifier by a byte
        // order of its own, which .NET's Guid comparison does not reproduce — so a client-side sort is not
        // a valid oracle here. What matters is that Id is in the ORDER BY at all, making it a total order.
        var orderBy = sql[sql.LastIndexOf("ORDER BY", StringComparison.Ordinal)..];
        Assert.Contains("[Id]", orderBy, StringComparison.Ordinal);
    }

    [Fact]
    public void Tied_rows_come_back_in_the_same_order_every_time()
    {
        SeedTied(10);

        using var db = NewContext();
        var query = db.Categories
            .ApplySort(null, false, SortMap, defaultSort: q => q.OrderBy(c => c.Name))
            .Select(c => c.Id);

        Assert.Equal(query.ToList(), query.ToList());
    }

    [Fact]
    public void Paging_over_tied_rows_never_duplicates_or_skips()
    {
        // The regression this guards: 10 rows sharing a sort key, paged 5 at a time. With no tiebreaker
        // SQL Server may order the two OFFSET/FETCH windows differently, so a row lands on both pages and
        // another on neither.
        SeedTied(10);

        using var db = NewContext();
        var query = db.Categories.ApplySort(null, false, SortMap, defaultSort: q => q.OrderBy(c => c.Name));

        var page1 = query.Skip(0).Take(5).Select(c => c.Id).ToList();
        var page2 = query.Skip(5).Take(5).Select(c => c.Id).ToList();
        var all = page1.Concat(page2).ToList();

        Assert.Equal(10, all.Distinct().Count());
        Assert.Empty(page1.Intersect(page2));
    }

    [Fact]
    public void Default_sort_is_used_when_sort_by_is_absent()
    {
        using (var db = NewContext())
        {
            db.Categories.Add(new Category { Name = "Bravo", DisplayOrder = 1 });
            db.Categories.Add(new Category { Name = "Alpha", DisplayOrder = 1 });
            db.Categories.Add(new Category { Name = "Zulu", DisplayOrder = 0 });
            db.SaveChanges();
        }

        using var read = NewContext();
        var names = read.Categories
            .ApplySort(null, false, SortMap, defaultSort: q => q.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name))
            .Select(c => c.Name)
            .ToList();

        Assert.Equal(new[] { "Zulu", "Alpha", "Bravo" }, names);
    }

    [Fact]
    public void An_unrecognised_sort_by_falls_back_to_the_default_rather_than_throwing()
    {
        SeedTied(1, name: "Only");

        using var db = NewContext();
        var names = db.Categories
            .ApplySort("definitely-not-a-column", false, SortMap, defaultSort: q => q.OrderBy(c => c.Name))
            .Select(c => c.Name)
            .ToList();

        Assert.Equal(new[] { "Only" }, names);
    }

    [Fact]
    public void An_allow_listed_sort_by_wins_over_the_default()
    {
        using (var db = NewContext())
        {
            db.Categories.Add(new Category { Name = "Alpha", DisplayOrder = 9 });
            db.Categories.Add(new Category { Name = "Zulu", DisplayOrder = 0 });
            db.SaveChanges();
        }

        using var read = NewContext();
        var names = read.Categories
            .ApplySort("name", descending: true, SortMap, defaultSort: q => q.OrderBy(c => c.DisplayOrder))
            .Select(c => c.Name)
            .ToList();

        Assert.Equal(new[] { "Zulu", "Alpha" }, names);
    }
}
