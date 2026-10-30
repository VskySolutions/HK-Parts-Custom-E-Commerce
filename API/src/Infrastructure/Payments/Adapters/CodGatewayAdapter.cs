using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Payments.Adapters;

/// <summary>
/// Cash-on-Delivery adapter (WO-34, AC-PAY-001.5). Authorization leaves the order awaiting payment;
/// the store captures on delivery and refunds if returned. No external gateway is contacted.
/// </summary>
public class CodGatewayAdapter : ManualPaymentAdapterBase
{
    public override PaymentMethodType Method => PaymentMethodType.CashOnDelivery;
    protected override string ReferencePrefix => "COD";
}
