namespace HKPartsEcommerce.Domain.Enums;

/// <summary>Address classification in a customer's address book (Customers and Accounts; AC-CUS-002.2).</summary>
public enum AddressType
{
    Shipping = 0,
    Billing = 1
}

/// <summary>Purpose of a time-limited, single-use <see cref="Entities.UserToken"/>.</summary>
public enum UserTokenPurpose
{
    EmailVerification = 0,
    PasswordReset = 1
}
