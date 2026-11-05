using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Cart;
using HKPartsEcommerce.Domain.Enums;
using CartEntity = HKPartsEcommerce.Domain.Entities.Cart;

namespace HKPartsEcommerce.Application.Features.GiftCertificates;

/// <summary>Cart-stage gift certificate state, shared by the cart DTO, apply and totals endpoints.</summary>
public static class GiftCertificateCartSupport
{
    /// <summary>
    /// Re-validates the cart's certificate. Invalid (deactivated, deleted, spent, expired) → detached from the
    /// cart (saved) and the "no longer valid" message is returned. Otherwise the summary for
    /// <paramref name="grandTotal"/>, using the one apply formula.
    /// </summary>
    public static async Task<(CartGiftCertificateDto? Summary, string? Warning)> RefreshAsync(
        IApplicationDbContext db, CartEntity cart, decimal grandTotal, CancellationToken ct)
    {
        if (cart.GiftCertificateId is not Guid gcId)
            return (null, null);

        var gc = await db.GiftCertificates.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(g => g.Id == gcId, ct);
        if (gc is null || !GiftCertificateRules.IsRedeemable(gc, DateTime.UtcNow))
        {
            cart.GiftCertificateId = null;
            await db.SaveChangesAsync(ct);
            return (null, GiftCertificateRules.NoLongerValidMessage);
        }

        var apply = GiftCertificateRules.ApplyAmount(gc.CurrentBalance, grandTotal);
        return (new CartGiftCertificateDto
        {
            Code = gc.Code,
            CurrentBalance = gc.CurrentBalance,
            ApplyAmount = apply,
            RemainingTotal = Math.Max(0m, grandTotal - apply),
        }, null);
    }

    public static async Task<bool> ContainsGiftCertificateProductAsync(IApplicationDbContext db, CartEntity cart, CancellationToken ct)
    {
        var ids = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        return ids.Count > 0 && await db.Products.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(p => ids.Contains(p.Id) && p.ProductType == ProductType.GiftCard, ct);
    }
}

// ---- Apply ----------------------------------------------------------------------------------------------

public record ApplyCartGiftCertificateCommand(string Code, string? SessionId = null) : IRequest<CartGiftCertificateDto>;

public class ApplyCartGiftCertificateHandler : IRequestHandler<ApplyCartGiftCertificateCommand, CartGiftCertificateDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICustomerGroupService _groups;
    private readonly ICommerceModeService _commerce;
    private readonly IGiftCertificateService _service;
    private readonly ISettingsService _settings;

    public ApplyCartGiftCertificateHandler(
        IApplicationDbContext db, ICurrentUserService current, ICustomerGroupService groups, ICommerceModeService commerce,
        IGiftCertificateService service, ISettingsService settings)
    {
        _db = db;
        _current = current;
        _groups = groups;
        _commerce = commerce;
        _service = service;
        _settings = settings;
    }

    public async Task<CartGiftCertificateDto> Handle(ApplyCartGiftCertificateCommand request, CancellationToken ct)
    {
        var cart = await CartResolver.ResolveOrCreateAsync(_db, _current, request.SessionId, ct);

        if (await GiftCertificateCartSupport.ContainsGiftCertificateProductAsync(_db, cart, ct)
            && !await GiftCertificateSettings.AllowRedeemOnGcPurchaseAsync(_settings, ct))
            throw new GiftCertificateUnavailableException(GiftCertificateRules.GcForGcMessage);

        var storeId = await StoreScope.ResolveAsync(_db, null, ct);
        // One message for unknown / inactive / deleted / spent / expired: never reveal that a code exists.
        var gc = await _service.FindRedeemableAsync(storeId, request.Code, ct)
                 ?? throw new GiftCertificateUnavailableException(GiftCertificateRules.InvalidMessage);

        // One certificate per cart: applying another replaces the first.
        cart.GiftCertificateId = gc.Id;
        await _db.SaveChangesAsync(ct);

        var dto = await CartResolver.BuildDtoAsync(_db, _groups, _commerce, cart, ct);
        return dto.GiftCertificate ?? throw new GiftCertificateUnavailableException(GiftCertificateRules.InvalidMessage);
    }
}

public record RemoveCartGiftCertificateCommand(string? SessionId = null) : IRequest;

public class RemoveCartGiftCertificateHandler : IRequestHandler<RemoveCartGiftCertificateCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public RemoveCartGiftCertificateHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task Handle(RemoveCartGiftCertificateCommand request, CancellationToken ct)
    {
        var cart = await CartResolver.ResolveOrCreateAsync(_db, _current, request.SessionId, ct);
        cart.GiftCertificateId = null;
        await _db.SaveChangesAsync(ct);
    }
}

// ---- Totals ---------------------------------------------------------------------------------------------

public class CartTotalsDto
{
    public decimal Subtotal { get; set; }
    /// <summary>The cart-stage grand total (tax and shipping are added at checkout).</summary>
    public decimal GrandTotal { get; set; }
    public CartGiftCertificateDto? GiftCertificate { get; set; }
    public decimal AmountDue { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public record GetCartTotalsQuery(string? SessionId = null) : IRequest<CartTotalsDto>;

public class GetCartTotalsHandler : IRequestHandler<GetCartTotalsQuery, CartTotalsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICustomerGroupService _groups;
    private readonly ICommerceModeService _commerce;

    public GetCartTotalsHandler(IApplicationDbContext db, ICurrentUserService current, ICustomerGroupService groups, ICommerceModeService commerce)
    {
        _db = db;
        _current = current;
        _groups = groups;
        _commerce = commerce;
    }

    public async Task<CartTotalsDto> Handle(GetCartTotalsQuery request, CancellationToken ct)
    {
        var cart = await CartResolver.ResolveOrCreateAsync(_db, _current, request.SessionId, ct);
        await _db.SaveChangesAsync(ct);
        var dto = await CartResolver.BuildDtoAsync(_db, _groups, _commerce, cart, ct);
        return new CartTotalsDto
        {
            Subtotal = dto.Subtotal,
            GrandTotal = dto.Subtotal,
            GiftCertificate = dto.GiftCertificate,
            AmountDue = dto.AmountDue,
            Warnings = dto.Warnings,
        };
    }
}
