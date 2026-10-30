using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductReviews;

/// <summary>Admin bulk moderation (WO-14): approve or reject many reviews at once. Returns the number of
/// reviews actually updated (unknown ids are ignored).</summary>
public record BulkModerateProductReviewsCommand(IReadOnlyList<Guid> Ids, ReviewStatus Status) : IRequest<int>;

public class BulkModerateProductReviewsCommandValidator : AbstractValidator<BulkModerateProductReviewsCommand>
{
    public BulkModerateProductReviewsCommandValidator()
    {
        RuleFor(x => x.Ids).NotEmpty();
        RuleFor(x => x.Status)
            .Must(s => s is ReviewStatus.Approved or ReviewStatus.Rejected)
            .WithMessage("Status must be Approved or Rejected.");
    }
}

public class BulkModerateProductReviewsCommandHandler : IRequestHandler<BulkModerateProductReviewsCommand, int>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService _current;

    public BulkModerateProductReviewsCommandHandler(IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService current)
    {
        _db = db;
        _clock = clock;
        _current = current;
    }

    public async Task<int> Handle(BulkModerateProductReviewsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().ToList();

        var reviews = await _db.ProductReviews
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        var moderatorId = _current.UserId;

        foreach (var review in reviews)
        {
            review.Status = request.Status;
            review.ModeratedOnUtc = now;
            review.ModeratedById = moderatorId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return reviews.Count;
    }
}
