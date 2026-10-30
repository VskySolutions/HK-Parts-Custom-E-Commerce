using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductCustomFields;

/// <summary>Updates a product custom field (the route id wins over any id in the body).</summary>
public record UpdateProductCustomFieldCommand(
    Guid Id,
    string Name,
    string Code,
    ProductCustomFieldType FieldType = ProductCustomFieldType.Text,
    bool IsRequired = false,
    bool IsActive = true,
    int DisplayOrder = 0) : IRequest<ProductCustomFieldDto>;

public class UpdateProductCustomFieldCommandValidator : AbstractValidator<UpdateProductCustomFieldCommand>
{
    public UpdateProductCustomFieldCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9_]+$").WithMessage("Code may contain only lowercase letters, digits and underscores.");
        RuleFor(x => x.FieldType).IsInEnum();
    }
}

public class UpdateProductCustomFieldCommandHandler : IRequestHandler<UpdateProductCustomFieldCommand, ProductCustomFieldDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateProductCustomFieldCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductCustomFieldDto> Handle(UpdateProductCustomFieldCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductCustomFields
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductCustomField), request.Id);

        if (await _db.ProductCustomFields.AnyAsync(f => f.Code == request.Code && f.Id != request.Id, cancellationToken))
            throw new ConflictException($"A custom field with code '{request.Code}' already exists.");

        entity.Name = request.Name.Trim();
        entity.Code = request.Code;
        entity.FieldType = request.FieldType;
        entity.IsRequired = request.IsRequired;
        entity.IsActive = request.IsActive;
        entity.DisplayOrder = request.DisplayOrder;

        await _db.SaveChangesAsync(cancellationToken);
        return ProductCustomFieldDto.From(entity);
    }
}
