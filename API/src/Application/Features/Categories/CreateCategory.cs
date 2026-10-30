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
    bool IsEnabled = true,
    string? Summary = null,
    bool ShowInMenu = true,
    Guid? ImageMediaId = null,
    string? ImageAltText = null,
    Guid? Image2MediaId = null,
    string? Image2AltText = null,
    string? H1 = null,
    Guid? MetaImageMediaId = null,
    string? MetaOpenGraph = null,
    string? MetaCustomTags = null,
    string? MetaFooterTags = null) : IRequest<CategoryDto>;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(220);
        RuleFor(x => x.Summary).MaximumLength(2000);
        RuleFor(x => x.ImageAltText).MaximumLength(300);
        RuleFor(x => x.Image2AltText).MaximumLength(300);
        RuleFor(x => x.H1).MaximumLength(300);
    }
}

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly IApplicationDbContext _db;

    public CreateCategoryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        await CategoryQueries.EnsureMediaExistsAsync(_db, cancellationToken,
            request.ImageMediaId, request.Image2MediaId, request.MetaImageMediaId);

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
            Summary = request.Summary,
            ShowInMenu = request.ShowInMenu,
            ImageMediaId = request.ImageMediaId,
            ImageAltText = request.ImageAltText,
            Image2MediaId = request.Image2MediaId,
            Image2AltText = request.Image2AltText,
            H1 = request.H1,
            MetaImageMediaId = request.MetaImageMediaId,
            MetaOpenGraph = request.MetaOpenGraph,
            MetaCustomTags = request.MetaCustomTags,
            MetaFooterTags = request.MetaFooterTags,
        };

        _db.Categories.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return await CategoryQueries.GetDtoAsync(_db, entity.Id, cancellationToken);
    }
}
