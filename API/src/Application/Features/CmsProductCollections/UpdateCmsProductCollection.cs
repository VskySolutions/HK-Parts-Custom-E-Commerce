using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsProductCollections;

/// <summary>Updates a product collection's metadata (name, description, slug, enabled state). Its item set is
/// managed separately through the item endpoints.</summary>
public record UpdateCmsProductCollectionCommand(
    Guid Id,
    string Name,
    string? Description = null,
    string? Slug = null,
    bool IsEnabled = true) : IRequest<CmsProductCollectionDto>;

public class UpdateCmsProductCollectionCommandValidator : AbstractValidator<UpdateCmsProductCollectionCommand>
{
    public UpdateCmsProductCollectionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Slug).MaximumLength(220);
    }
}

public class UpdateCmsProductCollectionCommandHandler : IRequestHandler<UpdateCmsProductCollectionCommand, CmsProductCollectionDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateCmsProductCollectionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsProductCollectionDto> Handle(UpdateCmsProductCollectionCommand request, CancellationToken cancellationToken)
    {
        // Load the aggregate (with its items + media) so the returned DTO is complete in one round-trip.
        var entity = await _db.CMSProductCollections
            .WithItemsAndMedia()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSProductCollection), request.Id);

        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : SlugGenerator.Generate(request.Slug);
        if (slug is not null && await _db.CMSProductCollections.AnyAsync(c => c.Slug == slug && c.Id != request.Id, cancellationToken))
            throw new ConflictException($"A product collection with slug '{slug}' already exists.");

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Slug = slug;
        entity.IsEnabled = request.IsEnabled;

        await _db.SaveChangesAsync(cancellationToken);
        return CmsProductCollectionDto.From(entity);
    }
}
