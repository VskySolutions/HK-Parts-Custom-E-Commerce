using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Ebay;

public sealed record EbayOrderImportResult(bool Skipped, int Fetched, int Imported, int AlreadyImported, int NotPaid, int Failed, IReadOnlyList<string> Messages);

/// <summary>
/// Imports paid eBay orders (Fulfillment getOrders, every page) as store orders. Each order is written in one
/// transaction — order, lines, status history, payment, stock decrements and listing sold counts — and the
/// filtered unique index on <c>Orders.EbayOrderId</c> backs the existence check, so re-runs never duplicate.
/// </summary>
public sealed class EbayOrderImportService
{
    private const int PageSize = 200;
    private static readonly TimeSpan Overlap = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FirstRunLookback = TimeSpan.FromDays(30);

    private readonly IApplicationDbContext _db;
    private readonly IEbayClient _ebay;
    private readonly IInventoryService _inventory;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<EbayOrderImportService> _logger;

    public EbayOrderImportService(
        IApplicationDbContext db, IEbayClient ebay, IInventoryService inventory, IDateTimeProvider clock,
        ILogger<EbayOrderImportService> logger)
    {
        _db = db;
        _ebay = ebay;
        _inventory = inventory;
        _clock = clock;
        _logger = logger;
    }

    /// <param name="force">Run for a Sandbox account too (Production only otherwise).</param>
    public async Task<EbayOrderImportResult> ImportAsync(Guid storeId, bool force = false, CancellationToken ct = default)
    {
        var account = await _db.EbayAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.StoreId == storeId, ct);
        if (account is null || !account.IsEnabled || (account.Environment == EbayEnvironment.Sandbox && !force))
            return new EbayOrderImportResult(true, 0, 0, 0, 0, 0, Array.Empty<string>());

        var runStart = _clock.UtcNow;
        var since = (account.OrdersLastSyncedAtUtc ?? runStart - FirstRunLookback) - Overlap;

        var orders = new List<EbayOrder>();
        // Advance by what actually came back (eBay may cap a page below the requested limit).
        for (var offset = 0; ; )
        {
            var page = await _ebay.GetOrdersAsync(storeId, since, offset, PageSize, ct);
            orders.AddRange(page.Orders);
            offset += page.Orders.Count;
            if (page.Orders.Count == 0 || offset >= page.Total)
                break;
        }

        int imported = 0, existing = 0, notPaid = 0, failed = 0;
        var messages = new List<string>();
        foreach (var order in orders)
        {
            if (!string.Equals(order.OrderPaymentStatus, "PAID", StringComparison.OrdinalIgnoreCase))
            {
                notPaid++;
                continue;
            }

            if (await _db.Orders.IgnoreQueryFilters().AnyAsync(o => o.EbayOrderId == order.OrderId, ct))
            {
                existing++;
                continue;
            }

            try
            {
                if (await ImportOrderAsync(storeId, order, messages, ct))
                    imported++;
                else
                    failed++;
            }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
            {
                failed++;
                messages.Add($"eBay order {order.OrderId}: {ex.Message}");
                _logger.LogError(ex, "Importing eBay order {EbayOrderId} failed.", order.OrderId);
                ClearTracker();
            }
        }

