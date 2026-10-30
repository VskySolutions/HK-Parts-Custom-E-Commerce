namespace HKPartsEcommerce.Domain.Common;

/// <summary>
/// The payment instrument used within a gateway, when one credential can charge more than one instrument
/// type. Today only Authorize.Net distinguishes these: a card (the default) versus an ACH/eCheck bank
/// account. Stored on <see cref="Entities.PaymentRecord.PaymentInstrument"/> and carried on a payment
/// request's metadata (<see cref="MetadataKey"/>) so the gateway adapter can apply the eCheck-specific
/// rules — ACH is capture-only, and an eCheck refund references a bank account rather than a credit card.
/// </summary>
public static class PaymentInstruments
{
    /// <summary>A card payment (the default when no instrument is specified).</summary>
    public const string Card = "Card";

    /// <summary>An ACH / eCheck bank-account payment (Authorize.Net).</summary>
    public const string BankAccount = "BankAccount";

    /// <summary>The payment-request metadata key carrying the instrument (read by the gateway adapter at authorize time).</summary>
    public const string MetadataKey = "instrument";

    /// <summary>True when <paramref name="instrument"/> denotes an ACH/eCheck bank account (case-insensitive).</summary>
    public static bool IsBankAccount(string? instrument)
        => string.Equals(instrument, BankAccount, StringComparison.OrdinalIgnoreCase);
}
