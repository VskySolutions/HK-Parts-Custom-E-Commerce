using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsBlog;

/// <summary>Transitions a blog post's publish lifecycle (Draft / Published / Archived). The first
/// transition to Published stamps <see cref="CMSBlogPost.PublishedOnUtc"/> when it is not already set.</summary>
public record SetCmsBlogPostStatusCommand(Guid Id, CmsContentStatus Status) : IRequest<CmsBlogPostDto>;

public class SetCmsBlogPostStatusCommandValidator : AbstractValidator<SetCmsBlogPostStatusCommand>
{
    public SetCmsBlogPostStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class SetCmsBlogPostStatusCommandHandler : IRequestHandler<SetCmsBlogPostStatusCommand, CmsBlogPostDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public SetCmsBlogPostStatusCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CmsBlogPostDto> Handle(SetCmsBlogPostStatusCommand request, CancellationToken cancellationToken)
    {
        var post = await _db.CMSBlogPosts
            .Include(p => p.FeaturedImageMedia)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CMSBlogPost), request.Id);

        post.Status = request.Status;

        if (request.Status == CmsContentStatus.Published && post.PublishedOnUtc is null)
            post.PublishedOnUtc = _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return CmsBlogPostDto.From(post);
    }
}
