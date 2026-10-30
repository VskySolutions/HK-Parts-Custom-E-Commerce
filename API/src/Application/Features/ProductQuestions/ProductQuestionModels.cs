using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductQuestions;

/// <summary>Full admin view of a product question and its answer (WO-58).</summary>
public class ProductQuestionDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>Product name for display in the admin moderation queue (null if the product row is gone).</summary>
    public string? ProductName { get; set; }

    public Guid? CustomerId { get; set; }
    public string AskerName { get; set; } = string.Empty;
    public string? AskerEmail { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public QuestionStatus Status { get; set; }
    public string? AnswerText { get; set; }
    public DateTime? AnsweredOnUtc { get; set; }
    public Guid? AnsweredById { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static ProductQuestionDto From(ProductQuestion q) => new()
    {
        Id = q.Id,
        ProductId = q.ProductId,
        ProductName = q.Product?.Name,
        CustomerId = q.CustomerId,
        AskerName = q.AskerName,
        AskerEmail = q.AskerEmail,
        QuestionText = q.QuestionText,
        Status = q.Status,
        AnswerText = q.AnswerText,
        AnsweredOnUtc = q.AnsweredOnUtc,
        AnsweredById = q.AnsweredById,
        CreatedOnUtc = q.CreatedOnUtc,
        UpdatedOnUtc = q.UpdatedOnUtc,
    };
}

/// <summary>Public storefront view of an approved, answered product question (WO-58).</summary>
public class ProductQuestionPublicDto
{
    public string AskerName { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string? AnswerText { get; set; }
    public DateTime? AnsweredOnUtc { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    public static ProductQuestionPublicDto From(ProductQuestion q) => new()
    {
        AskerName = q.AskerName,
        QuestionText = q.QuestionText,
        AnswerText = q.AnswerText,
        AnsweredOnUtc = q.AnsweredOnUtc,
        CreatedOnUtc = q.CreatedOnUtc,
    };
}
