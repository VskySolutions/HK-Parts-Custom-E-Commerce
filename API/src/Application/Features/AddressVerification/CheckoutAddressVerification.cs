using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Application.Features.Shipping;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.AddressVerification;

/// <summary>
/// One check of a buyer's ship-to address: the P.O. box rule (AV-30, runs even with verification off — AV-31),
/// then the provider (AV-02, AV-10..14). Shared by the storefront's verify call and the placement re-check, so
/// the two cannot disagree.
/// </summary>
public class CheckoutAddressCheck
{
    public AddressVerificationStatus Status { get; init; }
    public bool Blocking { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public List<string> Messages { get; init; } = new();
    public VerifiableAddress? Suggestion { get; init; }
    public string AddressHash { get; init; } = string.Empty;

    public CheckoutAddressVerificationDto ToDto() => new()
    {
        Status = AddressVerificationSupport.WireStatus(Status),
        CanContinue = !Blocking,
        Code = Code,
        Message = Message,
        Messages = Messages,
        Suggestion = Status == AddressVerificationStatus.Suggested ? VerificationAddressDto.From(Suggestion) : null,
    };

    /// <summary>The 409 a blocking check raises at placement (ProblemDetails code + the provider's reasons).</summary>
    public Exception ToConflict() =>
        CheckoutErrorCodes.Conflict(Code ?? AddressVerificationCodes.Invalid, Message ?? AddressVerificationCodes.DefaultFailedMessage,
            new Dictionary<string, object?> { ["messages"] = Messages });
}

public static class CheckoutAddressGate
{
    public static async Task<CheckoutAddressCheck> CheckAsync(
        IApplicationDbContext db, IAddressVerificationService verifier, CheckoutAddress shipTo, CancellationToken ct)
    {
        var address = AddressVerificationSupport.ToVerifiable(shipTo);
        var hash = AddressVerificationSupport.Hash(address);

        var rules = await ShippingRulesEngine.LoadAsync(db, ct);
        if (rules.RefusePoBoxes && PoBoxDetector.IsPoBox(shipTo.Line1, shipTo.Line2))
            return new CheckoutAddressCheck
            {
                Status = AddressVerificationStatus.PoBox,
                Blocking = true,
                Code = AddressVerificationCodes.PoBox,
                Message = ShippingRulesEngine.PoBoxRefusal(rules),
                AddressHash = hash,
            };

        var settings = await AddressVerificationSupport.LoadAsync(db, ct);
        var outcome = await verifier.VerifyAsync(address, ct);

        var failed = string.IsNullOrWhiteSpace(settings.FailedMessage)
            ? AddressVerificationCodes.DefaultFailedMessage
            : settings.FailedMessage!;

        return outcome.Status switch
        {
            AddressVerificationStatus.Invalid => new CheckoutAddressCheck
            {
                Status = outcome.Status, Blocking = true, Code = AddressVerificationCodes.Invalid,
                Message = failed, Messages = outcome.Messages, AddressHash = hash,
            },
            // AV-14: never block because the provider is down, unless the admin chose to.
            AddressVerificationStatus.Unavailable => new CheckoutAddressCheck
            {
                Status = outcome.Status,
                Blocking = settings.OnUnavailable == AddressUnavailablePolicy.Block,
                Code = settings.OnUnavailable == AddressUnavailablePolicy.Block ? AddressVerificationCodes.Unavailable : null,
                Message = settings.OnUnavailable == AddressUnavailablePolicy.Block ? AddressVerificationCodes.DefaultUnavailableMessage : null,
                AddressHash = hash,
            },
            AddressVerificationStatus.Suggested => new CheckoutAddressCheck
            {
                Status = outcome.Status, Messages = outcome.Messages, Suggestion = outcome.Suggestion, AddressHash = hash,
            },
            _ => new CheckoutAddressCheck { Status = outcome.Status, AddressHash = hash },
        };
    }

