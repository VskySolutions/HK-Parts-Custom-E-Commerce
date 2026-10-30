using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsPages;

/// <summary>Creates a new CMS content page. The slug is auto-derived from the title when left blank and
/// must be unique among live pages.</summary>
public record CreateCmsPageCommand(
    string Title,
    string? Slug = null,
    string? Body = null,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? MetaKeywords = null,
    string? CanonicalUrl = null,
    CmsContentStatus Status = CmsContentStatus.Draft,
    Guid? PageGroupId = null,
    int DisplayOrder = 0,
    bool ShowInTopBar = false,
    bool ShowInFooter = true) : IRequest<CmsPageDto>;

public class CreateCmsPageCommandValidator : AbstractValidator<CreateCmsPageCommand>
{
    public CreateCmsPageCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Slug).MaximumLength(300);
        RuleFor(x => x.MetaTitle).MaximumLength(300);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
        RuleFor(x => x.MetaKeywords).MaximumLength(500);
        RuleFor(x => x.CanonicalUrl).MaximumLength(500);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class CreateCmsPageCommandHandler : IRequestHandler<CreateCmsPageCommand, CmsPageDto>
{
    private readonly IApplicationDbContext _db;

    public CreateCmsPageCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsPageDto> Handle(CreateCmsPageCommand request, CancellationToken cancellationToken)
    {
        var slug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(request.Slug) ? request.Title : request.Slug);

        if (await _db.CMSPages.AnyAsync(p => p.Slug == slug, cancellationToken))
            throw new ConflictException($"A CMS page with slug '{slug}' already exists.");

        if (request.PageGroupId is Guid groupId && !await _db.CMSPageGroups.AnyAsync(g => g.Id == groupId, cancellationToken))
            throw new NotFoundException(nameof(CMSPageGroup), groupId);

        var entity = new CMSPage
        {
            Title = request.Title,
            Slug = slug,
            Body = request.Body,
            MetaTitle = request.MetaTitle,
            MetaDescription = request.MetaDescription,
            MetaKeywords = request.MetaKeywords,
            CanonicalUrl = request.CanonicalUrl,
            Status = request.Status,
            PageGroupId = request.PageGroupId,
            DisplayOrder = request.DisplayOrder,
            ShowInTopBar = request.ShowInTopBar,
            ShowInFooter = request.ShowInFooter,
            IsSystemPage = false,
        };

        _db.CMSPages.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        // Reload the group name for a complete DTO without a second round-trip cost on the hot path.
        entity.PageGroup = request.PageGroupId is null
            ? null
            : await _db.CMSPageGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == request.PageGroupId, cancellationToken);

        return CmsPageDto.From(entity);
    }
}
