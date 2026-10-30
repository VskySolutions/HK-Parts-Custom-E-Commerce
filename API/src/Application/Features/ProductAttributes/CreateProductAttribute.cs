using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductAttributes;

/// <summary>An attribute value to create, or (when <see cref="Id"/> is supplied) update during reconciliation.</summary>
public record ProductAttributeValueInput(Guid? Id, string Value, int DisplayOrder, string? ColorHex = null);

/// <summary>
/// Creates a product attribute together with its selectable values (AC-CAT-003.1). A CustomInput
/// attribute has no values — the buyer types their own — so any supplied values are ignored and the
/// InputType/MaxLength/IsRequired settings apply instead.
/// </summary>
public record CreateProductAttributeCommand(
    string Name,
    string? Description = null,
    ProductAttributeDisplayType DisplayType = ProductAttributeDisplayType.Dropdown,
    int DisplayOrder = 0,
    List<ProductAttributeValueInput>? Values = null,
    ProductAttributeInputType InputType = ProductAttributeInputType.Text,
    int? MaxLength = null,
    bool IsRequired = false) : IRequest<ProductAttributeDto>;

public class CreateProductAttributeCommandValidator : AbstractValidator<CreateProductAttributeCommand>
{
    public CreateProductAttributeCommandValidator()
    {
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

public class CreateProductAttributeCommandHandler : IRequestHandler<CreateProductAttributeCommand, ProductAttributeDto>
{
    private readonly IApplicationDbContext _db;

    public CreateProductAttributeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductAttributeDto> Handle(CreateProductAttributeCommand request, CancellationToken cancellationToken)
    {
        var input = NormalizeInput(request.DisplayType, request.InputType, request.MaxLength, request.IsRequired);
        var entity = new ProductAttribute
        {
            Name = request.Name,
            Description = request.Description,
            DisplayType = request.DisplayType,
            DisplayOrder = request.DisplayOrder,
            InputType = input.InputType,
            MaxLength = input.MaxLength,
            IsRequired = input.IsRequired,
        };

        if (!IsCustomInput(request.DisplayType))
        {
            foreach (var value in request.Values ?? new())
            {
                entity.Values.Add(new ProductAttributeValue
                {
                    Value = value.Value,
                    ColorHex = NormalizeColor(request.DisplayType, value.ColorHex),
                    DisplayOrder = value.DisplayOrder,
                });
            }
        }

        _db.ProductAttributes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ProductAttributeDto.From(entity);
    }

    /// <summary>A colour is meaningful only for Swatch attributes; other display types store null.</summary>
    internal static string? NormalizeColor(ProductAttributeDisplayType displayType, string? colorHex) =>
        displayType == ProductAttributeDisplayType.Swatch && !string.IsNullOrWhiteSpace(colorHex)
            ? colorHex.Trim()
            : null;

    /// <summary>An attribute whose value the buyer types rather than picks (so it carries no values).</summary>
    internal static bool IsCustomInput(ProductAttributeDisplayType displayType) =>
        displayType == ProductAttributeDisplayType.CustomInput;

    /// <summary>
    /// The buyer-input settings are meaningful only for CustomInput attributes; every other display
    /// type stores the defaults so a switched-away attribute doesn't keep stale constraints.
    /// </summary>
    internal static (ProductAttributeInputType InputType, int? MaxLength, bool IsRequired) NormalizeInput(
        ProductAttributeDisplayType displayType,
        ProductAttributeInputType inputType,
        int? maxLength,
        bool isRequired) =>
        IsCustomInput(displayType)
            ? (inputType, maxLength > 0 ? maxLength : null, isRequired)
            : (ProductAttributeInputType.Text, null, false);
}
