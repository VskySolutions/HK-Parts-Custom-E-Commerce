using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsProductCollections;

/// <summary>Creates a new (empty) admin-curated product collection. The slug, when supplied, is normalised
/// and must be unique among live collections; products are added afterwards via the item endpoints.</summary>
public record CreateCmsProductCollectionCommand(
    string Name,
    string? Description = null,
    string? Slug = null,
    bool IsEnabled = true) : IRequest<CmsProductCollectionDto>;

public class CreateCmsProductCollectionCommandValidator : AbstractValidator<CreateCmsProductCollectionCommand>
{
    public CreateCmsProductCollectionCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Slug).MaximumLength(220);
    }
}

public class CreateCmsProductCollectionCommandHandler : IRequestHandler<CreateCmsProductCollectionCommand, CmsProductCollectionDto>
{
    private readonly IApplicationDbContext _db;

    public CreateCmsProductCollectionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsProductCollectionDto> Handle(CreateCmsProductCollectionCommand request, CancellationToken cancellationToken)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : SlugGenerator.Generate(request.Slug);
        if (slug is not null && await _db.CMSProductCollections.AnyAsync(c => c.Slug == slug, cancellationToken))
            throw new ConflictException($"A product collection with slug '{slug}' already exists.");

        var entity = new CMSProductCollection
        {
            Name = request.Name,
            Description = request.Description,
            Slug = slug,
            IsEnabled = request.IsEnabled,
        };

        _db.CMSProductCollections.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        // A freshly created collection has no items yet, so no media include is needed for its DTO.
        return CmsProductCollectionDto.From(entity);
    }
}
