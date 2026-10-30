using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsBlog;

/// <summary>Creates a new blog post. The slug is auto-derived from the title when left blank and must be
/// unique among live posts. Creating a post directly as Published stamps its publish date.</summary>
public record CreateCmsBlogPostCommand(
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

public class CreateCmsBlogPostCommandValidator : AbstractValidator<CreateCmsBlogPostCommand>
{
    public CreateCmsBlogPostCommandValidator()
    {
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

public class CreateCmsBlogPostCommandHandler : IRequestHandler<CreateCmsBlogPostCommand, CmsBlogPostDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateCmsBlogPostCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CmsBlogPostDto> Handle(CreateCmsBlogPostCommand request, CancellationToken cancellationToken)
    {
        var slug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(request.Slug) ? request.Title : request.Slug);

        if (await _db.CMSBlogPosts.AnyAsync(p => p.Slug == slug, cancellationToken))
            throw new ConflictException($"A blog post with slug '{slug}' already exists.");

        if (request.FeaturedImageMediaId is Guid mediaId && !await _db.Media.AnyAsync(m => m.Id == mediaId, cancellationToken))
            throw new NotFoundException(nameof(Media), mediaId);

        var entity = new CMSBlogPost
        {
            Title = request.Title,
            Slug = slug,
            Summary = request.Summary,
            Body = request.Body,
            Author = request.Author,
            Tags = request.Tags,
            FeaturedImageMediaId = request.FeaturedImageMediaId,
            Status = request.Status,
            MetaTitle = request.MetaTitle,
            MetaDescription = request.MetaDescription,
            PublishedOnUtc = request.Status == CmsContentStatus.Published ? _clock.UtcNow : null,
        };

        _db.CMSBlogPosts.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        entity.FeaturedImageMedia = request.FeaturedImageMediaId is null
            ? null
            : await _db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.FeaturedImageMediaId, cancellationToken);

        return CmsBlogPostDto.From(entity);
    }
}
