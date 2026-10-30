using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsPages;

/// <summary>Updates an existing CMS content page. <see cref="CMSPage.IsSystemPage"/> is never changed here.</summary>
public record UpdateCmsPageCommand(
    Guid Id,
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

public class UpdateCmsPageCommandValidator : AbstractValidator<UpdateCmsPageCommand>
{
    public UpdateCmsPageCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Slug).MaximumLength(300);
        RuleFor(x => x.MetaTitle).MaximumLength(300);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
        RuleFor(x => x.MetaKeywords).MaximumLength(500);
        RuleFor(x => x.CanonicalUrl).MaximumLength(500);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class UpdateCmsPageCommandHandler : IRequestHandler<UpdateCmsPageCommand, CmsPageDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateCmsPageCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsPageDto> Handle(UpdateCmsPageCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSPages
            .Include(p => p.PageGroup)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSPage), request.Id);

        var slug = SlugGenerator.Generate(string.IsNullOrWhiteSpace(request.Slug) ? request.Title : request.Slug);

        if (await _db.CMSPages.AnyAsync(p => p.Slug == slug && p.Id != request.Id, cancellationToken))
            throw new ConflictException($"A CMS page with slug '{slug}' already exists.");

        if (request.PageGroupId is Guid groupId && !await _db.CMSPageGroups.AnyAsync(g => g.Id == groupId, cancellationToken))
            throw new NotFoundException(nameof(CMSPageGroup), groupId);

        entity.Title = request.Title;
        entity.Slug = slug;
        entity.Body = request.Body;
        entity.MetaTitle = request.MetaTitle;
        entity.MetaDescription = request.MetaDescription;
        entity.MetaKeywords = request.MetaKeywords;
        entity.CanonicalUrl = request.CanonicalUrl;
        entity.Status = request.Status;
        entity.PageGroupId = request.PageGroupId;
        entity.DisplayOrder = request.DisplayOrder;
        entity.ShowInTopBar = request.ShowInTopBar;
        entity.ShowInFooter = request.ShowInFooter;

        await _db.SaveChangesAsync(cancellationToken);

        // Refresh the group navigation so the returned DTO reflects any group change.
        entity.PageGroup = request.PageGroupId is null
            ? null
            : await _db.CMSPageGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == request.PageGroupId, cancellationToken);

        return CmsPageDto.From(entity);
    }
}
