using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Categories;

/// <summary>Updates an existing category, including its parent, SEO metadata, ordering and enabled state.</summary>
public record UpdateCategoryCommand(
    Guid Id,
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

public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(220);
        RuleFor(x => x.ParentId).NotEqual(x => x.Id).When(x => x.ParentId.HasValue);
    }
}

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CategoryDto> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), request.Id);

        if (request.ParentId.HasValue && request.ParentId.Value == request.Id)
            throw new ConflictException("A category cannot be its own parent.");

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.ParentId = request.ParentId;
        entity.Slug = request.Slug;
        entity.MetaTitle = request.MetaTitle;
        entity.MetaDescription = request.MetaDescription;
        entity.MetaKeywords = request.MetaKeywords;
        entity.CanonicalUrl = request.CanonicalUrl;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsEnabled = request.IsEnabled;

        await _db.SaveChangesAsync(cancellationToken);
        return CategoryDto.From(entity);
    }
}
