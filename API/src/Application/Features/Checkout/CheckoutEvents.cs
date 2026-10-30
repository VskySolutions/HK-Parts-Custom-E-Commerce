using MediatR;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>
/// Raised when a checkout completes with a successful payment (REQ-CHK-003 / AC-CHK-003.8). Downstream
/// handlers (order confirmation, analytics, fulfilment hand-off) subscribe to this rather than the
/// low-level routing/payment events.
/// </summary>
public record OrderPlaced(
    Guid OrderId,
    string OrderNumber,
    decimal Total,
    Guid? CustomerId,
    string? Email) : INotification;

/// <summary>
/// Raised when a storefront inquiry (quote request) is submitted (REQ-INQ-001). Deliberately separate from
/// <see cref="OrderPlaced"/>: an inquiry earns no loyalty points, moves no stock and is not revenue, so
/// handlers that act on a sale must not see it. Admin alerts, webhooks and lead routing subscribe here.
/// </summary>
public record InquirySubmitted(
    Guid InquiryId,
    string ReferenceNumber,
    decimal EstimatedTotal,
    Guid? CustomerId,
    string? Email,
    Guid? AssignedStoreId) : INotification;
