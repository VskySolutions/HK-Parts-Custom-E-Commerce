using System.Linq.Expressions;

namespace HKPartsEcommerce.Application.Common.Extensions;

/// <summary>
/// Server-side sorting helpers for list endpoints. <see cref="ApplySort"/> keeps every admin list
/// consistent: a caller-supplied <c>sortBy</c> is honoured only when it maps to an allow-listed property
/// (guarding against arbitrary/invalid columns), otherwise the list falls back to the caller's default.
/// Ordering is built as a normal, typed <c>OrderBy</c> expression so EF Core translates it to SQL exactly
/// as a hand-written one would.
///
/// Every ordering is finished with an <c>Id</c> tiebreaker, which makes it a total order. Without one, rows
/// tied on the sort key come back in whatever order SQL Server happens to produce — and because paging is
/// OFFSET/FETCH over that ordering, a tied row can appear on two pages or on none.
/// </summary>
public static class QueryableSortExtensions
{
    private const string TiebreakerProperty = "Id";

    /// <summary>
    /// Orders by the mapped property for <paramref name="sortBy"/> (from <paramref name="allowed"/>). When
    /// it is absent or unrecognised, falls back to <paramref name="defaultSort"/> if supplied, else to
    /// <paramref name="defaultProperty"/>. An <c>Id</c> tiebreaker is always appended.
    /// </summary>
    /// <param name="defaultSort">
    /// The list's natural order, for defaults a single property cannot express (e.g. DisplayOrder then
    /// Name). Prefer this over <paramref name="defaultProperty"/>: it is checked by the compiler.
    /// </param>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        string? sortBy,
        bool descending,
        IReadOnlyDictionary<string, string> allowed,
        string defaultProperty = "CreatedOnUtc",
        bool defaultDescending = true,
        Func<IQueryable<T>, IOrderedQueryable<T>>? defaultSort = null)
    {
        IOrderedQueryable<T> ordered;

        if (!string.IsNullOrWhiteSpace(sortBy) && allowed != null && allowed.TryGetValue(sortBy.Trim(), out var mapped))
            ordered = query.OrderByProperty(mapped, descending);
        else if (defaultSort is not null)
            ordered = defaultSort(query);
        else
            ordered = query.OrderByProperty(defaultProperty, defaultDescending);

        return AppendTiebreaker(ordered);
    }

    /// <summary>
    /// Appends the <c>Id</c> tiebreaker, unless the entity has no Id to break ties on. Sorting by a column
    /// that is already the primary key needs no tiebreaker, but adding one is harmless and keeps this
    /// branch-free.
    /// </summary>
    private static IQueryable<T> AppendTiebreaker<T>(IOrderedQueryable<T> ordered)
        => typeof(T).GetProperty(TiebreakerProperty) is null
            ? ordered
            : ordered.ThenByProperty(TiebreakerProperty, descending: false);

    /// <summary>
    /// Builds a typed <c>OrderBy</c>/<c>OrderByDescending</c> for the given (optionally dotted) property path.
    /// The key type is the property's real type, so value-type columns sort without boxing surprises.
    /// </summary>
    public static IOrderedQueryable<T> OrderByProperty<T>(this IQueryable<T> query, string propertyPath, bool descending)
        => query.SortByProperty(
            propertyPath,
            descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy));

    /// <summary>Appends a secondary sort for the given (optionally dotted) property path.</summary>
    public static IOrderedQueryable<T> ThenByProperty<T>(this IOrderedQueryable<T> query, string propertyPath, bool descending)
        => query.SortByProperty(
            propertyPath,
            descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

    private static IOrderedQueryable<T> SortByProperty<T>(this IQueryable<T> query, string propertyPath, string method)
    {
        var param = Expression.Parameter(typeof(T), "x");
        Expression body = param;
        foreach (var member in propertyPath.Split('.'))
            body = Expression.PropertyOrField(body, member);

        var keySelector = Expression.Lambda(body, param);
        var call = Expression.Call(
            typeof(Queryable),
            method,
            new[] { typeof(T), body.Type },
            query.Expression,
            Expression.Quote(keySelector));

        return (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(call);
    }
}
