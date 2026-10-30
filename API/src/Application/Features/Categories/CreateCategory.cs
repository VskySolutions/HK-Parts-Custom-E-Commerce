using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Categories;

/// <summary>Creates a new catalog category node.</summary>
public record CreateCategoryCommand(
    string Name,
    string? Description = null,
    Guid? ParentId = null,
    string? Slug = null,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? MetaKeywords = null,
    string? CanonicalUrl = null,
    int DisplayOrder = 0,
    bool IsEnabled = true) : IRequest<CategoryDto>;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(220);
    }
}

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly IApplicationDbContext _db;

    public CreateCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var entity = new Category
        {
            Name = request.Name,
            Description = request.Description,
            ParentId = request.ParentId,
            Slug = request.Slug,
            MetaTitle = request.MetaTitle,
            MetaDescription = request.MetaDescription,
            MetaKeywords = request.MetaKeywords,
            CanonicalUrl = request.CanonicalUrl,
            DisplayOrder = request.DisplayOrder,
            IsEnabled = request.IsEnabled,
        };

        _db.Categories.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return CategoryDto.From(entity);
    }
}
