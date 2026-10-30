using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Gdpr;

/// <summary>
/// Builds the authenticated customer's personal-data export (GDPR right to portability, WO-23). Resolves the
/// customer from the current login, generates the JSON snapshot on demand, notifies the customer by email that
/// their export is ready, and returns the raw bytes for the controller to stream as a download.
/// </summary>
public record RequestDataExportCommand : IRequest<byte[]>;

public class RequestDataExportCommandHandler : IRequestHandler<RequestDataExportCommand, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IGdprService _gdpr;
    private readonly IEmailEnqueuer _email;

    public RequestDataExportCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService current,
        IGdprService gdpr,
        IEmailEnqueuer email)
    {
        _db = db;
        _current = current;
        _gdpr = gdpr;
        _email = email;
    }

    public async Task<byte[]> Handle(RequestDataExportCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is null)
            throw new UnauthorizedException();

        var customer = await _db.Customers
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == _current.UserId.Value, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var bytes = await _gdpr.ExportCustomerDataAsync(customer.Id, cancellationToken);

        // Notify the customer their export was generated. The file itself is streamed back on this request
        // (synchronous download) rather than attached — the email is only a confirmation.
        var recipient = customer.User?.Email ?? _current.Email;
        if (!string.IsNullOrWhiteSpace(recipient))
        {
            var fullName = $"{customer.FirstName} {customer.LastName}".Trim();
            await _email.EnqueueAsync(
                "account.data-export",
                recipient,
                string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                "Your personal data export is ready",
                "Hi,\n\nWe've generated the export of the personal data associated with your account, and it has "
                    + "been downloaded from your account. If you did not request this export, please contact our "
                    + "support team.\n\nThank you.",
                NotificationCategory.Transactional,
                isHtml: false,
                cancellationToken);
        }

        return bytes;
    }
}