        // Only a completed run advances the window (a throw above skips this).
        await _db.EbayAccounts.Where(a => a.StoreId == storeId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.OrdersLastSyncedAtUtc, runStart), ct);

        _logger.LogInformation(
            "eBay order import for store {StoreId}: fetched {Fetched}, imported {Imported}, existing {Existing}, unpaid {Unpaid}, failed {Failed}.",
            storeId, orders.Count, imported, existing, notPaid, failed);
        return new EbayOrderImportResult(false, orders.Count, imported, existing, notPaid, failed, messages);
    }

    private async Task<bool> ImportOrderAsync(Guid storeId, EbayOrder ebayOrder, List<string> messages, CancellationToken ct)
    {
        // Match every line to a listing of this store by eBay listing id, else by SKU (ended listings count:
        // an order can arrive after the listing ended).
        var legacyIds = ebayOrder.LineItems.Select(l => l.LegacyItemId).Where(x => x != null).Select(x => x!).ToList();
        var skus = ebayOrder.LineItems.Select(l => l.Sku).Where(x => x != null).Select(x => x!).ToList();
        var listings = await _db.EbayListings
            .Include(l => l.Product)
            .Where(l => l.StoreId == storeId
                        && ((l.EbayListingIdExternal != null && legacyIds.Contains(l.EbayListingIdExternal)) || skus.Contains(l.Sku)))
            .ToListAsync(ct);

        var matched = new List<(EbayOrderLineItem Line, EbayListing Listing)>();
        foreach (var line in ebayOrder.LineItems)
        {
            var listing = listings.FirstOrDefault(l => line.LegacyItemId != null && l.EbayListingIdExternal == line.LegacyItemId)
                          ?? listings
                              .Where(l => line.Sku != null && string.Equals(l.Sku, line.Sku, StringComparison.OrdinalIgnoreCase))
                              .OrderBy(l => l.Status == EbayListingStatus.Active ? 0 : 1)
                              .FirstOrDefault();
            if (listing?.Product is null)
            {
                var note = $"eBay order {ebayOrder.OrderId}: line {line.LineItemId} ('{line.Title}', SKU {line.Sku ?? "-"}) matches no listing — skipped.";
                messages.Add(note);
                _logger.LogWarning("{Message}", note);
                continue;
            }

            matched.Add((line, listing));
        }

        if (matched.Count == 0)
        {
            messages.Add($"eBay order {ebayOrder.OrderId}: no line item matched a listing — order not imported.");
            return false;
        }

        var email = ebayOrder.ShipToEmail ?? ebayOrder.BuyerEmail;
        Guid? customerId = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.Trim().ToLower();
            customerId = await _db.Customers.AsNoTracking()
                .Where(c => _db.Users.Any(u => u.Id == c.UserId && u.Email.ToLower() == normalized))
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);
        }

        var (first, last) = SplitName(ebayOrder.ShipTo?.Name ?? ebayOrder.BuyerUsername);
        var now = _clock.UtcNow;
        var order = new Order
        {
            OrderNumber = $"EB-{ebayOrder.OrderId}"[..Math.Min(40, ebayOrder.OrderId.Length + 3)],
            Status = OrderStatus.Processing,
            CustomerId = customerId,
            ShippingAddress = new Address
            {
                FirstName = first,
                LastName = last,
                Email = email,
                PhoneNumber = ebayOrder.ShipTo?.Phone,
                AddressLine1 = ebayOrder.ShipTo?.AddressLine1 ?? string.Empty,
                AddressLine2 = ebayOrder.ShipTo?.AddressLine2,
                City = ebayOrder.ShipTo?.City ?? string.Empty,
                StateProvince = ebayOrder.ShipTo?.StateOrProvince,
                PostalCode = ebayOrder.ShipTo?.PostalCode ?? string.Empty,
                CountryCode = ebayOrder.ShipTo?.CountryCode ?? string.Empty,
            },
            AssignedStoreId = storeId,
            PlacedOnUtc = ebayOrder.CreationDateUtc,
            RoutedOnUtc = now,
            CurrencyCode = ebayOrder.CurrencyCode,
            Subtotal = ebayOrder.Subtotal,
            ShippingTotal = ebayOrder.DeliveryCost,
            TaxTotal = ebayOrder.Tax,
            TotalAmount = ebayOrder.Total,
            ShippingMethodName = ebayOrder.ShippingServiceCode,
            PaymentStatus = PaymentStatus.Captured,
            EbayOrderId = ebayOrder.OrderId,
            EbayBuyerMessage = string.IsNullOrWhiteSpace(ebayOrder.BuyerMessage) ? null : ebayOrder.BuyerMessage,
        };

        foreach (var (line, listing) in matched)
        {
            order.Lines.Add(new OrderLineItem
            {
                ProductId = listing.ProductId,
                ProductName = listing.Product!.Name,
                Sku = listing.Sku,
                Quantity = line.Quantity,
                // eBay's sold price, not the store price.
                UnitPrice = line.UnitPrice,
                OriginalUnitPrice = line.UnitPrice,
                LineTotal = line.LineItemCost,
                EbayLineItemId = line.LineItemId,
            });
        }

        order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = OrderStatus.Processing,
            ToStatus = OrderStatus.Processing,
            ChangedOnUtc = now,
            Note = $"Imported from eBay order {ebayOrder.OrderId}.",
        });

        order.Payments.Add(new PaymentRecord
        {
            Method = PaymentMethodType.Ebay,
            GatewayName = $"eBay - {ebayOrder.PaymentMethod ?? "Managed Payments"}",
            Amount = ebayOrder.Total,
            CurrencyCode = ebayOrder.CurrencyCode,
            Status = PaymentStatus.Captured,
            TransactionId = ebayOrder.OrderId,
            GatewayReference = ebayOrder.OrderId,
            AuthorizedOnUtc = now,
            CapturedOnUtc = now,
        });

        var ownsTransaction = _db.Database.CurrentTransaction is null;
        var tx = ownsTransaction ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            _db.Orders.Add(order);
            await _db.SaveChangesAsync(ct);

            foreach (var (line, listing) in matched)
            {
                // The sale already happened on eBay: when stock is short, record it anyway (may go negative).
                if (!await _inventory.DecrementStockAsync(listing.ProductId, null, storeId, line.Quantity, ct))
                {
                    await _inventory.AdjustStockAsync(listing.ProductId, null, storeId, -line.Quantity, ct);
                    messages.Add($"eBay order {ebayOrder.OrderId}: stock for {listing.Sku} was short; decremented anyway.");
                }

                listing.QuantitySold += line.Quantity;
            }

            await _db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    /// <summary>First name = up to the first space; last name = everything after it.</summary>
    public static (string? First, string? Last) SplitName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return (null, null);
        var trimmed = fullName.Trim();
        var space = trimmed.IndexOf(' ');
        return space < 0 ? (trimmed, null) : (trimmed[..space], trimmed[(space + 1)..].Trim());
    }

    private void ClearTracker()
    {
        if (_db is DbContext context)
            context.ChangeTracker.Clear();
    }
}
