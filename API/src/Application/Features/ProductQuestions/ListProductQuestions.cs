using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductQuestions;

/// <summary>Admin moderation queue: a page of product questions, filterable by status, product and free-text search (WO-58).</summary>
public record ListProductQuestionsQuery(
    int Page = 1,
    int PageSize = 20,
    QuestionStatus? Status = null,
    Guid? ProductId = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<ProductQuestionDto>>;

public class ListProductQuestionsQueryHandler : IRequestHandler<ListProductQuestionsQuery, PaginatedList<ProductQuestionDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["askerName"] = "AskerName",
        ["status"] = "Status",
        ["createdOn"] = "CreatedOnUtc",
        ["answeredOn"] = "AnsweredOnUtc",
    };

    private readonly IApplicationDbContext _db;

    public ListProductQuestionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<ProductQuestionDto>> Handle(ListProductQuestionsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<ProductQuestion> query = _db.ProductQuestions.AsNoTracking().Include(q => q.Product);

        if (request.Status is QuestionStatus status)
            query = query.Where(q => q.Status == status);

        if (request.ProductId is Guid productId)
            query = query.Where(q => q.ProductId == productId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(q =>
                q.QuestionText.Contains(term) ||
                q.AskerName.Contains(term) ||
                (q.AskerEmail != null && q.AskerEmail.Contains(term)));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderByDescending(x => x.CreatedOnUtc));

        var page = await PaginatedList<ProductQuestion>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(ProductQuestionDto.From).ToList();
        return new PaginatedList<ProductQuestionDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
