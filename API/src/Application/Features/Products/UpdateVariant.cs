using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Updates the purchasable configuration of a single variant (AC-CAT-002.3/4).</summary>
public record UpdateVariantCommand(
    Guid VariantId,
    string? Sku = null,
    decimal? Price = null,
    int StockQuantity = 0,
    bool AllowBackorder = false,
    bool IsEnabled = true,
    int DisplayOrder = 0) : IRequest<ProductVariantDto>;

public class UpdateVariantCommandValidator : AbstractValidator<UpdateVariantCommand>
{
    public UpdateVariantCommandValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.Sku).MaximumLength(400);
    }
}

public class UpdateVariantCommandHandler : IRequestHandler<UpdateVariantCommand, ProductVariantDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateVariantCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductVariantDto> Handle(UpdateVariantCommand request, CancellationToken cancellationToken)
    {
        var variant = await _db.ProductVariants
            .Include(v => v.AttributeValues)
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductVariant), request.VariantId);

        variant.Sku = request.Sku;
        variant.Price = request.Price;
        variant.AllowBackorder = request.AllowBackorder;
        variant.IsEnabled = request.IsEnabled;
        variant.DisplayOrder = request.DisplayOrder;

        await _db.SaveChangesAsync(cancellationToken);
        return ProductVariantDto.From(variant);
    }
}
