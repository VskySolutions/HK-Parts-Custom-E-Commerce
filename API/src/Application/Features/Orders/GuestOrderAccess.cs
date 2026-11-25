using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Validation;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using RmaFeature = HKPartsEcommerce.Application.Features.Rma;

namespace HKPartsEcommerce.Application.Features.Orders;

// Guest order lookup and guest → account (docs/guest-checkout-requirements.md §3.7, §3.8, N-04).

/// <summary>U-60: order number plus the order's email, or its billing / shipping ZIP.</summary>
public record LookupGuestOrderCommand(string OrderNumber, string? Email, string? PostalCode, string? RecaptchaToken = null)
    : IRequest<GuestOrderLookupResult>;

/// <summary>A signed token that opens the order page (U-61); the storefront then calls <see cref="GetGuestOrderQuery"/>.</summary>
public record GuestOrderLookupResult(string Token);

public class LookupGuestOrderCommandValidator : AbstractValidator<LookupGuestOrderCommand>
{
    public LookupGuestOrderCommandValidator()
    {
        RuleFor(x => x.OrderNumber).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Email).MaximumLength(254);
        RuleFor(x => x.PostalCode).MaximumLength(20);
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.PostalCode))
            .WithName("Email")
            .WithMessage("Enter the email address used for the order, or its billing ZIP code.");
    }
}

public class LookupGuestOrderCommandHandler : IRequestHandler<LookupGuestOrderCommand, GuestOrderLookupResult>
{
    /// <summary>U-62: failures allowed per IP within <see cref="Window"/>.</summary>
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>The same answer for a wrong number, a wrong email and a wrong ZIP (U-62, N-04).</summary>
    public const string NoMatchMessage = "We couldn't find an order with those details. Please check the order number and email address.";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;
    private readonly IGuestOrderTokenService _tokens;
    private readonly ISettingsService _settings;
    private readonly IRecaptchaVerifier _recaptcha;

    public LookupGuestOrderCommandHandler(
        IApplicationDbContext db, ICurrentUserService current, IDateTimeProvider clock,
        IGuestOrderTokenService tokens, ISettingsService settings, IRecaptchaVerifier recaptcha)
    {
        _db = db;
        _current = current;
        _clock = clock;
        _tokens = tokens;
        _settings = settings;
        _recaptcha = recaptcha;
    }

    public async Task<GuestOrderLookupResult> Handle(LookupGuestOrderCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var ip = _current.IpAddress;
        // N-08: the lookup form is a guest-facing form, protected like guest checkout.
        await _recaptcha.VerifyOrThrowAsync(RecaptchaFormType.GuestCheckout, request.RecaptchaToken, ct);
        var since = now - Window;
        var failures = await _db.GuestOrderAccessLogs.AsNoTracking()
            .CountAsync(l => l.IpAddress == ip && !l.Success && l.AtUtc >= since, ct);
        if (failures >= MaxFailures)
            throw new TooManyRequestsException("Too many attempts. Please wait 15 minutes and try again.");

        var number = request.OrderNumber.Trim();
        var candidate = await _db.Orders.AsNoTracking()
            .GuestOrders()
            .Where(o => o.OrderNumber == number)
            .Select(o => new
            {
                o.Id,
                Email = o.ShippingAddress != null ? o.ShippingAddress.Email : null,
                ShipZip = o.ShippingAddress != null ? o.ShippingAddress.PostalCode : null,
                BillZip = o.BillingAddress != null ? o.BillingAddress.PostalCode : null,
            })
            .FirstOrDefaultAsync(ct);

        // Compare in constant time whatever matched, so timing never tells a right number from a wrong email.
        var emailOk = !string.IsNullOrWhiteSpace(request.Email)
                      && FixedEquals(Guest.NormalizeEmail(request.Email), Guest.NormalizeEmail(candidate?.Email));
        var zip = NormalizeZip(request.PostalCode);
        var zipOk = zip.Length > 0
                    && (FixedEquals(zip, NormalizeZip(candidate?.BillZip)) || FixedEquals(zip, NormalizeZip(candidate?.ShipZip)));
        var success = candidate is not null && (emailOk || zipOk);

        _db.GuestOrderAccessLogs.Add(new GuestOrderAccessLog
        {
            OrderId = success ? candidate!.Id : null,
            IpAddress = ip,
            Success = success,
            Method = "Lookup",
            AtUtc = now,
        });
        await _db.SaveChangesAsync(ct);

        if (!success)
            throw new NotFoundException(NoMatchMessage);

        var guest = await GuestCheckoutSettingsStore.LoadAsync(_settings, ct);
        return new GuestOrderLookupResult(_tokens.Generate(candidate!.Id, TimeSpan.FromDays(guest.LinkLifetimeDays)));
    }

