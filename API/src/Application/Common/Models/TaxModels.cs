namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>
/// An origin (ship-from) or destination (ship-to) address used to resolve the applicable tax
/// jurisdictions for a calculation (REQ-TAX-001). The street lines are optional: the rate methods ignore them,
/// Avalara uses them for rooftop-level jurisdictions.
/// </summary>
public record TaxAddress(
    string CountryCode,
    string? Region = null,
    string? PostalCode = null,
    string? City = null,
    string? Line1 = null,
    string? Line2 = null);

/// <summary>
/// A single taxable line in a <see cref="TaxCalculationRequest"/>. <see cref="Amount"/> is the
/// per-unit price; the gross line value is <see cref="Amount"/> × <see cref="Quantity"/>, and the
/// <b>taxable</b> value is that less <see cref="DiscountAmount"/> (the order/product/coupon discount
/// allocated to this line) — so tax is charged on the discounted price (REQ-PRP/REQ-TAX).
/// <see cref="TaxCategoryCode"/> is the provider tax code the product's Tax Category maps to;
/// <see cref="ProviderTaxCode"/> is the product's own tax code (PHP product tax_code), preferred by Avalara.
/// </summary>
public record TaxLineInput(
    Guid ProductId,
    string? TaxCategoryCode,
    decimal Amount,
    int Quantity,
    decimal DiscountAmount = 0m,
    string? ItemCode = null,
    string? Description = null,
    string? ProviderTaxCode = null);

/// <summary>
/// Input to a tax calculation: the ship-from <see cref="Origin"/>, ship-to <see cref="Destination"/>,
/// the taxable <see cref="Lines"/>, the (separately taxable) <see cref="ShippingAmount"/> and an optional
/// customer <see cref="Exemption"/> (REQ-TAX-003).
/// <para>
/// PHP parity: <see cref="CustomerGroupId"/> lets a customer group override the store's tax method;
/// <see cref="CustomerCode"/> (the buyer's email), <see cref="ExemptionNumber"/> (the customer's tax-exempt
/// reason) and <see cref="ShippingDescription"/> are forwarded to Avalara.
/// </para>
/// </summary>
public record TaxCalculationRequest(
    TaxAddress Origin,
    TaxAddress Destination,
    List<TaxLineInput> Lines,
    decimal ShippingAmount,
    TaxExemption? Exemption = null,
    Guid? CustomerGroupId = null,
    string? CustomerCode = null,
    string? ExemptionNumber = null,
    string? ShippingDescription = null);

/// <summary>
/// A customer's tax-exemption status carried into a calculation (AC-TAX-003.3). When
/// <see cref="IsExempt"/> is set the calculation yields zero tax; the certificate / VAT id are captured
/// for the audit trail and, where supported, forwarded to the provider.
/// </summary>
public record TaxExemption(bool IsExempt, string? CertificateNumber = null, string? VatId = null);

/// <summary>
/// One jurisdiction's contribution to the total tax (e.g. country/state/county/city/special district).
/// <see cref="Rate"/> is a decimal fraction (0.0825 = 8.25%).
/// </summary>
public record TaxJurisdiction(
    string Name,
    string? Type,
    decimal Rate,
    decimal Amount);

/// <summary>
/// Result of a tax calculation: the <see cref="TotalTax"/> to collect, the per-jurisdiction
/// <see cref="Jurisdictions"/> breakdown, and <see cref="FallbackApplied"/> — set when the flat-rate
/// fallback was used because the active provider was unavailable (AC-TAX-001.4).
/// </summary>
public record TaxBreakdown(
    decimal TotalTax,
    List<TaxJurisdiction> Jurisdictions,
    bool FallbackApplied,
    string? ProviderReference = null);

/// <summary>
/// One returned line of a <see cref="TaxReturnRequest"/>: the order line (null = a line not tied to one), the units
/// returned and the pre-tax value given back for them (positive; the provider client negates it).
/// </summary>
public record TaxReturnLine(Guid? OrderLineItemId, decimal Quantity, decimal Amount);

/// <summary>
/// A refund reported to the tax provider (PHP EcommOrder::applyCreditAvalara, a committed Avalara ReturnInvoice).
/// <see cref="TaxAmount"/> is the tax actually given back, so the provider's records match the money refunded.
/// <see cref="Reference"/> distinguishes the return (e.g. the RMA number or "Refund").
/// </summary>
public record TaxReturnRequest(
    Guid OrderId,
    List<TaxReturnLine> Lines,
    decimal ShippingAmount,
    decimal TaxAmount,
    string Reference);

/// <summary>Result of the Avalara Test Connection button (PHP pingTest).</summary>
public record TaxConnectionTestResult(bool Success, string Message);
