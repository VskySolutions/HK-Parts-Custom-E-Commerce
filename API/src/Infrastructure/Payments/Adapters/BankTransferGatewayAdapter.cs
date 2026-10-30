using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Payments.Adapters;

/// <summary>
/// Bank Transfer adapter (WO-34, AC-PAY-001.5). Authorization leaves the order awaiting the incoming
/// transfer; an admin captures once funds are reconciled and refunds if reversed. No external gateway call.
/// </summary>
public class BankTransferGatewayAdapter : ManualPaymentAdapterBase
{
    public override PaymentMethodType Method => PaymentMethodType.BankTransfer;
    protected override string ReferencePrefix => "BANK";
}
