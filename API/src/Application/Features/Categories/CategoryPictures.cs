using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Categories;

/// <summary>A category picture: a Media asset assigned to a category, with its resolved public URL (mirrors WO-123 product pictures).</summary>
public class CategoryPictureDto
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }
}

// ---- List --------------------------------------------------------------------

/// <summary>Lists a category's Media-backed pictures (ordered), each with its resolved public URL.</summary>
public record ListCategoryPicturesQuery(Guid CategoryId) : IRequest<List<CategoryPictureDto>>;

public class ListCategoryPicturesQueryHandler : IRequestHandler<ListCategoryPicturesQuery, List<CategoryPictureDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public ListCategoryPicturesQueryHandler(IApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<List<CategoryPictureDto>> Handle(ListCategoryPicturesQuery request, CancellationToken cancellationToken)
    {
        var rows = await _db.CategoryPictures.AsNoTracking()
            .Where(p => p.CategoryId == request.CategoryId)
            .Include(p => p.Media)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);

        var result = new List<CategoryPictureDto>(rows.Count);
        foreach (var p in rows)
        {
            var url = p.Media is null ? string.Empty : await _storage.GetFileUrlAsync(p.Media.AssetKey, cancellationToken);
            result.Add(new CategoryPictureDto
            {
                Id = p.Id,
                MediaId = p.MediaId,
                Url = url,
                AltText = p.Media?.AltText,
                DisplayOrder = p.DisplayOrder,
            });
        }
        return result;
    }
}

// ---- Assign ------------------------------------------------------------------

/// <summary>Assigns a committed Media asset to a category as a picture; returns the created row.</summary>
public record AssignCategoryPictureCommand(Guid CategoryId, Guid MediaId, int DisplayOrder = 0) : IRequest<CategoryPictureDto>;

public class AssignCategoryPictureCommandValidator : AbstractValidator<AssignCategoryPictureCommand>
{
    public AssignCategoryPictureCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.MediaId).NotEmpty();
    }
}

public class AssignCategoryPictureCommandHandler : IRequestHandler<AssignCategoryPictureCommand, CategoryPictureDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public AssignCategoryPictureCommandHandler(IApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<CategoryPictureDto> Handle(AssignCategoryPictureCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
            throw new NotFoundException(nameof(Category), request.CategoryId);

        var media = await _db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Media), request.MediaId);

        // Append after the current last picture when no explicit order is supplied.
        var order = request.DisplayOrder;
        if (order == 0)
        {
            var max = await _db.CategoryPictures.Where(p => p.CategoryId == request.CategoryId)
                .Select(p => (int?)p.DisplayOrder).MaxAsync(cancellationToken) ?? -1;
            order = max + 1;
        }

        var picture = new CategoryPicture { CategoryId = request.CategoryId, MediaId = request.MediaId, DisplayOrder = order };
        _db.CategoryPictures.Add(picture);
        await _db.SaveChangesAsync(cancellationToken);

        return new CategoryPictureDto
        {
            Id = picture.Id,
            MediaId = media.Id,
            Url = await _storage.GetFileUrlAsync(media.AssetKey, cancellationToken),
            AltText = media.AltText,
            DisplayOrder = picture.DisplayOrder,
        };
    }
}

// ---- Remove ------------------------------------------------------------------

/// <summary>Removes a category picture (the underlying Media asset is left intact).</summary>
public record RemoveCategoryPictureCommand(Guid PictureId) : IRequest;

public class RemoveCategoryPictureCommandHandler : IRequestHandler<RemoveCategoryPictureCommand>
{
    private readonly IApplicationDbContext _db;

    public RemoveCategoryPictureCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(RemoveCategoryPictureCommand request, CancellationToken cancellationToken)
    {
        var picture = await _db.CategoryPictures.FirstOrDefaultAsync(p => p.Id == request.PictureId, cancellationToken);
        if (picture is null)
            return;

        _db.CategoryPictures.Remove(picture);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
