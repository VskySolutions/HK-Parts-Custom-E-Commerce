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

    public decimal? AuthorizedAmount { get; set; }
    public string? AuthCode { get; set; }
    public string? AvsResultCode { get; set; }
    public string? CvvResultCode { get; set; }
    public string? NetworkTransactionId { get; set; }
    public string? CardType { get; set; }
    public string? CardLastFour { get; set; }
    public string? GatewayResponseCode { get; set; }
    public string? GatewayReasonCode { get; set; }
    public string? GatewayResponseText { get; set; }
    public string? CustomerProfileId { get; set; }
    public string? CustomerPaymentProfileId { get; set; }

    /// <summary>Gateway operations on this payment (sale, capture, void, refund), oldest first. Empty unless loaded.</summary>
    public List<PaymentTransactionDto> Transactions { get; set; } = new();

    public static PaymentDto From(PaymentRecord p) => new()
    {
        AuthorizedAmount = p.AuthorizedAmount,
        AuthCode = p.AuthCode,
        AvsResultCode = p.AvsResultCode,
        CvvResultCode = p.CvvResultCode,
        NetworkTransactionId = p.NetworkTransactionId,
        CardType = p.CardType,
        CardLastFour = p.CardLastFour,
        GatewayResponseCode = p.GatewayResponseCode,
        GatewayReasonCode = p.GatewayReasonCode,
        GatewayResponseText = p.GatewayResponseText,
        CustomerProfileId = p.CustomerProfileId,
        CustomerPaymentProfileId = p.CustomerPaymentProfileId,
        Transactions = p.Transactions.OrderBy(t => t.CreatedOnUtc).Select(PaymentTransactionDto.From).ToList(),
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

/// <summary>One gateway operation on a payment, for the order page's payment history (AN-42).</summary>
public class PaymentTransactionDto
{
    public Guid Id { get; set; }
    public PaymentTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string? RefTransactionId { get; set; }
    public string? ResponseCode { get; set; }
    public string? ReasonCode { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    public static PaymentTransactionDto From(PaymentTransaction t) => new()
    {
        Id = t.Id,
        Type = t.Type,
        Amount = t.Amount,
        Success = t.Success,
        TransactionId = t.TransactionId,
        RefTransactionId = t.RefTransactionId,
        ResponseCode = t.ResponseCode,
        ReasonCode = t.ReasonCode,
        Message = t.Message,
        CreatedOnUtc = t.CreatedOnUtc,
    };
}
