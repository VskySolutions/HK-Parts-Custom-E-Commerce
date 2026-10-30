using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Pricing;

/// <summary>
/// Applies the configured discount rules to a cart (REQ-PRP-001). Loads every active, in-window
/// discount whose minimum-order threshold the subtotal meets, computes each rule's reduction against
/// the base its scope targets, then either stacks all non-exclusive reductions or — when any
/// exclusive discount applies — replaces them with the single most-favorable exclusive one
/// (AC-PRP-001.4). The result is an itemized breakdown whose total equals the sum of the amounts.
/// </summary>
public class DiscountService : IDiscountService
{
    private readonly IApplicationDbContext _db;

    public DiscountService(IApplicationDbContext db) => _db = db;

    public async Task<DiscountEvaluationResult> EvaluateAsync(
        IReadOnlyList<DiscountCartLine> lines, decimal subtotal,
        IReadOnlyCollection<Guid>? unlockedDiscountIds = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Candidate rules: active, currently in-window, and satisfied by the subtotal's minimum threshold.
        var discounts = await _db.Discounts
            .AsNoTracking()
            .Where(d => d.IsActive)
            .Where(d => d.StartDateUtc == null || d.StartDateUtc <= now)
            .Where(d => d.EndDateUtc == null || d.EndDateUtc >= now)
            .Where(d => d.MinimumOrderValue == null || subtotal >= d.MinimumOrderValue)
            .ToListAsync(ct);

        // Turn each rule into a concrete reduction; drop rules that reduce nothing (no matching lines, etc.).
        var candidates = new List<(Discount Discount, AppliedDiscount Applied)>();
        foreach (var d in discounts)
        {
            // Coupon-gated rules (REQ-PRP-002) apply only when a valid coupon bound to them is present;
            // otherwise they never contribute, so entering the code is what actually unlocks the price.
            if (d.RequiresCoupon && (unlockedDiscountIds is null || !unlockedDiscountIds.Contains(d.Id)))
                continue;

            var amount = ComputeReduction(d, lines, subtotal);
            if (amount <= 0m)
                continue;
            candidates.Add((d, new AppliedDiscount(d.Id, d.Name, d.Scope, amount)));
        }

        // AC-PRP-001.4: an applicable exclusive discount overrides all others — keep only the best one.
        var exclusive = candidates.Where(c => c.Discount.IsExclusive).ToList();
        if (exclusive.Count > 0)
        {
            var best = exclusive
                .OrderByDescending(c => c.Applied.Amount)
                .ThenBy(c => c.Applied.DiscountId)
                .First();
            var (bestLineDiscounts, bestTotal) = AllocateToLines(new[] { best }, lines);
            return new DiscountEvaluationResult(new List<AppliedDiscount> { best.Applied }, bestTotal, bestLineDiscounts);
        }

        // Otherwise every non-exclusive discount stacks.
        var applied = candidates.Select(c => c.Applied).ToList();
        var (lineDiscounts, total) = AllocateToLines(candidates, lines);
        return new DiscountEvaluationResult(applied, total, lineDiscounts);
    }

    /// <summary>
    /// Distributes each applied discount across the lines its scope targets (whole cart → every line;
    /// product/category → only matching lines), proportional to each line's value, and sums the shares per
    /// line. A line's cumulative discount is capped at the line's own value so a stacked set of rules can
    /// never drive its taxable base below zero. Returns the per-line amounts (index-aligned to
    /// <paramref name="lines"/>) and their total.
    /// </summary>
    private static (IReadOnlyList<decimal> LineDiscounts, decimal Total) AllocateToLines(
        IReadOnlyList<(Discount Discount, AppliedDiscount Applied)> finalDiscounts,
        IReadOnlyList<DiscountCartLine> lines)
    {
        var lineDiscounts = new decimal[lines.Count];

        foreach (var (discount, applied) in finalDiscounts)
            AllocateAcross(applied.Amount, TargetLineIndices(discount, lines), lines, lineDiscounts);

        decimal total = 0m;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lineDiscounts[i] > lines[i].LineTotal) lineDiscounts[i] = lines[i].LineTotal;
            if (lineDiscounts[i] < 0m) lineDiscounts[i] = 0m;
            total += lineDiscounts[i];
        }

        return (lineDiscounts, total);
    }

    /// <summary>The indices of the lines a discount's scope targets (only lines with a positive value).</summary>
    private static List<int> TargetLineIndices(Discount d, IReadOnlyList<DiscountCartLine> lines)
    {
        var indices = new List<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].LineTotal <= 0m)
                continue;

            var matches = d.Scope switch
            {
                DiscountScope.CartTotal or DiscountScope.OrderSubtotal => true,
                DiscountScope.Product => d.ProductId.HasValue && lines[i].ProductId == d.ProductId.Value,
                DiscountScope.Category => d.CategoryId.HasValue && lines[i].CategoryIds.Contains(d.CategoryId.Value),
                _ => false,
            };
            if (matches)
                indices.Add(i);
        }
        return indices;
    }

    /// <summary>
    /// Spreads <paramref name="amount"/> across <paramref name="targetIndices"/> proportional to each line's
    /// value, rounding each share to cents and giving the last target the remainder so the parts sum exactly
    /// to <paramref name="amount"/>. Accumulates into <paramref name="lineDiscounts"/>.
    /// </summary>
    private static void AllocateAcross(
        decimal amount, IReadOnlyList<int> targetIndices, IReadOnlyList<DiscountCartLine> lines, decimal[] lineDiscounts)
    {
        if (amount <= 0m || targetIndices.Count == 0)
            return;

        var baseSum = targetIndices.Sum(i => lines[i].LineTotal);
        if (baseSum <= 0m)
            return;

        decimal allocated = 0m;
        for (var k = 0; k < targetIndices.Count; k++)
        {
            var i = targetIndices[k];
            var share = k == targetIndices.Count - 1
                ? amount - allocated // last line takes the remainder → no rounding drift
                : decimal.Round(amount * (lines[i].LineTotal / baseSum), 2, MidpointRounding.AwayFromZero);
            allocated += share;
            lineDiscounts[i] += share;
        }
    }

    /// <summary>
    /// The reduction a single rule contributes: a percentage of, or a fixed amount capped at, the base
    /// its scope targets (whole subtotal for cart/order rules, matching product lines for product rules,
    /// matching-category lines for category rules). Never reduces the targeted base below zero.
    /// </summary>
    private static decimal ComputeReduction(Discount d, IReadOnlyList<DiscountCartLine> lines, decimal subtotal)
    {
        var @base = d.Scope switch
        {
            DiscountScope.CartTotal or DiscountScope.OrderSubtotal => subtotal,
            DiscountScope.Product => lines
                .Where(l => d.ProductId.HasValue && l.ProductId == d.ProductId.Value)
                .Sum(l => l.LineTotal),
            DiscountScope.Category => lines
                .Where(l => d.CategoryId.HasValue && l.CategoryIds.Contains(d.CategoryId.Value))
                .Sum(l => l.LineTotal),
            _ => 0m,
        };

        if (@base <= 0m)
            return 0m;

        var reduction = d.Type switch
        {
            DiscountType.Percentage => @base * (d.Value / 100m),
            DiscountType.FixedAmount => d.Value,
            _ => 0m,
        };

        if (reduction < 0m)
            reduction = 0m;
        if (reduction > @base)
            reduction = @base;

        return decimal.Round(reduction, 2, MidpointRounding.AwayFromZero);
    }
}
