using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>
/// What the checkout's method choice may offer before anything is priced (guest checkout U-01, U-05, §5.3
/// <c>GET /api/checkout/options</c>): whether guest checkout is open for this cart and, when not, why. The order
/// limit (A-04) and the store's own policy need a priced quote, so they arrive on the quote instead.
/// </summary>
public class CheckoutOptionsDto
{
    public bool SignedIn { get; set; }
    public bool GuestAllowed { get; set; }
    public string? GuestBlockedCode { get; set; }
    public string? GuestBlockedReason { get; set; }
}

public record GetCheckoutOptionsQuery(Guid? CartId, string? SessionId) : IRequest<CheckoutOptionsDto>;

public class GetCheckoutOptionsQueryHandler : IRequestHandler<GetCheckoutOptionsQuery, CheckoutOptionsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ISettingsService _settings;

    public GetCheckoutOptionsQueryHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings)
    {
        _db = db;
        _current = current;
        _settings = settings;
    }

    public async Task<CheckoutOptionsDto> Handle(GetCheckoutOptionsQuery request, CancellationToken ct)
    {
        if (_current.UserId is not null)
            return new CheckoutOptionsDto { SignedIn = true, GuestAllowed = true };

        string? code = null;
        var guest = await GuestCheckoutSettingsStore.LoadAsync(_settings, ct);
        if (!guest.Enabled)
            code = GuestBlockReasons.Disabled;
        else if (request.CartId is not null || !string.IsNullOrWhiteSpace(request.SessionId))
        {
            var session = request.SessionId?.Trim();
            var carts = _db.Carts.AsNoTracking().Where(c => !c.IsCheckedOut && c.CustomerId == null);
            carts = request.CartId is Guid id
                ? carts.Where(c => c.Id == id)
                : carts.Where(c => c.SessionId == session);
            var needsAccount = await carts
                .SelectMany(c => c.Items)
                .Join(_db.Products, i => i.ProductId, p => p.Id, (i, p) => p.AccountRequired)
                .AnyAsync(required => required, ct);
            if (needsAccount)
                code = GuestBlockReasons.AccountRequiredItem;
        }

        return new CheckoutOptionsDto
        {
            GuestAllowed = code is null,
            GuestBlockedCode = code,
            GuestBlockedReason = code is null ? null : GuestBlockReasons.Message(code),
        };
    }
}

/// <summary>
/// U-13: whether the email a guest typed belongs to an account, so the checkout can say "You have an account.
/// Sign in to use your saved details". Reveals only that an account exists, never anything about it.
/// </summary>
public record CheckGuestEmailQuery(string Email) : IRequest<GuestEmailCheckDto>;

/// <summary><see cref="Deliverable"/> = the domain can receive mail (U-11); false shows "check your email" before payment.</summary>
public record GuestEmailCheckDto(bool AccountExists, bool Deliverable = true);

public class CheckGuestEmailQueryHandler : IRequestHandler<CheckGuestEmailQuery, GuestEmailCheckDto>
{
    private readonly IApplicationDbContext _db;

    private readonly IEmailDomainChecker? _domains;

    public CheckGuestEmailQueryHandler(IApplicationDbContext db, IEmailDomainChecker? domains = null)
    {
        _db = db;
        _domains = domains;
    }

    public async Task<GuestEmailCheckDto> Handle(CheckGuestEmailQuery request, CancellationToken ct)
    {
        var email = Guest.NormalizeEmail(request.Email);
        if (!GuestEmail.IsValid(email))
            return new GuestEmailCheckDto(false, Deliverable: false);
        var exists = await _db.Users.AsNoTracking().AnyAsync(u => u.Email == email && u.IsActive, ct);
        var deliverable = _domains is null || await _domains.CanReceiveMailAsync(email, ct);
        return new GuestEmailCheckDto(exists, deliverable);
    }
}