    /// <summary>US ZIPs compare on their first five digits; other postcodes on letters and digits only.</summary>
    internal static string NormalizeZip(string? zip)
    {
        var cleaned = new string((zip ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return cleaned.Length == 9 && cleaned.All(char.IsDigit) ? cleaned[..5] : cleaned;
    }

    private static bool FixedEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}

/// <summary>The read-only guest order page (U-60/U-61): the order, plus what the page may offer next.</summary>
public class GuestOrderViewDto
{
    public OrderDto Order { get; set; } = new();

    /// <summary>The email the receipt went to (the guest's own, shown back to them).</summary>
    public string? Email { get; set; }

    /// <summary>U-72: an account already exists for this email, so the page offers Sign in instead of Create account.</summary>
    public bool AccountExists { get; set; }

    /// <summary>U-63: something on the order can be returned now (the returns policy decides), so "Request a return" shows.</summary>
    public bool CanRequestReturn { get; set; }

    /// <summary>U-63: the order can still be cancelled (nothing shipped yet) and no cancel was requested.</summary>
    public bool CanRequestCancel { get; set; }

    /// <summary>When the guest asked to cancel, and why; null when they haven't.</summary>
    public DateTime? CancelRequestedOnUtc { get; set; }
    public string? CancelRequestReason { get; set; }

    /// <summary>Returns already requested on this order (number, status, date).</summary>
    public List<GuestOrderReturnDto> Returns { get; set; } = new();
}

public class GuestOrderReturnDto
{
    public string RmaNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedOnUtc { get; set; }
}

/// <summary>Shared guest-order rules (U-63): loading the order a token opens, and whether it can still be cancelled.</summary>
public static class GuestOrderAccess
{
    /// <summary>
    /// The guest order a signed token opens: a real storefront order still without an account. An invalid or expired
    /// token is a 404; an order since moved onto an account asks the guest to sign in (409).
    /// </summary>
    public static async Task<Order> LoadGuestOrderAsync(
        IApplicationDbContext db, IGuestOrderTokenService? tokens, string? token,
        Func<IQueryable<Order>, IQueryable<Order>>? include, bool tracking, CancellationToken ct)
    {
        if (tokens is null || !tokens.TryValidate(token, out var orderId))
            throw new NotFoundException(GetGuestOrderQueryHandler.InvalidLinkMessage);
        IQueryable<Order> query = db.Orders;
        if (!tracking) query = query.AsNoTracking();
        if (include is not null) query = include(query);
        var order = await query.FirstOrDefaultAsync(o => o.Id == orderId && o.EbayOrderId == null && !o.IsInquiry, ct)
                    ?? throw new NotFoundException(GetGuestOrderQueryHandler.InvalidLinkMessage);
        if (order.CustomerId is not null)
            throw new ConflictException("This order is now in your account. Please sign in to view it.");
        return order;
    }

    /// <summary>Statuses before anything leaves the warehouse: a cancel can still be honoured (cancel spec CX-01).</summary>
    private static readonly OrderStatus[] CancelableStatuses =
    {
        OrderStatus.Pending, OrderStatus.PendingRouting, OrderStatus.Routed, OrderStatus.Accepted, OrderStatus.Preparing,
    };

    /// <summary>
    /// Why the guest can't ask to cancel, or null when they can (cancel spec CX-46/CX-49): not yet cancelled or
    /// requested, nothing shipped or ready for pickup, and no shipment created.
    /// </summary>
    public static async Task<string?> CancelBlockReasonAsync(IApplicationDbContext db, Order order, CancellationToken ct)
    {
        if (order.CancelRequestedOnUtc is not null)
            return "You have already asked us to cancel this order. We'll email you once it's done.";
        if (order.Status == OrderStatus.Cancelled)
            return "This order is already cancelled.";
        if (!CancelableStatuses.Contains(order.Status) || order.ShippedOnUtc is not null)
            return "This order can no longer be cancelled because it is on its way. You can request a return once it arrives.";
        if (await db.Shipments.AnyAsync(s => s.OrderId == order.Id, ct))
            return "Part of this order has already shipped, so it can no longer be cancelled online. Please contact us.";
        return null;
    }
}

public record GetGuestOrderQuery(string Token) : IRequest<GuestOrderViewDto>;

public class GetGuestOrderQueryHandler : IRequestHandler<GetGuestOrderQuery, GuestOrderViewDto>
{
    public const string InvalidLinkMessage = "This order link is invalid or has expired. Look up your order with its number and email instead.";

    private readonly IApplicationDbContext _db;
    private readonly IGuestOrderTokenService _tokens;
    private readonly ISettingsService? _settings;
    private readonly IDateTimeProvider? _clock;

    public GetGuestOrderQueryHandler(IApplicationDbContext db, IGuestOrderTokenService tokens,
        ISettingsService? settings = null, IDateTimeProvider? clock = null)
    {
        _db = db;
        _tokens = tokens;
        _settings = settings;
        _clock = clock;
    }

    public async Task<GuestOrderViewDto> Handle(GetGuestOrderQuery request, CancellationToken ct)
    {
        var order = await GuestOrderAccess.LoadGuestOrderAsync(_db, _tokens, request.Token,
            q => q.Include(o => o.ShippingAddress)
                .Include(o => o.BillingAddress)
                .Include(o => o.Lines)
                .Include(o => o.Payments)
                .Include(o => o.Shippings)
                .AsSplitQuery(),
            tracking: false, ct);

        var dto = OrderDto.From(order);
        // Never more than the buyer needs (N-04): no gateway transaction id, no internal routing detail.
        dto.PaymentTransactionId = null;
        dto.RoutingChain = null;
        dto.Latitude = null;
        dto.Longitude = null;

        var productIds = order.Lines.Select(l => l.ProductId).Distinct().ToList();
        var published = (await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.IsPublished)
            .Select(p => p.Id)
            .ToListAsync(ct)).ToHashSet();
        foreach (var line in dto.Lines)
            line.ProductAvailable = published.Contains(line.ProductId);

        // U-63: what the page may offer next, under the returns policy and the cancel rules.
        var returns = await _db.Rmas.AsNoTracking()
            .Where(r => r.OrderId == order.Id)
            .OrderByDescending(r => r.CreatedOnUtc)
            .Select(r => new GuestOrderReturnDto { RmaNumber = r.RmaNumber, Status = r.Status.ToString(), CreatedOnUtc = r.CreatedOnUtc })
            .ToListAsync(ct);
        var returnSettings = _settings is null ? new RmaFeature.ReturnSettingsDto() : await RmaFeature.ReturnSettingsStore.LoadAsync(_settings, ct);
        var eligibility = await RmaFeature.ReturnEligibility.EvaluateAsync(
            _db, order, returnSettings, _clock?.UtcNow ?? DateTime.UtcNow, forStaff: false, null, ct);

        var email = Guest.NormalizeEmail(order.ContactEmail);
        return new GuestOrderViewDto
        {
            Order = dto,
            Email = order.ContactEmail,
            AccountExists = email.Length > 0 && await _db.Users.AnyAsync(u => u.Email == email, ct),
            CanRequestReturn = eligibility.Eligible,
            CanRequestCancel = await GuestOrderAccess.CancelBlockReasonAsync(_db, order, ct) is null,
            CancelRequestedOnUtc = order.CancelRequestedOnUtc,
            CancelRequestReason = order.CancelRequestReason,
            Returns = returns,
        };
    }
}

/// <summary>U-63: what a guest can return from the order a token opens (feeds the return form).</summary>
public record GetGuestOrderReturnabilityQuery(string Token) : IRequest<RmaFeature.ReturnEligibilityDto>;

public class GetGuestOrderReturnabilityQueryHandler : IRequestHandler<GetGuestOrderReturnabilityQuery, RmaFeature.ReturnEligibilityDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IGuestOrderTokenService _tokens;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;

