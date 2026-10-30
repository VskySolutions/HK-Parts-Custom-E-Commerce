using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductQuestions;

/// <summary>
/// Public storefront read: the approved, answered questions for a product, newest first (WO-58).
/// Pending/rejected questions and answered-but-unapproved questions are excluded.
/// </summary>
public record GetProductQuestionsQuery(Guid ProductId) : IRequest<IReadOnlyList<ProductQuestionPublicDto>>;

public class GetProductQuestionsQueryHandler : IRequestHandler<GetProductQuestionsQuery, IReadOnlyList<ProductQuestionPublicDto>>
{
    private readonly IApplicationDbContext _db;

    public GetProductQuestionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProductQuestionPublicDto>> Handle(GetProductQuestionsQuery request, CancellationToken cancellationToken)
    {
        var items = await _db.ProductQuestions.AsNoTracking()
            .Where(q => q.ProductId == request.ProductId
                && q.Status == QuestionStatus.Approved
                && q.AnswerText != null
                && q.AnswerText != "")
            .OrderByDescending(q => q.CreatedOnUtc)
            .ThenByDescending(q => q.Id)
            .Select(q => new ProductQuestionPublicDto
            {
                AskerName = q.AskerName,
                QuestionText = q.QuestionText,
                AnswerText = q.AnswerText,
                AnsweredOnUtc = q.AnsweredOnUtc,
                CreatedOnUtc = q.CreatedOnUtc,
            })
            .ToListAsync(cancellationToken);

        return items;
    }
}
