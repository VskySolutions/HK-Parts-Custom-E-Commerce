using HKPartsEcommerce.Application.Common.Utilities;
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
    int DisplayOrder = 0,
    bool IsEnabled = true,
    string? Summary = null,
    bool ShowInMenu = true,
    Guid? ImageMediaId = null,
    string? ImageAltText = null,
    Guid? Image2MediaId = null,
    string? Image2AltText = null) : IRequest<CategoryDto>;

public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(220);
        RuleFor(x => x.Summary).MaximumLength(2000);
        RuleFor(x => x.ImageAltText).MaximumLength(300);
        RuleFor(x => x.Image2AltText).MaximumLength(300);
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

        var slug = SlugGenerator.NormalizeOptional(request.Slug);
        if (slug is not null && await _db.Categories.AnyAsync(x => x.Slug == slug && x.Id != request.Id, cancellationToken))
            throw new ConflictException($"Another category already uses the slug '{slug}'.");

        if (request.ParentId.HasValue && request.ParentId.Value == request.Id)
            throw new ConflictException("A category cannot be its own parent.");

        await CategoryQueries.EnsureMediaExistsAsync(_db, cancellationToken,
            request.ImageMediaId, request.Image2MediaId);

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.ParentId = request.ParentId;
        entity.Slug = slug;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsEnabled = request.IsEnabled;
        entity.Summary = request.Summary;
        entity.ShowInMenu = request.ShowInMenu;
        entity.ImageMediaId = request.ImageMediaId;
        entity.ImageAltText = request.ImageAltText;
        entity.Image2MediaId = request.Image2MediaId;
        entity.Image2AltText = request.Image2AltText;

        await _db.SaveChangesAsync(cancellationToken);
        return await CategoryQueries.GetDtoAsync(_db, entity.Id, cancellationToken);
    }
}