    public GetGuestOrderReturnabilityQueryHandler(IApplicationDbContext db, IGuestOrderTokenService tokens, ISettingsService settings, IDateTimeProvider clock)
    {
        _db = db;
        _tokens = tokens;
        _settings = settings;
        _clock = clock;
    }

    public async Task<RmaFeature.ReturnEligibilityDto> Handle(GetGuestOrderReturnabilityQuery request, CancellationToken ct)
    {
        var order = await GuestOrderAccess.LoadGuestOrderAsync(_db, _tokens, request.Token, q => q.Include(o => o.Lines), tracking: false, ct);
        var settings = await RmaFeature.ReturnSettingsStore.LoadAsync(_settings, ct);
        return await RmaFeature.ReturnEligibility.EvaluateAsync(_db, order, settings, _clock.UtcNow, forStaff: false, null, ct);
    }
}

/// <summary>
/// U-63 / cancel spec "request" mode (CX-45..47): a guest asks to cancel an order that has not shipped. Staff decide
/// and cancel it from the admin order page (which voids or refunds the payment). The request is written to the order
/// history and the store's order alert addresses are emailed (CX-53).
/// </summary>
public record RequestGuestOrderCancelCommand(string Token, string Reason, string? Comment = null) : IRequest<GuestOrderViewDto>;

