using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsBlog;

/// <summary>Public storefront lookup of a single Published blog post by slug. Draft/Archived and
/// soft-deleted posts are treated as not found.</summary>
public record GetPublishedBlogPostBySlugQuery(string Slug) : IRequest<CmsBlogPostDto>;

public class GetPublishedBlogPostBySlugQueryHandler : IRequestHandler<GetPublishedBlogPostBySlugQuery, CmsBlogPostDto>
{
    private readonly IApplicationDbContext _db;

    public GetPublishedBlogPostBySlugQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsBlogPostDto> Handle(GetPublishedBlogPostBySlugQuery request, CancellationToken cancellationToken)
    {
        var slug = (request.Slug ?? string.Empty).Trim();

        var post = await _db.CMSBlogPosts
            .AsNoTracking()
            .Include(p => p.FeaturedImageMedia)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Status == CmsContentStatus.Published, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSBlogPost), slug);

        return CmsBlogPostDto.From(post);
    }
}
