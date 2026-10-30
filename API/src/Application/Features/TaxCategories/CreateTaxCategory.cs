using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.TaxCategories;

/// <summary>Creates a tax category so products have something to reference (AC-CAT-001.6).</summary>
public record CreateTaxCategoryCommand(
    string Name,
    string? Description = null,
    decimal? DefaultRatePercent = null,
    int DisplayOrder = 0,
    bool IsActive = true) : IRequest<TaxCategoryDto>;

public class CreateTaxCategoryCommandValidator : AbstractValidator<CreateTaxCategoryCommand>
{
    public CreateTaxCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DefaultRatePercent).InclusiveBetween(0, 100).When(x => x.DefaultRatePercent.HasValue);
    }
}

public class CreateTaxCategoryCommandHandler : IRequestHandler<CreateTaxCategoryCommand, TaxCategoryDto>
{
    private readonly IApplicationDbContext _db;

    public CreateTaxCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<TaxCategoryDto> Handle(CreateTaxCategoryCommand request, CancellationToken cancellationToken)
    {
        var entity = new TaxCategory
        {
            Name = request.Name,
            Description = request.Description,
            DefaultRatePercent = request.DefaultRatePercent,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };

        _db.TaxCategories.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return TaxCategoryDto.From(entity);
    }
}
