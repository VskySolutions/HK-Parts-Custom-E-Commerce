using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

public record ProductCustomFieldValueInput(Guid ProductCustomFieldId, string? Value);

/// <summary>
/// Replaces a product's custom-field values. Blank values are dropped; every active required field must
/// end up with a value.
/// </summary>
public record SetProductCustomFieldValuesCommand(Guid ProductId, List<ProductCustomFieldValueInput> Values) : IRequest<ProductDto>;

public class SetProductCustomFieldValuesCommandHandler : IRequestHandler<SetProductCustomFieldValuesCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public SetProductCustomFieldValuesCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(SetProductCustomFieldValuesCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        // Last value wins for a repeated field; blanks mean "no value".
        var desired = (request.Values ?? new List<ProductCustomFieldValueInput>())
            .GroupBy(v => v.ProductCustomFieldId)
            .ToDictionary(g => g.Key, g => g.Last().Value?.Trim());
        foreach (var key in desired.Where(kv => string.IsNullOrEmpty(kv.Value)).Select(kv => kv.Key).ToList())
            desired.Remove(key);

        var fields = await _db.ProductCustomFields.AsNoTracking().ToListAsync(cancellationToken);
        var unknown = desired.Keys.FirstOrDefault(id => fields.All(f => f.Id != id));
        if (unknown != Guid.Empty)
            throw new NotFoundException(nameof(ProductCustomField), unknown);

        var failures = fields
            .Where(f => f.IsActive && f.IsRequired && !desired.ContainsKey(f.Id))
            .Select(f => new ValidationFailure(f.Code, $"{f.Name} is required."))
            .ToList();
        if (failures.Count > 0)
            throw new ValidationException(failures);

        foreach (var row in product.CustomFieldValues.Where(v => !desired.ContainsKey(v.ProductCustomFieldId)).ToList())
            product.CustomFieldValues.Remove(row);

        foreach (var (fieldId, value) in desired)
        {
            var row = product.CustomFieldValues.FirstOrDefault(v => v.ProductCustomFieldId == fieldId);
            if (row is null)
                product.CustomFieldValues.Add(new ProductCustomFieldValue { ProductId = product.Id, ProductCustomFieldId = fieldId, Value = value });
            else
                row.Value = value;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }
}
