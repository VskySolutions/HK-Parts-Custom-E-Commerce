using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Domain.Enums;
using CartEntity = HKPartsEcommerce.Domain.Entities.Cart;
using ValidationException = HKPartsEcommerce.Application.Common.Exceptions.ValidationException;

namespace HKPartsEcommerce.Application.Features.Ffl;

/// <summary>The FFL panel's state for the caller's cart (FFL-21…27).</summary>
public class CheckoutFflStateDto
{
    /// <summary>FFL shipping is switched on (FFL-01).</summary>
    public bool Enabled { get; set; }

    /// <summary>The cart holds FFL-required lines, re-derived from the catalog on every read (FFL-04/05).</summary>
    public bool HasFflItems { get; set; }

    /// <summary>The cart also holds shippable non-FFL lines, which ship to the buyer (FFL-21).</summary>
    public bool HasNonFflItems { get; set; }

    public int FflLineCount { get; set; }
    public int NonFflLineCount { get; set; }

    /// <summary>The selected dealer, or null for "No dealer selected".</summary>
    public FflDealerDto? Dealer { get; set; }

    /// <summary>The dealer was pre-selected from the buyer's last FFL order (FFL-25).</summary>
    public bool Remembered { get; set; }

    /// <summary>Why a previously selected dealer was dropped (FFL-26), shown above the search.</summary>
    public string? Notice { get; set; }

    public string IntroText { get; set; } = string.Empty;
    public string TransferWarning { get; set; } = string.Empty;
    public List<int> Radii { get; set; } = new();
}

/// <summary>
/// Current (or remembered) dealer for the cart (§6.1 <c>GET /api/checkout/ffl/selection</c>). A selection that
/// is no longer eligible is cleared; a signed-in buyer with no selection gets the dealer of their last FFL
/// order, found by customer id rather than by a typed email (FFL-27), when that dealer is still eligible (FFL-26).
/// </summary>
public record GetCheckoutFflStateQuery(Guid? CartId, string? SessionId) : IRequest<CheckoutFflStateDto>;

public class GetCheckoutFflStateQueryHandler : IRequestHandler<GetCheckoutFflStateQuery, CheckoutFflStateDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;

    public GetCheckoutFflStateQueryHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _settings = settings;
        _clock = clock;
    }

    public async Task<CheckoutFflStateDto> Handle(GetCheckoutFflStateQuery request, CancellationToken ct)
    {
        var cart = await CheckoutFflSupport.LoadCartAsync(_db, _current, request.CartId, request.SessionId, ct);
        var settings = await FflSettingsStore.LoadAsync(_settings, ct);
        var state = await CheckoutFflSupport.BuildStateAsync(_db, cart, settings, ct);
        if (!state.Enabled || !state.HasFflItems)
            return state;

        var now = _clock.UtcNow;
        if (cart.FflDealerKey is not null)
        {
            var dealer = await FflDirectory.FindAsync(_db, cart.FflDealerKey, ct);
            var reason = dealer is null ? FflMessages.DealerLoadFailed : FflDirectory.IneligibleReason(dealer, now);
            if (reason is null)
            {
                state.Dealer = FflDirectory.ToDto(dealer!, now);
                return state;
            }

            cart.FflDealerKey = null;
            await _db.SaveChangesAsync(ct);
            state.Notice = reason;
        }

        if (settings.RememberLastDealer && await RememberedDealerKeyAsync(ct) is string key
            && await FflDirectory.FindAsync(_db, key, ct) is { } last
            && FflDirectory.IneligibleReason(last, now) is null)
        {
            cart.FflDealerKey = last.Key;
            await _db.SaveChangesAsync(ct);
            state.Dealer = FflDirectory.ToDto(last, now);
            state.Remembered = true;
        }

        return state;
    }

    /// <summary>FFL-25/27: the dealer of the signed-in customer's most recent FFL order.</summary>
    private async Task<string?> RememberedDealerKeyAsync(CancellationToken ct)
    {
        if (_current.UserId is not Guid userId)
            return null;
        var customerId = await _db.Customers.AsNoTracking()
            .Where(c => c.UserId == userId).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        if (customerId is null)
            return null;

        var shipping = await _db.OrderShippings.AsNoTracking()
            .Where(s => s.Order!.CustomerId == customerId && s.ShipToType == ShipToType.FflDealer
                        && (s.FflDealerKey != null || s.FflNumber != null))
            .OrderByDescending(s => s.Order!.PlacedOnUtc)
            .Select(s => new { s.FflDealerKey, s.FflNumber })
            .FirstOrDefaultAsync(ct);
        return shipping is null ? null : shipping.FflDealerKey ?? FflDirectory.KeyFromFflNumber(shipping.FflNumber);
    }
}

/// <summary>
/// Select a dealer for the cart (§6.1 <c>PUT /api/checkout/ffl/selection</c>). Only the key is accepted; the
/// dealer is read from the directory (FFL-52), and one that is unknown (404), expired or refusing transfers
/// (409) is rejected even when the key is posted directly (FFL-57/59).
/// </summary>
public record SelectCheckoutFflDealerCommand(Guid? CartId, string? SessionId, string DealerKey) : IRequest<CheckoutFflStateDto>;

