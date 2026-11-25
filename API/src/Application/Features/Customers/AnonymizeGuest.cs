using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Guest checkout N-05: a guest's deletion request. Name, email, phone and street address are anonymised on their
/// non-firearm guest orders (city / state / ZIP / country stay for tax and sales reports, as the customer GDPR
/// delete does). Firearm orders are kept untouched, as the law requires. When nothing firearm-related is left, the
/// guest row itself is anonymised too; otherwise it keeps the email so those records stay traceable. Also unsubscribes
/// the email and clears it from open carts. Orders already moved to an account are left to the account's own delete.
/// </summary>
public record AnonymizeGuestCommand(Guid GuestId) : IRequest<AnonymizeGuestResult>;

/// <param name="AnonymizedOrders">Orders whose personal data was removed.</param>
/// <param name="KeptFirearmOrders">Firearm orders kept as they are (legal record keeping).</param>
public record AnonymizeGuestResult(int AnonymizedOrders, int KeptFirearmOrders);

public class AnonymizeGuestCommandHandler : IRequestHandler<AnonymizeGuestCommand, AnonymizeGuestResult>
{
    public const string AnonymisedFirstName = "Deleted";
    public const string AnonymisedLastName = "Customer";
    public const string RedactedLine = "Redacted";

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService? _current;

    public AnonymizeGuestCommandHandler(IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService? current = null)
    {
        _db = db;
        _clock = clock;
        _current = current;
    }

    public static string AnonymisedEmail(Guid guestId) => $"deleted-{guestId:N}@anonymized.invalid";

    public async Task<AnonymizeGuestResult> Handle(AnonymizeGuestCommand request, CancellationToken ct)
    {
        var guest = await _db.Guests.FirstOrDefaultAsync(g => g.Id == request.GuestId, ct)
                    ?? throw new NotFoundException(nameof(Guest), request.GuestId);
        var originalEmail = guest.EmailNormalized;
        var replacement = AnonymisedEmail(guest.Id);
        var now = _clock.UtcNow;

        var orders = await _db.Orders
            .Include(o => o.ShippingAddress)
            .Include(o => o.BillingAddress)
            .Where(o => o.GuestId == guest.Id && o.CustomerId == null)
            .ToListAsync(ct);

        var anonymised = 0;
        var keptFirearm = 0;
        foreach (var order in orders)
        {
            if (order.HasFflItems)
            {
                keptFirearm++;
                continue;
            }
            Redact(order.ShippingAddress, replacement);
            Redact(order.BillingAddress, replacement);
            order.CancelRequestReason = order.CancelRequestReason is null ? null : "Redacted";
            order.StatusHistory.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = order.Status,
                ToStatus = order.Status,
                ChangedById = _current?.UserId,
                ChangedOnUtc = now,
                Note = "Guest personal data anonymised on request.",
            });
            anonymised++;
        }

        // N-06: no more marketing to this address; open carts forget it.
        var subscriptions = await _db.CMSNewsletterSubscriptions.Where(s => s.Email == originalEmail).ToListAsync(ct);
        foreach (var s in subscriptions)
        {
            s.Status = NewsletterSubscriptionStatus.Unsubscribed;
            s.UnsubscribedOnUtc = now;
            if (keptFirearm == 0)
                s.Email = replacement;
        }
        var carts = await _db.Carts.Where(c => c.ContactEmail == originalEmail).ToListAsync(ct);
        foreach (var cart in carts)
        {
            cart.ContactEmail = null;
            cart.MarketingOptIn = false;
        }

        guest.MarketingOptIn = false;
        guest.OptInOnUtc = null;
        guest.AnonymizedOnUtc = now;
        if (keptFirearm == 0)
        {
            guest.EmailNormalized = replacement;
            guest.FirstName = AnonymisedFirstName;
            guest.LastName = AnonymisedLastName;
            guest.PhoneNumber = null;
        }

        await _db.SaveChangesAsync(ct);
        return new AnonymizeGuestResult(anonymised, keptFirearm);
    }

    private static void Redact(Address? a, string email)
    {
        if (a is null)
            return;
        a.FirstName = AnonymisedFirstName;
        a.LastName = AnonymisedLastName;
        a.Company = null;
        a.AddressLine1 = RedactedLine;
        a.AddressLine2 = null;
        a.Landmark = null;
        a.PhoneNumber = null;
        a.Email = string.IsNullOrWhiteSpace(a.Email) ? a.Email : email;
        a.Latitude = null;
        a.Longitude = null;
    }
}
