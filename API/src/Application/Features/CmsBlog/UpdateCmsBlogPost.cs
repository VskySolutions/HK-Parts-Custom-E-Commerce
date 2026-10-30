using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsBlog;

/// <summary>Updates an existing blog post. When the post first becomes Published its publish date is
/// stamped (and preserved thereafter).</summary>
public record UpdateCmsBlogPostCommand(
    Guid Id,
    string Title,
    string? Slug = null,
    string? Summary = null,
    string? Body = null,
    string? Author = null,
    string? Tags = null,
    Guid? FeaturedImageMediaId = null,
    CmsContentStatus Status = CmsContentStatus.Draft,
    string? MetaTitle = null,
    string? MetaDescription = null) : IRequest<CmsBlogPostDto>;

public class UpdateCmsBlogPostCommandValidator : AbstractValidator<UpdateCmsBlogPostCommand>
{
    public UpdateCmsBlogPostCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Slug).MaximumLength(300);
        RuleFor(x => x.Summary).MaximumLength(1000);
        RuleFor(x => x.Author).MaximumLength(200);
        RuleFor(x => x.Tags).MaximumLength(1000);
        RuleFor(x => x.MetaTitle).MaximumLength(300);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class UpdateCmsBlogPostCommandHandler : IRequestHandler<UpdateCmsBlogPostCommand, CmsBlogPostDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UpdateCmsBlogPostCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CmsBlogPostDto> Handle(UpdateCmsBlogPostCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSBlogPosts
            .Include(p => p.FeaturedImageMedia)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSBlogPost), request.Id);

        var slug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(request.Slug) ? request.Title : request.Slug);

        if (await _db.CMSBlogPosts.AnyAsync(p => p.Slug == slug && p.Id != request.Id, cancellationToken))
            throw new ConflictException($"A blog post with slug '{slug}' already exists.");

        if (request.FeaturedImageMediaId is Guid mediaId && !await _db.Media.AnyAsync(m => m.Id == mediaId, cancellationToken))
            throw new NotFoundException(nameof(Media), mediaId);

        entity.Title = request.Title;
        entity.Slug = slug;
        entity.Summary = request.Summary;
        entity.Body = request.Body;
        entity.Author = request.Author;
        entity.Tags = request.Tags;
        entity.FeaturedImageMediaId = request.FeaturedImageMediaId;
        entity.Status = request.Status;
        entity.MetaTitle = request.MetaTitle;
        entity.MetaDescription = request.MetaDescription;

        // Stamp the publish date the first time the post goes live; keep it stable afterwards.
        if (request.Status == CmsContentStatus.Published && entity.PublishedOnUtc is null)
            entity.PublishedOnUtc = _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        entity.FeaturedImageMedia = request.FeaturedImageMediaId is null
            ? null
            : await _db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.FeaturedImageMediaId, cancellationToken);

        return CmsBlogPostDto.From(entity);
    }
}
