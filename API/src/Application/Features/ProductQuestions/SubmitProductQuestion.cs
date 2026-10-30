using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductQuestions;

/// <summary>
/// Public storefront submission of a product question (WO-58). Created as
/// <see cref="QuestionStatus.Pending"/> awaiting admin moderation. Protected by the optional reCAPTCHA
/// <see cref="RecaptchaFormType.QaSubmit"/> hook (a no-op when reCAPTCHA is unconfigured). When a
/// signed-in customer submits, their profile is linked and their stored name / email are preferred.
/// </summary>
public record SubmitProductQuestionCommand(
    Guid ProductId,
    string AskerName,
    string? AskerEmail,
    string QuestionText,
    string? RecaptchaToken = null) : IRequest<Guid>;

public class SubmitProductQuestionCommandValidator : AbstractValidator<SubmitProductQuestionCommand>
{
    public SubmitProductQuestionCommandValidator()
    {
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.AskerName).NotEmpty().MaximumLength(200);
        // Email is mandatory for storefront submissions (used to notify the asker when it's answered).
        RuleFor(x => x.AskerEmail).NotEmpty().MaximumLength(256).EmailAddress();
    }
}

public class SubmitProductQuestionCommandHandler : IRequestHandler<SubmitProductQuestionCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IRecaptchaVerifier _recaptcha;
    private readonly ICurrentUserService _current;

    public SubmitProductQuestionCommandHandler(
        IApplicationDbContext db,
        IRecaptchaVerifier recaptcha,
        ICurrentUserService current)
    {
        _db = db;
        _recaptcha = recaptcha;
        _current = current;
    }

    public async Task<Guid> Handle(SubmitProductQuestionCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken))
            throw new NotFoundException(nameof(Product), request.ProductId);

        await _recaptcha.VerifyOrThrowAsync(RecaptchaFormType.QaSubmit, request.RecaptchaToken, cancellationToken);

        var askerName = request.AskerName.Trim();
        var askerEmail = string.IsNullOrWhiteSpace(request.AskerEmail) ? null : request.AskerEmail.Trim();
        Guid? customerId = null;

        // A signed-in shopper: link their profile and prefer their stored name / email over what was posted.
        if (_current.UserId is Guid userId)
        {
            var customer = await _db.Customers.AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => new { c.Id, c.FirstName, c.LastName })
                .FirstOrDefaultAsync(cancellationToken);

            if (customer is not null)
            {
                customerId = customer.Id;
                var fullName = $"{customer.FirstName} {customer.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(fullName))
                    askerName = fullName;
                if (!string.IsNullOrWhiteSpace(_current.Email))
                    askerEmail = _current.Email;
            }
        }

        var entity = new ProductQuestion
        {
            ProductId = request.ProductId,
            CustomerId = customerId,
            AskerName = askerName,
            AskerEmail = askerEmail,
            QuestionText = request.QuestionText.Trim(),
            Status = QuestionStatus.Pending,
        };

        _db.ProductQuestions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
