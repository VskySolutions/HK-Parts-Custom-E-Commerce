using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Payments;

/// <summary>
/// Writes the per-operation history of a payment (<see cref="PaymentTransaction"/>) and copies a gateway's
/// response detail onto the <see cref="PaymentRecord"/>. Shared by checkout, capture, refund, cancel and
/// status sync so every gateway call leaves the same trail, success or failure (Authorize.Net AN-18/26/27).
/// </summary>
public static class PaymentLedger
{
    /// <summary>Adds a history row for one gateway operation. The caller saves.</summary>
    public static PaymentTransaction Record(
        IApplicationDbContext db, PaymentRecord payment, PaymentTransactionType type, decimal amount,
        PaymentResult result, DateTime nowUtc, Guid? byUserId = null)
    {
        var d = result.Details;
        var row = new PaymentTransaction
        {
            PaymentRecordId = payment.Id,
            // A refund the gateway ran as a void (unsettled, full amount) is recorded as what really happened.
            Type = d?.PerformedAs ?? type,
            Amount = amount,
            Success = result.Success,
            TransactionId = Clip(result.TransactionId ?? result.AuthorizationId, 200),
            RefTransactionId = Clip(d?.RefTransactionId, 200),
            ResponseCode = Clip(d?.ResponseCode, 10),
            ReasonCode = Clip(d?.ReasonCode, 20),
            Message = Clip(d?.ResponseText ?? result.ErrorMessage, 1000),
            CreatedById = byUserId,
            CreatedOnUtc = nowUtc,
        };
        db.PaymentTransactions.Add(row);
        return row;
    }

    /// <summary>
    /// Copies the sale/authorization response detail onto the payment (AN-18). Only non-null values are copied,
    /// so a later capture or status sync that returns less never blanks what the sale recorded.
    /// </summary>
    public static void ApplyDetails(PaymentRecord payment, PaymentResult result)
    {
        var d = result.Details;
        if (d is null) return;
        payment.AuthCode = Clip(d.AuthCode, 20) ?? payment.AuthCode;
        payment.AvsResultCode = Clip(d.AvsResultCode, 10) ?? payment.AvsResultCode;
        payment.CvvResultCode = Clip(d.CvvResultCode, 10) ?? payment.CvvResultCode;
        payment.NetworkTransactionId = Clip(d.NetworkTransactionId, 100) ?? payment.NetworkTransactionId;
        payment.CardType = Clip(d.CardType, 50) ?? payment.CardType;
        payment.CardLastFour = Clip(d.CardLastFour, 4) ?? payment.CardLastFour;
        payment.GatewayResponseCode = Clip(d.ResponseCode, 10) ?? payment.GatewayResponseCode;
        payment.GatewayReasonCode = Clip(d.ReasonCode, 20) ?? payment.GatewayReasonCode;
        payment.GatewayResponseText = Clip(d.ResponseText, 1000) ?? payment.GatewayResponseText;
        payment.CustomerProfileId = Clip(d.CustomerProfileId, 50) ?? payment.CustomerProfileId;
        payment.CustomerPaymentProfileId = Clip(d.CustomerPaymentProfileId, 50) ?? payment.CustomerPaymentProfileId;
    }

    private static string? Clip(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
}
