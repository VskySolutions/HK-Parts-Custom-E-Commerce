using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Checkout;

namespace HKPartsEcommerce.Application.Features.Inquiries;

/// <summary>
/// Raises an admin alert for every submitted inquiry (REQ-INQ-001). An inquiry is a lead with a clock on
/// it — unlike an order, nothing downstream chases it — so it gets a visible nudge rather than only an
/// email that may be filtered. Best-effort: an alert failure must never fail the buyer's submission.
/// </summary>
public class InquiryAlertHandler : INotificationHandler<InquirySubmitted>
{
    private readonly IAdminAlertService _alerts;

    public InquiryAlertHandler(IAdminAlertService alerts) => _alerts = alerts;

    public async Task Handle(InquirySubmitted notification, CancellationToken cancellationToken)
    {
        try
        {
            await _alerts.RaiseAsync(
                "NewInquiry",
                $"New inquiry {notification.ReferenceNumber}",
                $"A quote request worth approximately {notification.EstimatedTotal:0.00} was submitted" +
                (string.IsNullOrWhiteSpace(notification.Email) ? "." : $" by {notification.Email}."),
                "Info",
                "Inquiries",
                cancellationToken);
        }
        catch
        {
            // The inquiry is already saved and the emails are queued; an alerting failure is not the
            // buyer's problem.
        }
    }
}
