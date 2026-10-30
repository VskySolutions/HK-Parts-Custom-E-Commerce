using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A customer- or guest-submitted product question together with its optional admin answer (WO-58).
/// A single record holds both sides of the exchange. Submitted as <see cref="QuestionStatus.Pending"/>;
/// an admin answers and (separately) approves or rejects it. Only questions that are both
/// <see cref="QuestionStatus.Approved"/> and answered are shown publicly on the product page.
/// </summary>
public class ProductQuestion : AuditableEntity, ISoftDeletable
{
    public Guid ProductId { get; set; }

    /// <summary>Linked customer profile when a signed-in shopper asked; null for guest submissions.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Display name of the asker (the customer's name for signed-in shoppers).</summary>
    public string AskerName { get; set; } = string.Empty;

    /// <summary>Optional contact email of the asker.</summary>
    public string? AskerEmail { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    public QuestionStatus Status { get; set; } = QuestionStatus.Pending;

    /// <summary>Admin answer text; null until answered. Independent of <see cref="Status"/>.</summary>
    public string? AnswerText { get; set; }

    public DateTime? AnsweredOnUtc { get; set; }

    /// <summary>Id of the admin user who answered; null until answered.</summary>
    public Guid? AnsweredById { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    /// <summary>Optional navigation to the product the question is about.</summary>
    public Product? Product { get; set; }
}