    /// <summary>
    /// The caller's active cart, resolved like checkout does (cart id, the signed-in customer's cart, else the
    /// session cart), or null when there is none — verification still answers, it just records nothing.
    /// </summary>
    public static async Task<Domain.Entities.Cart?> FindCartAsync(
        IApplicationDbContext db, ICurrentUserService current, Guid? cartId, string? sessionId, CancellationToken ct)
    {
        if (cartId is Guid id)
            return await db.Carts.FirstOrDefaultAsync(c => c.Id == id && !c.IsCheckedOut, ct);

        if (current.UserId is Guid userId)
        {
            var customerId = await db.Customers.AsNoTracking()
                .Where(c => c.UserId == userId).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
            if (customerId is not null)
            {
                var customerCart = await db.Carts.FirstOrDefaultAsync(c => c.CustomerId == customerId && !c.IsCheckedOut, ct);
                if (customerCart is not null)
                    return customerCart;
            }
        }

        var session = sessionId?.Trim();
        if (string.IsNullOrEmpty(session))
            return null;
        return await db.Carts.FirstOrDefaultAsync(c => c.SessionId == session && c.CustomerId == null && !c.IsCheckedOut, ct);
    }
}

/// <summary>
/// <c>POST /api/checkout/address/verify</c> (§6.1, AV-01): the storefront calls it when the buyer saves the
/// delivery address, before shipping is rated. The outcome is recorded on the cart (AV-06).
/// </summary>
public record VerifyCheckoutAddressCommand(Guid? CartId, string? SessionId, CheckoutAddress ShipTo)
    : IRequest<CheckoutAddressVerificationDto>;

public class VerifyCheckoutAddressCommandValidator : AbstractValidator<VerifyCheckoutAddressCommand>
{
    public VerifyCheckoutAddressCommandValidator()
    {
        RuleFor(x => x.ShipTo).NotNull();
        RuleFor(x => x.ShipTo.Line1).NotEmpty().MaximumLength(256).When(x => x.ShipTo is not null);
        RuleFor(x => x.ShipTo.City).NotEmpty().MaximumLength(128).When(x => x.ShipTo is not null);
        RuleFor(x => x.ShipTo.CountryCode).NotEmpty().MaximumLength(2).When(x => x.ShipTo is not null);
    }
}

public class VerifyCheckoutAddressCommandHandler : IRequestHandler<VerifyCheckoutAddressCommand, CheckoutAddressVerificationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IAddressVerificationService _verifier;
    private readonly ICurrentUserService _current;

    public VerifyCheckoutAddressCommandHandler(IApplicationDbContext db, IAddressVerificationService verifier, ICurrentUserService current)
    {
        _db = db;
        _verifier = verifier;
        _current = current;
    }

    public async Task<CheckoutAddressVerificationDto> Handle(VerifyCheckoutAddressCommand request, CancellationToken ct)
    {
        var check = await CheckoutAddressGate.CheckAsync(_db, _verifier, request.ShipTo, ct);

        var cart = await CheckoutAddressGate.FindCartAsync(_db, _current, request.CartId, request.SessionId, ct);
        if (cart is not null)
        {
            cart.ShipToAddressHash = check.AddressHash;
            cart.ShipToVerifyStatus = check.Status;
            cart.ShipToAddressOverridden = false;
            await _db.SaveChangesAsync(ct);
        }

        return check.ToDto();
    }
}

/// <summary>
/// <c>POST /api/checkout/address/accept</c> (§6.1, AV-13): the buyer's answer to "We suggest: …".
/// <see cref="KeepMine"/> = false: <see cref="ShipTo"/> is the suggested address the buyer took, saved as verified.
/// <see cref="KeepMine"/> = true: the buyer keeps their own address; the order is flagged "customer override"
/// (AV-15). An address the provider could not find cannot be kept (AV-10).
/// </summary>
public record AcceptCheckoutAddressCommand(Guid? CartId, string? SessionId, CheckoutAddress ShipTo, bool KeepMine)
    : IRequest<CheckoutAddressVerificationDto>;

public class AcceptCheckoutAddressCommandValidator : AbstractValidator<AcceptCheckoutAddressCommand>
{
    public AcceptCheckoutAddressCommandValidator()
    {
        RuleFor(x => x.ShipTo).NotNull();
        RuleFor(x => x.ShipTo.Line1).NotEmpty().MaximumLength(256).When(x => x.ShipTo is not null);
        RuleFor(x => x.ShipTo.CountryCode).NotEmpty().MaximumLength(2).When(x => x.ShipTo is not null);
    }
}

public class AcceptCheckoutAddressCommandHandler : IRequestHandler<AcceptCheckoutAddressCommand, CheckoutAddressVerificationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IAddressVerificationService _verifier;
    private readonly ICurrentUserService _current;

    public AcceptCheckoutAddressCommandHandler(IApplicationDbContext db, IAddressVerificationService verifier, ICurrentUserService current)
    {
        _db = db;
        _verifier = verifier;
        _current = current;
    }

    public async Task<CheckoutAddressVerificationDto> Handle(AcceptCheckoutAddressCommand request, CancellationToken ct)
    {
        var check = await CheckoutAddressGate.CheckAsync(_db, _verifier, request.ShipTo, ct);
        if (check.Blocking)
            throw check.ToConflict();

        // The suggestion the provider standardised can itself come back "suggested" (e.g. ZIP+4 formatting);
        // the buyer accepted the provider's own version, so it counts as verified.
        var status = !request.KeepMine && check.Status == AddressVerificationStatus.Suggested
            ? AddressVerificationStatus.Valid
            : check.Status;
        var overridden = request.KeepMine && check.Status == AddressVerificationStatus.Suggested;

        var cart = await CheckoutAddressGate.FindCartAsync(_db, _current, request.CartId, request.SessionId, ct);
        if (cart is not null)
        {
            cart.ShipToAddressHash = check.AddressHash;
            cart.ShipToVerifyStatus = status;
            cart.ShipToAddressOverridden = overridden;
            await _db.SaveChangesAsync(ct);
        }

        var dto = check.ToDto();
        dto.Status = AddressVerificationSupport.WireStatus(status);
        dto.Suggestion = null;
        return dto;
    }
}
