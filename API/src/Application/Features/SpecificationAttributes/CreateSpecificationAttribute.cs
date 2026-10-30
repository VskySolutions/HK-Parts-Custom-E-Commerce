using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.SpecificationAttributes;

/// <summary>An option to create, or (when <see cref="Id"/> is supplied) update during reconciliation.</summary>
public record SpecificationAttributeOptionInput(Guid? Id, string Value, int DisplayOrder);

/// <summary>Creates a specification attribute together with its options (AC-CAT-003.2).</summary>
public record CreateSpecificationAttributeCommand(
    string Name,
    bool IsFilterable = true,
    int DisplayOrder = 0,
    List<SpecificationAttributeOptionInput>? Options = null) : IRequest<SpecificationAttributeDto>;

public class CreateSpecificationAttributeCommandValidator : AbstractValidator<CreateSpecificationAttributeCommand>
{
    public CreateSpecificationAttributeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleForEach(x => x.Options).ChildRules(o =>
        {
            o.RuleFor(i => i.Value).NotEmpty().MaximumLength(400);
        });
    }
}

public class CreateSpecificationAttributeCommandHandler : IRequestHandler<CreateSpecificationAttributeCommand, SpecificationAttributeDto>
{
    private readonly IApplicationDbContext _db;

    public CreateSpecificationAttributeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<SpecificationAttributeDto> Handle(CreateSpecificationAttributeCommand request, CancellationToken cancellationToken)
    {
        var entity = new SpecificationAttribute
        {
            Name = request.Name,
            IsFilterable = request.IsFilterable,
            DisplayOrder = request.DisplayOrder,
        };

        foreach (var option in request.Options ?? new())
        {
            entity.Options.Add(new SpecificationAttributeOption
            {
                Value = option.Value,
                DisplayOrder = option.DisplayOrder,
            });
        }

        _db.SpecificationAttributes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return SpecificationAttributeDto.From(entity);
    }
}
