using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductQuestions;

/// <summary>
/// Admin: author a FAQ (question + answer) for a product (WO-58 follow-up). The admin chooses whether to
/// publish it now or keep it as a draft:
/// <list type="bullet">
/// <item><description><see cref="Publish"/> = true → saved as <see cref="QuestionStatus.Approved"/>, so it
/// appears on the storefront immediately.</description></item>
/// <item><description><see cref="Publish"/> = false → saved as <see cref="QuestionStatus.Pending"/> (a draft):
/// answered but hidden from the storefront until an admin approves it.</description></item>
/// </list>
/// Either way the answer is recorded up-front, unlike a customer submission
/// (<see cref="SubmitProductQuestionCommand"/>), which lands Pending and unanswered.
/// </summary>
public record CreateProductFaqCommand(
    Guid ProductId,
    string QuestionText,
    string AnswerText,
    string? AskerName = null,
    bool Publish = false) : IRequest<ProductQuestionDto>;

public class CreateProductFaqCommandValidator : AbstractValidator<CreateProductFaqCommand>
{
    public CreateProductFaqCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.AnswerText).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.AskerName).MaximumLength(200);
    }
}

public class CreateProductFaqCommandHandler : IRequestHandler<CreateProductFaqCommand, ProductQuestionDto>
{
    // Fallback display name for the "asker" when the admin doesn't supply one — an admin-curated FAQ has
    // no real customer behind it.
    private const string DefaultAskerName = "Store";

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService _current;

    public CreateProductFaqCommandHandler(IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService current)
    {
        _db = db;
        _clock = clock;
        _current = current;
    }

    public async Task<ProductQuestionDto> Handle(CreateProductFaqCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.AsNoTracking()
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { p.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var askerName = string.IsNullOrWhiteSpace(request.AskerName) ? DefaultAskerName : request.AskerName.Trim();

        var entity = new ProductQuestion
        {
            ProductId = request.ProductId,
            CustomerId = null,
            AskerName = askerName,
            AskerEmail = null,
            QuestionText = request.QuestionText.Trim(),
            AnswerText = request.AnswerText.Trim(),
            // Publish now → Approved (live on the storefront); otherwise a Pending draft. Answered either way.
            Status = request.Publish ? QuestionStatus.Approved : QuestionStatus.Pending,
            AnsweredOnUtc = _clock.UtcNow,
            AnsweredById = _current.UserId,
        };

        _db.ProductQuestions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = ProductQuestionDto.From(entity);
        dto.ProductName = product.Name; // Product nav isn't loaded on the freshly-added entity.
        return dto;
    }
}