public class SelectCheckoutFflDealerCommandValidator : AbstractValidator<SelectCheckoutFflDealerCommand>
{
    public SelectCheckoutFflDealerCommandValidator()
    {
        RuleFor(x => x.DealerKey).NotEmpty().WithMessage(FflMessages.DealerRequired)
            .Must(FflDirectory.IsValidKey).WithMessage("A dealer key is digits only.");
    }
}

public class SelectCheckoutFflDealerCommandHandler : IRequestHandler<SelectCheckoutFflDealerCommand, CheckoutFflStateDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;

    public SelectCheckoutFflDealerCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _settings = settings;
        _clock = clock;
    }

    public async Task<CheckoutFflStateDto> Handle(SelectCheckoutFflDealerCommand request, CancellationToken ct)
    {
        var cart = await CheckoutFflSupport.LoadCartAsync(_db, _current, request.CartId, request.SessionId, ct);
        var settings = await FflSettingsStore.LoadAsync(_settings, ct);
        if (!settings.Enabled)
            throw new ConflictException("FFL dealer shipping is not enabled.");

        var now = _clock.UtcNow;
        var dealer = await FflDirectory.FindAsync(_db, request.DealerKey, ct)
            ?? throw new NotFoundException(FflMessages.DealerLoadFailed);
        if (FflDirectory.IneligibleReason(dealer, now) is string reason)
            throw CheckoutErrorCodes.Conflict(FflMessages.DealerNotEligibleCode, reason);

        cart.FflDealerKey = dealer.Key;
        await _db.SaveChangesAsync(ct);

        var state = await CheckoutFflSupport.BuildStateAsync(_db, cart, settings, ct);
        state.Dealer = FflDirectory.ToDto(dealer, now);
        return state;
    }
}

/// <summary>Clear the cart's dealer ("No dealer selected").</summary>
public record ClearCheckoutFflDealerCommand(Guid? CartId, string? SessionId) : IRequest;

public class ClearCheckoutFflDealerCommandHandler : IRequestHandler<ClearCheckoutFflDealerCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public ClearCheckoutFflDealerCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task Handle(ClearCheckoutFflDealerCommand request, CancellationToken ct)
    {
        var cart = await CheckoutFflSupport.LoadCartAsync(_db, _current, request.CartId, request.SessionId, ct);
        if (cart.FflDealerKey is null)
            return;
        cart.FflDealerKey = null;
        await _db.SaveChangesAsync(ct);
    }
}

internal static class CheckoutFflSupport
{
    /// <summary>
    /// Resolves the checkout cart exactly as the checkout orchestrator does: by id, else the signed-in customer's
    /// cart (falling back to the session cart they built as a guest), else the guest session cart.
    /// </summary>
    public static async Task<CartEntity> LoadCartAsync(
        IApplicationDbContext db, ICurrentUserService current, Guid? cartId, string? sessionId, CancellationToken ct)
    {
        if (cartId is Guid id)
            return await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id && !c.IsCheckedOut, ct)
                   ?? throw new NotFoundException("No active cart exists for the supplied cart id.");

        var session = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim();
        if (current.UserId is Guid userId)
        {
            var customerId = await db.Customers.AsNoTracking()
                .Where(c => c.UserId == userId).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct)
                ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");
            var customerCart = await db.Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && !c.IsCheckedOut, ct);
            if (customerCart is not null)
                return customerCart;
            if (session is not null
                && await db.Carts.Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.SessionId == session && c.CustomerId == null && !c.IsCheckedOut, ct) is { } sessionCart)
                return sessionCart;
            throw new NotFoundException("No active cart exists for the current user.");
        }

        if (session is null)
            throw new ValidationException(new[] { new ValidationFailure("sessionId", "A cart session id is required for guest carts.") });
        return await db.Carts.Include(c => c.Items)
                   .FirstOrDefaultAsync(c => c.SessionId == session && c.CustomerId == null && !c.IsCheckedOut, ct)
               ?? throw new NotFoundException("No active cart exists for the supplied session id.");
    }

    /// <summary>Line classification re-derived from the catalog (FFL-03/04/05: line counts, not quantities).
    /// Online gift certificates are emailed, so they are not a shipment of their own.</summary>
    public static async Task<CheckoutFflStateDto> BuildStateAsync(
        IApplicationDbContext db, CartEntity cart, FflSettingsDto settings, CancellationToken ct)
    {
        var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.IsFflRequired, Emailed = p.ProductType == ProductType.GiftCard && p.GiftCertificateOnline })
            .ToDictionaryAsync(p => p.Id, ct);

        int ffl = 0, other = 0;
        foreach (var item in cart.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var p) || p.Emailed)
                continue;
            if (settings.Enabled && p.IsFflRequired) ffl++;
            else other++;
        }

        return new CheckoutFflStateDto
        {
            Enabled = settings.Enabled,
            HasFflItems = ffl > 0,
            HasNonFflItems = other > 0,
            FflLineCount = ffl,
            NonFflLineCount = other,
            IntroText = settings.IntroText,
            TransferWarning = settings.TransferWarning,
            Radii = settings.CheckoutRadii,
        };
    }
}