public class RequestGuestOrderCancelCommandValidator : AbstractValidator<RequestGuestOrderCancelCommand>
{
    public RequestGuestOrderCancelCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Choose why you want to cancel.").MaximumLength(100);
        RuleFor(x => x.Comment).MaximumLength(390);
    }
}

public class RequestGuestOrderCancelCommandHandler : IRequestHandler<RequestGuestOrderCancelCommand, GuestOrderViewDto>
{
    /// <summary>The short customer-facing reason list (CX-47).</summary>
    public static readonly string[] Reasons =
    {
        "Ordered by mistake", "Found a better price", "Delivery is too slow", "Need to change the order", "Other",
    };

    private readonly IApplicationDbContext _db;
    private readonly IGuestOrderTokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailEnqueuer _emails;
    private readonly ISender _mediator;

    public RequestGuestOrderCancelCommandHandler(
        IApplicationDbContext db, IGuestOrderTokenService tokens, IDateTimeProvider clock, IEmailEnqueuer emails, ISender mediator)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _emails = emails;
        _mediator = mediator;
    }

    public async Task<GuestOrderViewDto> Handle(RequestGuestOrderCancelCommand request, CancellationToken ct)
    {
        var order = await GuestOrderAccess.LoadGuestOrderAsync(_db, _tokens, request.Token,
            q => q.Include(o => o.ShippingAddress).Include(o => o.AssignedStore), tracking: true, ct);
        if (await GuestOrderAccess.CancelBlockReasonAsync(_db, order, ct) is string blocked)
            throw new ConflictException(blocked);

        var reason = Reasons.FirstOrDefault(r => string.Equals(r, request.Reason.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Other";
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        var now = _clock.UtcNow;
        order.CancelRequestedOnUtc = now;
        order.CancelRequestReason = comment is null ? reason : $"{reason}: {comment}";
        order.StatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = order.Status,
            ToStatus = order.Status,
            ChangedOnUtc = now,
            Note = $"Cancellation requested by the guest ({order.CancelRequestReason}).",
        });
        await _db.SaveChangesAsync(ct);

        // CX-53: alert the people who handle the order (the store's order alert address, else its contact email).
        var store = order.AssignedStore;
        var raw = store is null ? null : string.IsNullOrWhiteSpace(store.NotificationEmail) ? store.ContactEmail : store.NotificationEmail;
        var recipients = (raw ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var body =
            $"A guest asked to cancel order {order.OrderNumber}.\n\n" +
            $"Customer: {order.ContactName} ({order.ContactEmail})\n" +
            $"Reason: {order.CancelRequestReason}\n" +
            $"Order total: {order.CurrencyCode} {order.TotalAmount:0.00}\n\n" +
            "Open the order in the admin to cancel it (the payment is voided or refunded there) or contact the customer.";
        foreach (var to in recipients)
            await _emails.EnqueueAsync("order.cancel-request", to, store?.Name, $"Cancellation requested: {order.OrderNumber}",
                body, NotificationCategory.Transactional, cancellationToken: ct);

        return await _mediator.Send(new GetGuestOrderQuery(request.Token), ct);
    }
}

/// <summary>
/// U-70: a guest sets a password to turn their order's email, name and addresses into an account. The account is
/// created unverified and a verification email is sent: the token only proves someone knew the order details, not
/// that they own the mailbox, so the guest orders are linked when the email is verified (U-71, N-07).
/// </summary>
public record CreateAccountFromGuestOrderCommand(string Token, string Password) : IRequest<CreateAccountFromGuestOrderResult>;

public record CreateAccountFromGuestOrderResult(string Email);

public class CreateAccountFromGuestOrderCommandValidator : AbstractValidator<CreateAccountFromGuestOrderCommand>
{
    public CreateAccountFromGuestOrderCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password).Password();
    }
}

