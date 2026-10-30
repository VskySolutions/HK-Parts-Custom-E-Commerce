using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.EmailTemplates;

/// <summary>
/// Renders a template with sample data and enqueues a one-off test email to a chosen recipient, so an
/// admin can verify layout and deliverability before the template goes live (WO-79, REQ-ENT-004).
/// Enqueue-only: it inserts a single queued email and creates no order/account/business state
/// (AC-ENT-004.5).
/// </summary>
public record TestSendEmailTemplateCommand(string Key, string RecipientEmail)
    : IRequest<EmailTemplateTestSendResult>;

/// <summary>
/// Outcome of a template test-send: whether it was queued for dispatch, a human-readable reason
/// (confirmation or failure — AC-ENT-004.3 / AC-ENT-004.4), and the target recipient.
/// </summary>
public record EmailTemplateTestSendResult(bool Dispatched, string Message, string RecipientEmail);

public class TestSendEmailTemplateCommandValidator : AbstractValidator<TestSendEmailTemplateCommand>
{
    public TestSendEmailTemplateCommandValidator()
    {
        RuleFor(x => x.RecipientEmail).NotEmpty().EmailAddress();
    }
}

public class TestSendEmailTemplateCommandHandler
    : IRequestHandler<TestSendEmailTemplateCommand, EmailTemplateTestSendResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailEnqueuer _emails;

    public TestSendEmailTemplateCommandHandler(IApplicationDbContext db, IEmailEnqueuer emails)
    {
        _db = db;
        _emails = emails;
    }

    public async Task<EmailTemplateTestSendResult> Handle(
        TestSendEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateKey == request.Key, cancellationToken)
            ?? throw new NotFoundException("EmailTemplate", request.Key);

        var recipient = request.RecipientEmail.Trim();

        // Route via the SMTP account assigned to the template's category (AC-ENT-004.2). Report a clear
        // reason instead of throwing when none is configured (AC-ENT-004.4).
        var account = await EmailTemplateSmtpResolver.ResolveForCategoryAsync(_db, template.Category, cancellationToken);
        if (account is null)
            return new EmailTemplateTestSendResult(
                false,
                $"No enabled SMTP account is configured for the '{template.Category}' notification category.",
                recipient);

        // Render subject + HTML body with sample data (same substitution as preview — AC-ENT-003.2).
        var values = TemplatePreviewData.ForTemplate(template.TemplateKey, template.Category);
        var subject = PreviewEmailTemplateQueryHandler.Render(template.SubjectLine, values);
        var body = PreviewEmailTemplateQueryHandler.Render(template.HtmlBody, values);

        // Hand off to the async dispatch pipeline. This inserts only one EmailQueue row (carrying the
        // category, which the dispatch worker uses to select the SMTP account) — no order, account, or
        // other business side effects occur (AC-ENT-004.5).
        await _emails.EnqueueAsync(
            template.TemplateKey,
            recipient,
            recipientName: null,
            subject,
            body,
            template.Category,
            isHtml: true,
            cancellationToken: cancellationToken);

        return new EmailTemplateTestSendResult(
            true,
            $"Test email queued for dispatch to {recipient} via SMTP account '{account.DisplayName}'.",
            recipient);
    }
}
