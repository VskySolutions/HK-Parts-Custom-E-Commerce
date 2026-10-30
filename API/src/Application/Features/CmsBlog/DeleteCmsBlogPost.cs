using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsBlog;

/// <summary>Soft-deletes a blog post (idempotent).</summary>
public record DeleteCmsBlogPostCommand(Guid Id) : IRequest;

public class DeleteCmsBlogPostCommandHandler : IRequestHandler<DeleteCmsBlogPostCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteCmsBlogPostCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCmsBlogPostCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSBlogPosts
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.CMSBlogPosts.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
