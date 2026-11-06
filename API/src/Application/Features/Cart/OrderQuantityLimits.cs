using FluentValidation.Results;
using HKPartsEcommerce.Domain.Entities;
using ValidationException = HKPartsEcommerce.Application.Common.Exceptions.ValidationException;

namespace HKPartsEcommerce.Application.Features.Cart;

/// <summary>
/// The product's "Min qty allowed" / "Max qty allowed" (<see cref="Product.MinOrderQuantity"/> /
/// <see cref="Product.MaxOrderQuantity"/>). The limit applies to the product's TOTAL quantity in the cart —
/// summed across all of its lines (variants and differently-engraved lines alike) — so splitting the same
/// product over several lines cannot get around the maximum. A null or non-positive limit means "no limit".
/// </summary>
public static class OrderQuantityLimits
{
    /// <summary>The buyer-facing message when <paramref name="totalQuantity"/> breaks a limit; otherwise null.</summary>
    public static string? Violation(Product product, int totalQuantity)
    {
        if (product.MinOrderQuantity is int min && min > 1 && totalQuantity < min)
            return $"The minimum order quantity for '{product.Name}' is {min}.";

        if (product.MaxOrderQuantity is int max && max > 0 && totalQuantity > max)
            return $"The maximum order quantity for '{product.Name}' is {max}.";

        return null;
    }

    /// <summary>Throws a 400 (field <c>quantity</c>) when <paramref name="totalQuantity"/> breaks a limit.</summary>
    public static void Ensure(Product product, int totalQuantity)
    {
        var message = Violation(product, totalQuantity);
        if (message is not null)
            throw new ValidationException(new[] { new ValidationFailure("quantity", message) });
    }
}