public class CreateAccountFromGuestOrderCommandHandler : IRequestHandler<CreateAccountFromGuestOrderCommand, CreateAccountFromGuestOrderResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IGuestOrderTokenService _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailTemplateSender _templates;
    private readonly IStorefrontUrlBuilder _urls;

    public CreateAccountFromGuestOrderCommandHandler(
        IApplicationDbContext db, IGuestOrderTokenService tokens, IPasswordHasher hasher, IJwtTokenService jwt,
        IDateTimeProvider clock, IEmailTemplateSender templates, IStorefrontUrlBuilder urls)
    {
        _db = db;
        _tokens = tokens;
        _hasher = hasher;
        _jwt = jwt;
        _clock = clock;
        _templates = templates;
        _urls = urls;
    }

    public async Task<CreateAccountFromGuestOrderResult> Handle(CreateAccountFromGuestOrderCommand request, CancellationToken ct)
    {
        if (!_tokens.TryValidate(request.Token, out var orderId))
            throw new NotFoundException(GetGuestOrderQueryHandler.InvalidLinkMessage);

        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.ShippingAddress)
            .Include(o => o.BillingAddress)
            .GuestOrders()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException(GetGuestOrderQueryHandler.InvalidLinkMessage);

        var email = Guest.NormalizeEmail(order.ContactEmail);
        if (email.Length == 0)
            throw new ConflictException("This order has no email address, so an account can't be created from it.");
        // U-72: never a second account for the same email.
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account already exists for this email. Please sign in instead.");

        var now = _clock.UtcNow;
        var ship = order.ShippingAddress;
        var bill = order.BillingAddress;
        var firstName = FirstNonEmpty(ship?.FirstName, bill?.FirstName) ?? string.Empty;
        var lastName = FirstNonEmpty(ship?.LastName, bill?.LastName) ?? string.Empty;
        var customerRoleId = await _db.Roles
            .Where(r => r.Name == nameof(RoleType.Customer))
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);

        var user = new User
        {
            Username = await UniqueUsernameAsync(email, ct),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            EmailVerified = false,
            IsActive = true,
            Customer = new Customer
            {
                FirstName = firstName,
                LastName = lastName,
                PhoneNumber = FirstNonEmpty(ship?.PhoneNumber, bill?.PhoneNumber),
            },
        };
        if (customerRoleId is Guid roleId)
            user.UserRoles.Add(new UserRole { RoleId = roleId, AssignedOnUtc = now });
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // The order's addresses go into the new address book (U-70): delivery as default shipping, bill-to as
        // default billing. A pickup / FFL-only order has no buyer postal ship-to, so only its billing is copied.
        if (ship is not null && !string.IsNullOrWhiteSpace(ship.AddressLine1) && !order.IsPickup)
            _db.CustomerAddresses.Add(CopyAddress(user.Customer.Id, ship, AddressType.Shipping));
        if (bill is not null && !string.IsNullOrWhiteSpace(bill.AddressLine1))
            _db.CustomerAddresses.Add(CopyAddress(user.Customer.Id, bill, AddressType.Billing));

        var rawToken = _jwt.GenerateRefreshToken();
        _db.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.EmailVerification,
            TokenHash = _jwt.HashRefreshToken(rawToken),
            CreatedOnUtc = now,
            ExpiresOnUtc = now.AddHours(24),
        });
        await _db.SaveChangesAsync(ct);

        var fullName = $"{firstName} {lastName}".Trim();
        await _templates.SendAsync(
            "account.email-verification",
            email,
            fullName,
            new Dictionary<string, string>
            {
                ["customerName"] = string.IsNullOrWhiteSpace(fullName) ? "there" : fullName,
                ["verificationUrl"] = _urls.EmailVerificationUrl(rawToken),
            },
            ct);

        return new CreateAccountFromGuestOrderResult(email);
    }

    private static CustomerAddress CopyAddress(Guid customerId, Address a, AddressType type) => new()
    {
        CustomerId = customerId,
        AddressType = type,
        IsDefault = true,
        Address = new Address
        {
            FirstName = a.FirstName,
            LastName = a.LastName,
            Company = a.Company,
            AddressLine1 = a.AddressLine1,
            AddressLine2 = a.AddressLine2,
            Landmark = a.Landmark,
            City = a.City,
            StateProvince = a.StateProvince,
            PostalCode = a.PostalCode,
            CountryCode = a.CountryCode,
            PhoneNumber = a.PhoneNumber,
        },
    };

    private async Task<string> UniqueUsernameAsync(string email, CancellationToken ct)
    {
        var baseName = email.Split('@')[0];
        var candidate = baseName;
        var suffix = 1;
        while (await _db.Users.AnyAsync(u => u.Username == candidate, ct))
            candidate = $"{baseName}{suffix++}";
        return candidate;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
