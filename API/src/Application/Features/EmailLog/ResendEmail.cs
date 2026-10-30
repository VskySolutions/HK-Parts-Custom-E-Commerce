using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.EmailLog;

/// <summary>
/// Re-queues an email for delivery. A NEW <see cref="EmailQueue"/> row is created (a copy of the
/// original's rendered content) in <see cref="EmailStatus.Pending"/>, so the original send stays in
/// the log as history and the resend is dispatched by the EmailDispatchWorker on its next run.
/// </summary>
public record ResendEmailCommand(Guid Id) : IRequest<EmailLogDetailDto>;

public class ResendEmailCommandValidator : AbstractValidator<ResendEmailCommand>
{
    public ResendEmailCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class ResendEmailCommandHandler : IRequestHandler<ResendEmailCommand, EmailLogDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ResendEmailCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<EmailLogDetailDto> Handle(ResendEmailCommand request, CancellationToken cancellationToken)
    {
        var source = await _db.EmailQueue
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EmailQueue), request.Id);

        var resend = new EmailQueue
        {
            TemplateKey = source.TemplateKey,
            RecipientEmail = source.RecipientEmail,
            RecipientName = source.RecipientName,
            RenderedSubject = source.RenderedSubject,
            RenderedBody = source.RenderedBody,
            Category = source.Category,
            Status = EmailStatus.Pending,
            CreatedOnUtc = _clock.UtcNow,
        };

        _db.EmailQueue.Add(resend);
        await _db.SaveChangesAsync(cancellationToken);

        return EmailLogDetailDto.From(resend);
    }
}
