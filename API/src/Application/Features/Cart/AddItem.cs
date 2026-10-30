using System.Globalization;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using ValidationException = HKPartsEcommerce.Application.Common.Exceptions.ValidationException;

namespace HKPartsEcommerce.Application.Features.Cart;

/// <summary>A value the buyer typed into one of the product's CustomInput attributes.</summary>
public record CustomAttributeInput(Guid AttributeId, string? Value);

/// <summary>
/// Adds a product (optionally a specific variant) to the caller's cart, snapshotting the current unit
/// price (AC-CHK-001.1). When a line for the same product/variant — and the same custom-input values —
/// already exists its quantity is incremented rather than duplicated; a different engraving is a
/// different line. <see cref="SessionId"/> identifies a guest cart.
/// </summary>
public record AddItemCommand(
    Guid ProductId,
    Guid? ProductVariantId,
    int Quantity,
    string? SessionId = null,
    List<CustomAttributeInput>? CustomAttributes = null) : IRequest<CartDto>;

public class AddItemCommandValidator : AbstractValidator<AddItemCommand>
{
    public AddItemCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
        RuleForEach(x => x.CustomAttributes).ChildRules(v => v.RuleFor(i => i.AttributeId).NotEmpty());
    }
}

public class AddItemCommandHandler : IRequestHandler<AddItemCommand, CartDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICustomerGroupService _groups;
    private readonly ICommerceModeService _commerce;

    public AddItemCommandHandler(
        IApplicationDbContext db, ICurrentUserService current, ICustomerGroupService groups,
        ICommerceModeService commerce)
    {
        _db = db;
        _current = current;
        _groups = groups;
        _commerce = commerce;
    }

    public async Task<CartDto> Handle(AddItemCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (!product.IsPublished)
            throw new ConflictException("This product is not available for purchase.");

        decimal? variantPrice = null;
        if (request.ProductVariantId is Guid variantId)
        {
            var variant = await _db.ProductVariants
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == variantId, cancellationToken)
                ?? throw new NotFoundException(nameof(ProductVariant), variantId);

            if (variant.ProductId != product.Id)
                throw new ConflictException("The selected variant does not belong to the specified product.");
            if (!variant.IsEnabled)
                throw new ConflictException("The selected variant is not available for purchase.");

            variantPrice = variant.Price;
        }

        // Snapshot the price at the moment of adding (AC-CHK-001.1): variant price wins, else product price, else 0.
        // This snapshot stays the BASE price — Customer Group pricing is overlaid at projection time
        // (AC-CUS-003.5) and re-resolved from this same base at checkout, so persisting a group price here
        // would apply a percentage rule twice (and would freeze a member's price against later group changes).
        var unitPrice = variantPrice ?? product.Price ?? 0m;

        var cart = await CartResolver.ResolveOrCreateAsync(_db, _current, request.SessionId, cancellationToken);

        // A cart is either all-buyable or all quote-only (REQ-INQ-001): the two check out through
        // different flows, so mixing them would mean one click producing both an order and an inquiry.
        // Skipped entirely when the whole tenant sells by inquiry — everything is quote-only there.
        if (!await _commerce.IsInquiryOnlyAsync(cancellationToken) && cart.Items.Count > 0)
        {
            var existingProductIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
            var cartHasInquiryItem = await _db.Products
                .AsNoTracking()
                .AnyAsync(p => existingProductIds.Contains(p.Id) && p.IsInquiryOnly, cancellationToken);

            if (cartHasInquiryItem != product.IsInquiryOnly)
            {
                throw new ConflictException(product.IsInquiryOnly
                    ? "Quote-only items must be requested on their own. Please empty your cart first, or " +
                      "check out the items already in it."
                    : "Your cart holds a quote-only item. Submit that request first, or empty your cart " +
                      "before adding items you can buy online.");
            }
        }

        var customAttributesJson = await ResolveCustomAttributesAsync(product.Id, request.CustomAttributes, cancellationToken);
        var signature = Common.Models.CustomAttributes.Signature(customAttributesJson);

        // Same product, same variant AND same typed-in values — otherwise it's a distinct line.
        var existing = cart.Items.FirstOrDefault(
            i => i.ProductId == request.ProductId
                 && i.ProductVariantId == request.ProductVariantId
                 && Common.Models.CustomAttributes.Signature(i.CustomAttributesJson) == signature);

        if (existing is not null)
        {
            existing.Quantity += request.Quantity;
        }
        else
        {
            cart.Items.Add(new CartItem
            {
                ProductId = request.ProductId,
                ProductVariantId = request.ProductVariantId,
                Quantity = request.Quantity,
                UnitPrice = unitPrice,
                CustomAttributesJson = customAttributesJson,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await CartResolver.BuildDtoAsync(_db, _groups, _commerce, cart, cancellationToken);
    }

    /// <summary>
    /// Validates the buyer's typed values against the product's assigned CustomInput attributes and
    /// returns the snapshot to store (null when the product has none, or none were filled in). A
    /// mandatory field left blank, an over-long value or a non-numeric value in a Number field is a 400 —
    /// the storefront blocks all three, so this is the guard for anything that bypasses it. Values for
    /// attributes the product doesn't carry are ignored, so a stale product page can't fail the add.
    /// The attribute's name is captured here so the line keeps reading correctly after a later rename.
    /// </summary>
    private async Task<string?> ResolveCustomAttributesAsync(
        Guid productId, List<CustomAttributeInput>? supplied, CancellationToken ct)
    {
        var attributes = await _db.ProductAttributeMappings
            .AsNoTracking()
            .Where(m => m.ProductId == productId
                        && m.ProductAttribute!.DisplayType == ProductAttributeDisplayType.CustomInput)
            .OrderBy(m => m.DisplayOrder)
            .Select(m => m.ProductAttribute!)
            .ToListAsync(ct);

        if (attributes.Count == 0)
            return null;

        var byId = (supplied ?? new())
            .GroupBy(v => v.AttributeId)
            .ToDictionary(g => g.Key, g => g.Last().Value?.Trim() ?? string.Empty);

        var failures = new List<ValidationFailure>();
        var selections = new List<CustomAttributeSelection>();

        foreach (var attribute in attributes)
        {
            var value = byId.TryGetValue(attribute.Id, out var v) ? v : string.Empty;

            if (value.Length == 0)
            {
                if (attribute.IsRequired)
                    failures.Add(new ValidationFailure("customAttributes", $"'{attribute.Name}' is required."));
                continue;
            }

            if (attribute.MaxLength is int max && value.Length > max)
            {
                failures.Add(new ValidationFailure("customAttributes",
                    $"'{attribute.Name}' may be at most {max} characters."));
                continue;
            }

            if (attribute.InputType == ProductAttributeInputType.Number
                && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            {
                failures.Add(new ValidationFailure("customAttributes", $"'{attribute.Name}' must be a number."));
                continue;
            }

            selections.Add(new CustomAttributeSelection
            {
                AttributeId = attribute.Id,
                Name = attribute.Name,
                Value = value,
            });
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return Common.Models.CustomAttributes.Serialize(selections);
    }
}
