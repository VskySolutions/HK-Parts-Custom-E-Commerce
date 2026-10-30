using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ProductQuestions;

/// <summary>
/// Admin: record (or update) the answer to a product question (WO-58). Sets the answer text, the
/// answered timestamp and the answering admin. Moderation status is left untouched — answering and
/// approving are independent steps (an admin approves separately via <c>ModerateProductQuestion</c>).
/// </summary>
public record AnswerProductQuestionCommand(Guid Id, string AnswerText) : IRequest<ProductQuestionDto>;

public class AnswerProductQuestionCommandValidator : AbstractValidator<AnswerProductQuestionCommand>
{
    public AnswerProductQuestionCommandValidator()
    {
        RuleFor(x => x.AnswerText).NotEmpty().MaximumLength(4000);
    }
}

public class AnswerProductQuestionCommandHandler : IRequestHandler<AnswerProductQuestionCommand, ProductQuestionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService _current;

    public AnswerProductQuestionCommandHandler(IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService current)
    {
        _db = db;
        _clock = clock;
        _current = current;
    }

    public async Task<ProductQuestionDto> Handle(AnswerProductQuestionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductQuestions
            .Include(q => q.Product)
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductQuestion), request.Id);

        entity.AnswerText = request.AnswerText.Trim();
        entity.AnsweredOnUtc = _clock.UtcNow;
        entity.AnsweredById = _current.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return ProductQuestionDto.From(entity);
    }
}
