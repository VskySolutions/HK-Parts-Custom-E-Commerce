using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductReviews;

/// <summary>Admin moderation of a single review (WO-14): approve or reject it, stamping who moderated and when.</summary>
public record ModerateProductReviewCommand(Guid Id, ReviewStatus Status) : IRequest<ProductReviewDto>;

public class ModerateProductReviewCommandValidator : AbstractValidator<ModerateProductReviewCommand>
{
    public ModerateProductReviewCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status)
            .Must(s => s is ReviewStatus.Approved or ReviewStatus.Rejected)
            .WithMessage("Status must be Approved or Rejected.");
    }
}

public class ModerateProductReviewCommandHandler : IRequestHandler<ModerateProductReviewCommand, ProductReviewDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService _current;

    public ModerateProductReviewCommandHandler(IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService current)
    {
        _db = db;
        _clock = clock;
        _current = current;
    }

    public async Task<ProductReviewDto> Handle(ModerateProductReviewCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductReviews
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductReview), request.Id);

        entity.Status = request.Status;
        entity.ModeratedOnUtc = _clock.UtcNow;
        entity.ModeratedById = _current.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return ProductReviewDto.From(entity);
    }
}
