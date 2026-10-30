using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.EmailTemplates;

/// <summary>
/// Supplies deterministic, illustrative sample values for the mustache <c>{{tokens}}</c> used across
/// the seeded email templates, so previews and test-sends render realistically without touching any
/// live order/customer data (WO-79, AC-ENT-003.2 / AC-ENT-004.5). Values are fictitious.
/// </summary>
public static class TemplatePreviewData
{
    // Shared sample values covering every token used by DefaultEmailTemplates (subject/body chrome).
    private static readonly IReadOnlyDictionary<string, string> Base = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Branding / layout chrome (shared HTML wrapper)
        ["brandName"] = "HK Parts",
        ["primaryColor"] = "#4f46e5",
        ["footerAddress"] = "123 Market Street, San Francisco, CA 94103",
        ["unsubscribeBlock"] = "",

        // Customer
        ["customerName"] = "Jane Doe",

        // Account lifecycle
        ["verificationUrl"] = "https://example.com/verify-email?token=sample-verification-token",
        ["resetUrl"] = "https://example.com/reset-password?token=sample-reset-token",
        ["expiryMinutes"] = "60",
        ["changedAt"] = "January 1, 2026 12:00 PM UTC",
        ["ipAddress"] = "203.0.113.42",
        ["loginAt"] = "January 1, 2026 12:00 PM UTC",

        // Orders / billing
        ["orderNumber"] = "ORD-20260101120000",
        ["orderDate"] = "January 1, 2026",
        ["orderTotal"] = "$129.00",
        ["amountPaid"] = "$129.00",
        ["invoiceTotal"] = "$129.00",
        ["refundAmount"] = "$49.00",

        // Shipping
        ["carrier"] = "UPS",
        ["trackingNumber"] = "1Z999AA10123456784",
        ["trackingUrl"] = "https://example.com/track/1Z999AA10123456784",
        ["trackingStatus"] = "Out for delivery",
        ["deliveredAt"] = "January 3, 2026",

        // Quotes
        ["quoteNumber"] = "QUO-20260101120000",
        ["quoteExpiry"] = "January 31, 2026",

        // Returns
        ["rejectionReason"] = "The item shows signs of use beyond inspection.",

        // Tax exemption
        ["adminNote"] = "Approved — certificate verified.",

        // Marketing / catalog
        ["cartUrl"] = "https://example.com/cart",
        ["reviewUrl"] = "https://example.com/orders/ORD-20260101120000/review",
        ["productName"] = "Wireless Headphones",
        ["productUrl"] = "https://example.com/products/wireless-headphones",
        ["newPrice"] = "$79.00",
        ["oldPrice"] = "$99.00",
        ["promoCode"] = "SAVE20",
        ["discount"] = "20%",
        ["promoExpiry"] = "January 15, 2026",
        ["shopUrl"] = "https://example.com/shop",

        // Newsletter broadcast (content-driven template)
        ["subject"] = "This week's featured deals",
        ["heading"] = "Fresh picks just for you",
        ["content"] = "<p>Explore our latest arrivals and save up to 30% this week only.</p>",
    };

    private const string SampleUnsubscribeBlock =
        "<p style=\"margin:8px 0 0;\"><a href=\"https://example.com/unsubscribe?token=sample\">Unsubscribe</a> from these emails.</p>";

    /// <summary>
    /// Returns the token → sample-value map to substitute when rendering a preview or test-send of the
    /// given template. Marketing templates additionally receive a sample unsubscribe block, mirroring how
    /// real dispatches differ per <see cref="NotificationCategory"/>. Unknown tokens render empty.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ForTemplate(string templateKey, NotificationCategory category)
    {
        var values = new Dictionary<string, string>(Base, StringComparer.Ordinal);
        if (category == NotificationCategory.Marketing)
            values["unsubscribeBlock"] = SampleUnsubscribeBlock;
        return values;
    }
}
