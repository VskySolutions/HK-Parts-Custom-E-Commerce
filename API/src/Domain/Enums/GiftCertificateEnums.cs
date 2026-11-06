namespace HKPartsEcommerce.Domain.Enums;

public enum GiftCertificateStatus
{
    Inactive = 0,
    Active = 1
}

public enum GiftCertificateSource
{
    Admin = 1,
    Purchased = 2
}

/// <summary>Ledger action of a <see cref="Entities.GiftCertificateTransaction"/>.</summary>
public enum GiftCertificateAction
{
    Created = 1,
    AdminAdjusted = 2,
    Redeemed = 3,
    /// <summary>Refunded or restored to the certificate (order refund / cancel).</summary>
    Refunded = 4,
    StatusChanged = 5,
    Voided = 6
}
