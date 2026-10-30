using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductAttributes;

/// <summary>
/// Updates a product attribute and reconciles its values: adds new, updates existing by id, removes
/// the rest (AC-CAT-003.1). A CustomInput attribute has no values, so its existing ones are left
/// untouched — switching an attribute to CustomInput must not silently delete values that existing
/// variants still reference, and switching back restores the picker as it was.
/// </summary>
public record UpdateProductAttributeCommand(
    Guid Id,
    string Name,
    string? Description = null,
    ProductAttributeDisplayType DisplayType = ProductAttributeDisplayType.Dropdown,
    int DisplayOrder = 0,
    List<ProductAttributeValueInput>? Values = null,
    ProductAttributeInputType InputType = ProductAttributeInputType.Text,
    int? MaxLength = null,
    bool IsRequired = false) : IRequest<ProductAttributeDto>;

public class UpdateProductAttributeCommandValidator : AbstractValidator<UpdateProductAttributeCommand>
{
    public UpdateProductAttributeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.DisplayType).IsInEnum();
        RuleFor(x => x.InputType).IsInEnum();
        RuleFor(x => x.MaxLength).InclusiveBetween(1, 4000).When(x => x.MaxLength.HasValue);
        RuleForEach(x => x.Values).ChildRules(v =>
        {
            v.RuleFor(i => i.Value).NotEmpty().MaximumLength(400);
            v.RuleFor(i => i.ColorHex).MaximumLength(9);
        });
    }
}

public class UpdateProductAttributeCommandHandler : IRequestHandler<UpdateProductAttributeCommand, ProductAttributeDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateProductAttributeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductAttributeDto> Handle(UpdateProductAttributeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductAttributes
            .Include(a => a.Values)
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductAttribute), request.Id);

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.DisplayType = request.DisplayType;
        entity.DisplayOrder = request.DisplayOrder;

        var settings = CreateProductAttributeCommandHandler.NormalizeInput(
            request.DisplayType, request.InputType, request.MaxLength, request.IsRequired);
        entity.InputType = settings.InputType;
        entity.MaxLength = settings.MaxLength;
        entity.IsRequired = settings.IsRequired;

        if (CreateProductAttributeCommandHandler.IsCustomInput(request.DisplayType))
        {
            await _db.SaveChangesAsync(cancellationToken);
            return ProductAttributeDto.From(entity);
        }

        var inputs = request.Values ?? new();
        var keptIds = inputs
            .Where(i => i.Id.HasValue && i.Id.Value != Guid.Empty)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        foreach (var existing in entity.Values.Where(v => !keptIds.Contains(v.Id)).ToList())
        {
            entity.Values.Remove(existing);
            _db.ProductAttributeValues.Remove(existing);
        }

        foreach (var input in inputs)
        {
            var existing = input.Id is { } id && id != Guid.Empty
                ? entity.Values.FirstOrDefault(v => v.Id == id)
                : null;

            var color = CreateProductAttributeCommandHandler.NormalizeColor(request.DisplayType, input.ColorHex);
            if (existing is null)
            {
                entity.Values.Add(new ProductAttributeValue
                {
                    Value = input.Value,
                    ColorHex = color,
                    DisplayOrder = input.DisplayOrder,
                });
            }
            else
            {
                existing.Value = input.Value;
                existing.ColorHex = color;
                existing.DisplayOrder = input.DisplayOrder;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ProductAttributeDto.From(entity);
    }
}
