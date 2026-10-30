using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.SpecificationAttributes;

/// <summary>Updates a specification attribute and reconciles its options: adds new, updates existing by id, removes the rest (AC-CAT-003.2).</summary>
public record UpdateSpecificationAttributeCommand(
    Guid Id,
    string Name,
    bool IsFilterable = true,
    int DisplayOrder = 0,
    List<SpecificationAttributeOptionInput>? Options = null) : IRequest<SpecificationAttributeDto>;

public class UpdateSpecificationAttributeCommandValidator : AbstractValidator<UpdateSpecificationAttributeCommand>
{
    public UpdateSpecificationAttributeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleForEach(x => x.Options).ChildRules(o =>
        {
            o.RuleFor(i => i.Value).NotEmpty().MaximumLength(400);
        });
    }
}

public class UpdateSpecificationAttributeCommandHandler : IRequestHandler<UpdateSpecificationAttributeCommand, SpecificationAttributeDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateSpecificationAttributeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<SpecificationAttributeDto> Handle(UpdateSpecificationAttributeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.SpecificationAttributes
            .Include(a => a.Options)
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SpecificationAttribute), request.Id);

        entity.Name = request.Name;
        entity.IsFilterable = request.IsFilterable;
        entity.DisplayOrder = request.DisplayOrder;

        var inputs = request.Options ?? new();
        var keptIds = inputs
            .Where(i => i.Id.HasValue && i.Id.Value != Guid.Empty)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        foreach (var existing in entity.Options.Where(o => !keptIds.Contains(o.Id)).ToList())
        {
            entity.Options.Remove(existing);
            _db.SpecificationAttributeOptions.Remove(existing);
        }

        foreach (var input in inputs)
        {
            var existing = input.Id is { } id && id != Guid.Empty
                ? entity.Options.FirstOrDefault(o => o.Id == id)
                : null;

            if (existing is null)
            {
                entity.Options.Add(new SpecificationAttributeOption
                {
                    Value = input.Value,
                    DisplayOrder = input.DisplayOrder,
                });
            }
            else
            {
                existing.Value = input.Value;
                existing.DisplayOrder = input.DisplayOrder;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return SpecificationAttributeDto.From(entity);
    }
}
