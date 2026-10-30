using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsBlog;

public record GetCmsBlogPostQuery(Guid Id) : IRequest<CmsBlogPostDto>;

public class GetCmsBlogPostQueryHandler : IRequestHandler<GetCmsBlogPostQuery, CmsBlogPostDto>
{
    private readonly IApplicationDbContext _db;

    public GetCmsBlogPostQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsBlogPostDto> Handle(GetCmsBlogPostQuery request, CancellationToken cancellationToken)
    {
        var post = await _db.CMSBlogPosts
            .AsNoTracking()
            .Include(p => p.FeaturedImageMedia)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSBlogPost), request.Id);

        return CmsBlogPostDto.From(post);
    }
}
