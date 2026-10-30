using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductCustomFields;

/// <summary>Creates a product custom field. The code must be unique.</summary>
public record CreateProductCustomFieldCommand(
    string Name,
    string Code,
    ProductCustomFieldType FieldType = ProductCustomFieldType.Text,
    bool IsRequired = false,
    bool IsActive = true,
    int DisplayOrder = 0) : IRequest<ProductCustomFieldDto>;

public class CreateProductCustomFieldCommandValidator : AbstractValidator<CreateProductCustomFieldCommand>
{
    public CreateProductCustomFieldCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9_]+$").WithMessage("Code may contain only lowercase letters, digits and underscores.");
        RuleFor(x => x.FieldType).IsInEnum();
    }
}

public class CreateProductCustomFieldCommandHandler : IRequestHandler<CreateProductCustomFieldCommand, ProductCustomFieldDto>
{
    private readonly IApplicationDbContext _db;

    public CreateProductCustomFieldCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductCustomFieldDto> Handle(CreateProductCustomFieldCommand request, CancellationToken cancellationToken)
    {
        if (await _db.ProductCustomFields.AnyAsync(f => f.Code == request.Code, cancellationToken))
            throw new ConflictException($"A custom field with code '{request.Code}' already exists.");

        var entity = new ProductCustomField
        {
            Name = request.Name.Trim(),
            Code = request.Code,
            FieldType = request.FieldType,
            IsRequired = request.IsRequired,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
        };

        _db.ProductCustomFields.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ProductCustomFieldDto.From(entity);
    }
}
