using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.PriceRules;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Pricing;

/// <summary>
/// Rebuilds <c>PriceRuleProductPrices</c> with set-based SQL (one DELETE + one INSERT…SELECT per rule, no
/// row-by-row work), inside one transaction per rule. <c>sp_getapplock</c> on <c>PriceRule:{id}</c> makes the
/// rebuild single-flight per rule across every app instance (the "distributed lock").
/// <para>
/// Target set: products directly assigned to a selected category, listed in the rule's store (a product with
/// no store rows is listed everywhere), excluding gift cards — plus every variant of those products. Each
/// target × selected group gets one row: the group base price (fixed group price, group percentage, or list
/// price) and the rule price computed from it — the same arithmetic as <see cref="PriceRuleMath"/>.
/// </para>
/// </summary>
public class PriceRuleEngine : IPriceRuleEngine
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(3);

    // Shared by rebuild and preview. Parameters: @categoryIds / @groupIds (JSON arrays of guids), @storeId,
    // @discountType, @amount, plus the enum constants. Ends with the "Priced" CTE.
    private static readonly string PricedCte = $"""
        WITH Cats AS (
            SELECT DISTINCT TRY_CAST([value] AS uniqueidentifier) AS CategoryId FROM OPENJSON(@categoryIds)
        ),
        Grps AS (
            SELECT DISTINCT TRY_CAST([value] AS uniqueidentifier) AS CustomerGroupId FROM OPENJSON(@groupIds)
        ),
        Targets AS (
            SELECT DISTINCT p.[Id] AS ProductId
            FROM [Products] p
            JOIN [ProductCategories] pc ON pc.[ProductId] = p.[Id]
            JOIN Cats c ON c.CategoryId = pc.[CategoryId]
            WHERE p.[Deleted] = 0
              AND p.[ProductType] <> {(int)ProductType.GiftCard}
              AND (@storeId IS NULL
                   OR NOT EXISTS (SELECT 1 FROM [ProductStoreMappings] sm WHERE sm.[ProductId] = p.[Id])
                   OR EXISTS (SELECT 1 FROM [ProductStoreMappings] sm WHERE sm.[ProductId] = p.[Id] AND sm.[StoreId] = @storeId))
        ),
        Items AS (
            SELECT p.[Id] AS ProductId, CAST(NULL AS uniqueidentifier) AS ProductVariantId, p.[Price] AS ListPrice
            FROM Targets t JOIN [Products] p ON p.[Id] = t.ProductId
            WHERE p.[Price] IS NOT NULL
            UNION ALL
            SELECT p.[Id], v.[Id], COALESCE(v.[Price], p.[Price])
            FROM Targets t
            JOIN [Products] p ON p.[Id] = t.ProductId
            JOIN [ProductVariants] v ON v.[ProductId] = p.[Id]
            WHERE v.[Deleted] = 0 AND COALESCE(v.[Price], p.[Price]) IS NOT NULL
        ),
        Based AS (
            SELECT i.ProductId, i.ProductVariantId, g.CustomerGroupId,
                CAST(CASE
                    WHEN cg.[PricingRuleType] = {(int)CustomerGroupPricingRuleType.FixedGroupPrice} THEN COALESCE(
                        (SELECT MIN(x.[Price]) FROM [CustomerGroupPrices] x
                          WHERE x.[CustomerGroupId] = g.CustomerGroupId AND x.[ProductId] = i.ProductId
                            AND i.ProductVariantId IS NOT NULL AND x.[ProductVariantId] = i.ProductVariantId),
                        (SELECT MIN(x.[Price]) FROM [CustomerGroupPrices] x
                          WHERE x.[CustomerGroupId] = g.CustomerGroupId AND x.[ProductId] = i.ProductId
                            AND x.[ProductVariantId] IS NULL),
                        i.ListPrice)
                    WHEN cg.[PricingRuleType] = {(int)CustomerGroupPricingRuleType.PercentageDiscount} AND cg.[DiscountPercent] > 0 THEN
                        ROUND(i.ListPrice * (1 - (CASE WHEN cg.[DiscountPercent] > 100 THEN 100 ELSE cg.[DiscountPercent] END) / 100.0), 2)
                    ELSE i.ListPrice
                END AS decimal(18, 4)) AS BasePrice
            FROM Items i
            CROSS JOIN Grps g
            LEFT JOIN [CustomerGroups] cg
                   ON cg.[Id] = g.CustomerGroupId AND cg.[Deleted] = 0 AND cg.[IsActive] = 1
        ),
        Priced AS (
            SELECT b.ProductId, b.ProductVariantId, b.CustomerGroupId, b.BasePrice,
                CAST(CASE
                    WHEN @discountType = {(int)PriceRuleDiscountType.Percent}
                        THEN ROUND(b.BasePrice - b.BasePrice * @amount / 100.0, 2)
                    WHEN b.BasePrice - @amount < 0 THEN 0
                    ELSE ROUND(b.BasePrice - @amount, 2)
                END AS decimal(18, 4)) AS RulePrice
            FROM Based b
        )
        """;

    private static readonly string RebuildSql = $"""
        SET NOCOUNT ON;
        DECLARE @lock int;
        EXEC @lock = sp_getapplock @Resource = @lockName, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 120000;
        IF @lock < 0 THROW 50001, 'Could not acquire the price-rule rebuild lock.', 1;

        DELETE FROM [PriceRuleProductPrices] WHERE [PriceRuleId] = @ruleId;

        IF @write = 1
        BEGIN
            ;{PricedCte}
            INSERT INTO [PriceRuleProductPrices]
                ([PriceRuleId], [StoreId], [ProductId], [ProductVariantId], [CustomerGroupId],
                 [BasePrice], [RulePrice], [StartDate], [EndDate], [Priority], [StopFurtherRules])
            SELECT @ruleId, @storeId, ProductId, ProductVariantId, CustomerGroupId,
                   BasePrice, RulePrice, @startDate, @endDate, @priority, @stop
            FROM Priced;
        END

        UPDATE [PriceRules] SET [LastAppliedAtUtc] = @now WHERE [Id] = @ruleId;
        """;

    private static readonly string PreviewSql = $"""
        SET NOCOUNT ON;
        {PricedCte}
        SELECT * INTO #priced FROM Priced;

        SELECT COUNT(DISTINCT ProductId) AS ProductCount, COUNT(*) AS PriceRowCount FROM #priced;

        SELECT TOP (50) x.ProductId, x.ProductVariantId, COALESCE(v.[Sku], p.[Sku]) AS Sku, p.[Name],
               x.CustomerGroupId, cg.[Name] AS GroupName, x.BasePrice, x.RulePrice
        FROM #priced x
        JOIN [Products] p ON p.[Id] = x.ProductId
        LEFT JOIN [ProductVariants] v ON v.[Id] = x.ProductVariantId
        LEFT JOIN [CustomerGroups] cg ON cg.[Id] = x.CustomerGroupId
        ORDER BY p.[Name], v.[DisplayOrder], cg.[Name];

        DROP TABLE #priced;
        """;

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<PriceRuleEngine> _logger;

    public PriceRuleEngine(IApplicationDbContext db, IDateTimeProvider clock, ILogger<PriceRuleEngine> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task RebuildAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await _db.PriceRules.AsNoTracking()
            .Where(r => r.Id == ruleId)
            .Select(r => new
            {
                r.Id, r.StoreId, r.IsActive, r.Priority, r.StopFurtherRules, r.StartDate, r.EndDate,
                r.DiscountType, r.DiscountAmount,
                Groups = r.CustomerGroups.Select(g => g.CustomerGroupId).ToList(),
                Categories = r.Categories.Select(c => c.CategoryId).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (rule is null)
            return; // deleted since it was queued — its rows went with it (cascade)

        var now = _clock.UtcNow;
        var clock = await PriceRuleClock.LoadAsync(_db, cancellationToken);
        var today = clock.TodayFor(rule.StoreId, now);

        // Inactive or already expired: just clear. A future start date is fine — the resolver applies dates.
        var write = rule.IsActive && (rule.EndDate is null || rule.EndDate.Value >= today)
                    && rule.Groups.Count > 0 && rule.Categories.Count > 0;

        var parameters = new object[]
        {
            new SqlParameter("@lockName", SqlDbType.NVarChar, 255) { Value = $"PriceRule:{rule.Id}" },
            new SqlParameter("@ruleId", rule.Id),
            new SqlParameter("@write", write),
            new SqlParameter("@storeId", SqlDbType.UniqueIdentifier) { Value = (object?)rule.StoreId ?? DBNull.Value },
            new SqlParameter("@categoryIds", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(rule.Categories) },
            new SqlParameter("@groupIds", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(rule.Groups) },
            new SqlParameter("@discountType", (int)rule.DiscountType),
            new SqlParameter("@amount", SqlDbType.Decimal) { Precision = 18, Scale = 4, Value = rule.DiscountAmount },
            new SqlParameter("@startDate", SqlDbType.Date) { Value = (object?)rule.StartDate?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value },
            new SqlParameter("@endDate", SqlDbType.Date) { Value = (object?)rule.EndDate?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value },
            new SqlParameter("@priority", rule.Priority),
            new SqlParameter("@stop", rule.StopFurtherRules),
            new SqlParameter("@now", SqlDbType.DateTime2) { Value = now },
        };

        var started = DateTime.UtcNow;
        var previousTimeout = _db.Database.GetCommandTimeout();
        _db.Database.SetCommandTimeout(CommandTimeout);
        try
        {
            // Join an ambient transaction (e.g. a caller's unit of work, or a test) or own one.
            if (_db.Database.CurrentTransaction is not null)
            {
                await _db.Database.ExecuteSqlRawAsync(RebuildSql, parameters, cancellationToken);
            }
            else
            {
                await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                await _db.Database.ExecuteSqlRawAsync(RebuildSql, parameters, cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            _db.Database.SetCommandTimeout(previousTimeout);
        }

        _logger.LogInformation("Rebuilt price rule {RuleId} (write={Write}) in {Elapsed} ms.",
            rule.Id, write, (int)(DateTime.UtcNow - started).TotalMilliseconds);
    }

    public async Task<int> RebuildAllAsync(Guid? storeId = null, CancellationToken cancellationToken = default)
    {
        var ids = await _db.PriceRules.AsNoTracking()
            .Where(r => storeId == null || r.StoreId == storeId)
            .OrderBy(r => r.Priority).ThenBy(r => r.Id)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
            await RebuildAsync(id, cancellationToken);
        return ids.Count;
    }

    public async Task<int> RebuildForProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return 0;

        var ids = productIds.Distinct().ToList();

        // Rules that target one of the products' categories now (new/changed rows) or already price one of
        // them (rows that must go: category unassigned, product deleted, now a gift card…). IgnoreQueryFilters
        // so a just-soft-deleted product's category links still count.
        var categoryIds = _db.ProductCategories.IgnoreQueryFilters()
            .Where(pc => ids.Contains(pc.ProductId))
            .Select(pc => pc.CategoryId);

        var ruleIds = await _db.PriceRuleCategories.AsNoTracking()
            .Where(rc => categoryIds.Contains(rc.CategoryId))
            .Select(rc => rc.PriceRuleId)
            .Union(_db.PriceRuleProductPrices.AsNoTracking()
                .Where(x => ids.Contains(x.ProductId))
                .Select(x => x.PriceRuleId))
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var ruleId in ruleIds)
            await RebuildAsync(ruleId, cancellationToken);
        return ruleIds.Count;
    }

    public async Task<PriceRulePreviewResult> PreviewAsync(PriceRuleDraft draft, CancellationToken cancellationToken = default)
    {
        var result = new PriceRulePreviewResult();
        if (draft.CategoryIds.Count == 0 || draft.CustomerGroupIds.Count == 0)
            return result;

        var connection = _db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = PreviewSql;
            command.CommandTimeout = (int)CommandTimeout.TotalSeconds;
            command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
            command.Parameters.Add(new SqlParameter("@storeId", SqlDbType.UniqueIdentifier) { Value = (object?)draft.StoreId ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@categoryIds", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(draft.CategoryIds) });
            command.Parameters.Add(new SqlParameter("@groupIds", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(draft.CustomerGroupIds) });
            command.Parameters.Add(new SqlParameter("@discountType", (int)draft.DiscountType));
            command.Parameters.Add(new SqlParameter("@amount", SqlDbType.Decimal) { Precision = 18, Scale = 4, Value = draft.DiscountAmount });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                result.ProductCount = reader.GetInt32(0);
                result.PriceRowCount = reader.GetInt32(1);
            }

            await reader.NextResultAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Sample.Add(new PriceRulePreviewRow
                {
                    ProductId = reader.GetGuid(0),
                    ProductVariantId = reader.IsDBNull(1) ? null : reader.GetGuid(1),
                    Sku = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Name = reader.GetString(3),
                    CustomerGroupId = reader.GetGuid(4),
                    GroupName = reader.IsDBNull(5) ? PriceRuleStates.RetailGroupName : reader.GetString(5),
                    BasePrice = reader.GetDecimal(6),
                    RulePrice = reader.GetDecimal(7),
                });
            }
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }

        return result;
    }
}
