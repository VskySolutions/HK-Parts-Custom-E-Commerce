using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Payments;

/// <summary>Read model for a <see cref="PaymentRecord"/> across the authorize → capture → refund lifecycle.</summary>
public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public PaymentMethodType Method { get; set; }
    public string? GatewayName { get; set; }
    public PaymentStatus Status { get; set; }

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal RefundedAmount { get; set; }

    public string? AuthorizationId { get; set; }
    public string? TransactionId { get; set; }
    public string? GatewayReference { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime? AuthorizedOnUtc { get; set; }
    public DateTime? CapturedOnUtc { get; set; }
    public DateTime? RefundedOnUtc { get; set; }
    public DateTime? AuthorizationExpiresUtc { get; set; }

    public static PaymentDto From(PaymentRecord p) => new()
    {
        Id = p.Id,
        OrderId = p.OrderId,
        Method = p.Method,
        GatewayName = p.GatewayName,
        Status = p.Status,
        Amount = p.Amount,
        CurrencyCode = p.CurrencyCode,
        RefundedAmount = p.RefundedAmount,
        AuthorizationId = p.AuthorizationId,
        TransactionId = p.TransactionId,
        GatewayReference = p.GatewayReference,
        ErrorMessage = p.ErrorMessage,
        AuthorizedOnUtc = p.AuthorizedOnUtc,
        CapturedOnUtc = p.CapturedOnUtc,
        RefundedOnUtc = p.RefundedOnUtc,
        AuthorizationExpiresUtc = p.AuthorizationExpiresUtc,
    };
}
