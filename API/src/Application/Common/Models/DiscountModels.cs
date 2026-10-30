using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>
/// A single discount rule that reduced the cart, together with the reduction it contributed
/// (REQ-PRP-001). Part of the itemized breakdown returned by discount evaluation.
/// </summary>
public record AppliedDiscount(Guid DiscountId, string Name, DiscountScope Scope, decimal Amount);

/// <summary>
/// The itemized outcome of evaluating all applicable discounts against a cart: the discounts that
/// applied, the aggregate reduction (<see cref="TotalDiscount"/> equals the sum of the amounts), and the
/// per-line allocation of that reduction. <see cref="LineDiscounts"/> is index-aligned to the input lines
/// (each is that line's share of every applied discount, scope-aware and proportional), and its sum equals
/// <see cref="TotalDiscount"/> — the checkout uses it to tax each line on its discounted value.
/// </summary>
public record DiscountEvaluationResult(
    List<AppliedDiscount> Applied,
    decimal TotalDiscount,
    IReadOnlyList<decimal> LineDiscounts);

/// <summary>
/// One cart line projected for discount evaluation: the product, the categories it belongs to
/// (for category-scoped rules), the line's monetary total and its quantity.
/// </summary>
public record DiscountCartLine(Guid ProductId, IReadOnlyList<Guid> CategoryIds, decimal LineTotal, int Quantity);
