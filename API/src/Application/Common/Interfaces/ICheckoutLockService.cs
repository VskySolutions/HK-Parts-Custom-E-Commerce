namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Low-stock checkout lock (CK-20..23). Stops two buyers checking out the last few units of a product at the
/// same time: a product whose available stock is below the configured threshold is locked to the first cart
/// that reaches checkout, for a fixed time from first acquire.
/// </summary>
public interface ICheckoutLockService
{
    /// <summary>
    /// Acquires, or confirms, the lock on every low-stock product in the cart. Atomic per product. A lock the
    /// cart already holds keeps its original deadline (CK-21). Throws a 409 <c>CHECKOUT_LOCKED</c> naming the
    /// product when another cart holds a valid lock. Returns the soonest deadline the cart holds (for the
    /// storefront countdown), or null when nothing needed locking or the lock is switched off.
    /// </summary>
    Task<DateTime?> AcquireAsync(Guid cartId, IReadOnlyCollection<CheckoutLockItem> items, CancellationToken ct);

    /// <summary>Releases every lock the cart holds (order placed, or the countdown ran out). Never throws.</summary>
    Task ReleaseAsync(Guid cartId, CancellationToken ct);
}

/// <summary>A product in the cart, named for the "currently being purchased" message.</summary>
public record CheckoutLockItem(Guid ProductId, string ProductName);
