using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Persistence.Seed;

/// <summary>
/// The canonical set of platform notification events, each seeded with a default email template.
/// Templates use mustache-style {{tokens}} resolved at render time (Email Notification Templates blueprint).
/// </summary>
public static class DefaultEmailTemplates
{
    private sealed record Seed(
        string Key,
        string Name,
        NotificationCategory Category,
        string Subject,
        string Heading,
        string Body,
        bool Critical = false);

    private static readonly Seed[] Seeds =
    {
        // ---- Transactional ----
        new("account.registration", "Account Registration", NotificationCategory.Transactional,
            "Welcome to {{brandName}}, {{customerName}}!",
            "Welcome aboard 🎉",
            "<p>Hi {{customerName}},</p><p>Your account has been created successfully. We're glad to have you at {{brandName}}.</p>"),
        new("account.email-verification", "Email Verification", NotificationCategory.Transactional,
            "Verify your email address",
            "Confirm your email",
            "<p>Hi {{customerName}},</p><p>Please confirm your email address by clicking the button below.</p><p><a href=\"{{verificationUrl}}\">Verify email</a></p>",
            Critical: true),
        new("account.password-reset", "Password Reset", NotificationCategory.Transactional,
            "Reset your password",
            "Password reset requested",
            "<p>Hi {{customerName}},</p><p>We received a request to reset your password. This link expires in {{expiryMinutes}} minutes.</p><p><a href=\"{{resetUrl}}\">Reset password</a></p><p>If you didn't request this, you can ignore this email.</p>",
            Critical: true),
        new("account.password-changed", "Password Changed", NotificationCategory.Transactional,
            "Your password was changed",
            "Password updated",
            "<p>Hi {{customerName}},</p><p>This is a confirmation that your password was changed on {{changedAt}}.</p>"),
        new("account.login-alert", "New Login Alert", NotificationCategory.Transactional,
            "New sign-in to your account",
            "New sign-in detected",
            "<p>Hi {{customerName}},</p><p>A new sign-in to your account was detected from {{ipAddress}} at {{loginAt}}.</p>"),
        // ---- Inquiries (REQ-INQ-001) ----
        new("inquiry.acknowledgement", "Inquiry Received", NotificationCategory.Transactional,
            "We received your request {{inquiryNumber}}",
            "Thanks for your enquiry!",
            "<p>Hi {{customerName}},</p><p>We have received your request <strong>{{inquiryNumber}}</strong> submitted on {{inquiryDate}}.</p><p>Items requested: {{itemsSummary}}</p><p>No payment has been taken. Our team will review your request and get back to you shortly.</p>"),
        new("inquiry.store-notification", "New Inquiry (Store)", NotificationCategory.Transactional,
            "New inquiry {{inquiryNumber}} from {{customerName}}",
            "New inquiry received",
            "<p>A new inquiry <strong>{{inquiryNumber}}</strong> was submitted on {{inquiryDate}}.</p><p>Customer: {{customerName}}<br>Email: {{customerEmail}}<br>Phone: {{customerPhone}}<br>Company: {{companyName}}</p><p>Items: {{itemsSummary}}<br>Indicative value: <strong>{{estimatedValue}}</strong></p><p>Message:<br>{{message}}</p>"),
        new("inquiry.quote", "Inquiry Quote", NotificationCategory.Transactional,
            "Your quote for {{inquiryNumber}}",
            "Here is your quote",
            "<p>Hi {{customerName}},</p><p>Thank you for your enquiry <strong>{{inquiryNumber}}</strong>. Our quote is <strong>{{quoteAmount}}</strong>.</p><p>{{quoteNote}}</p><p>Reply to this email to accept or ask any questions.</p>"),
        new("order.confirmation", "Order Confirmation", NotificationCategory.Transactional,
            "Your {{brandName}} order {{orderNumber}} is confirmed",
            "Thanks for your order!",
            "<p>Hi {{customerName}},</p><p>We've received your order <strong>{{orderNumber}}</strong> placed on {{orderDate}}. Order total: <strong>{{orderTotal}}</strong>.</p>"),
        new("order.store-notification", "New Order (Store)", NotificationCategory.Transactional,
            "New order {{orderNumber}} to fulfil",
            "New order received",
            "<p>A new order <strong>{{orderNumber}}</strong> has been placed and routed to {{storeName}}.</p><p>Customer: {{customerName}} ({{customerEmail}})<br>Order total: <strong>{{orderTotal}}</strong><br>Placed: {{orderDate}}</p><p>Please prepare it for fulfilment.</p>"),
        new("order.processing", "Order Processing", NotificationCategory.Transactional,
            "We're preparing your order {{orderNumber}}",
            "Your order is being prepared",
            "<p>Hi {{customerName}},</p><p>Your order <strong>{{orderNumber}}</strong> is now being processed and prepared for shipment. We'll let you know as soon as it's on its way.</p>"),
        new("order.payment-received", "Payment Received", NotificationCategory.Transactional,
            "Payment received for order {{orderNumber}}",
            "Payment confirmed",
            "<p>Hi {{customerName}},</p><p>We've received your payment of {{amountPaid}} for order {{orderNumber}}.</p>"),
        new("order.payment-failed", "Payment Failed", NotificationCategory.Transactional,
            "Payment failed for order {{orderNumber}}",
            "Payment could not be processed",
            "<p>Hi {{customerName}},</p><p>Unfortunately your payment for order {{orderNumber}} could not be processed. Please update your payment method.</p>"),
        new("order.shipped", "Shipping Notification", NotificationCategory.Transactional,
            "Your order {{orderNumber}} has shipped",
            "Your order is on its way 🚚",
            "<p>Hi {{customerName}},</p><p>Good news — order {{orderNumber}} has shipped via {{carrier}}. Tracking number: <strong>{{trackingNumber}}</strong>.</p><p><a href=\"{{trackingUrl}}\">Track your shipment</a></p>"),
        new("order.delivered", "Order Delivered", NotificationCategory.Transactional,
            "Your order {{orderNumber}} was delivered",
            "Delivered!",
            "<p>Hi {{customerName}},</p><p>Your order {{orderNumber}} was delivered on {{deliveredAt}}. We hope you love it.</p>"),
        new("order.cancelled", "Order Cancelled", NotificationCategory.Transactional,
            "Your order {{orderNumber}} has been cancelled",
            "Order cancelled",
            "<p>Hi {{customerName}},</p><p>Your order {{orderNumber}} has been cancelled. Any authorized payment will be released.</p>"),
        new("order.refunded", "Refund Processed", NotificationCategory.Transactional,
            "Refund processed for order {{orderNumber}}",
            "Refund on its way",
            "<p>Hi {{customerName}},</p><p>A refund of {{refundAmount}} for order {{orderNumber}} has been processed and will appear on your statement shortly.</p>"),
        new("order.invoice", "Invoice / Receipt", NotificationCategory.Transactional,
            "Invoice for order {{orderNumber}}",
            "Your invoice",
            "<p>Hi {{customerName}},</p><p>Please find attached the invoice for order {{orderNumber}}. Amount due: {{invoiceTotal}}.</p>"),
        new("shipping.tracking-update", "Tracking Update", NotificationCategory.Transactional,
            "Tracking update for order {{orderNumber}}",
            "Shipment update",
            "<p>Hi {{customerName}},</p><p>The status of your shipment for order {{orderNumber}} is now: <strong>{{trackingStatus}}</strong>.</p>"),
        new("quote.delivery", "Quote PDF Delivery", NotificationCategory.Transactional,
            "Your quote {{quoteNumber}} from {{brandName}}",
            "Here's your quote",
            "<p>Hi {{customerName}},</p><p>Please find attached your requested quote {{quoteNumber}}, valid until {{quoteExpiry}}.</p>"),
        new("return.requested", "Return Requested", NotificationCategory.Transactional,
            "We received your return request for {{orderNumber}}",
            "Return request received",
            "<p>Hi {{customerName}},</p><p>We've received your return request for order {{orderNumber}}. We'll review it shortly.</p>"),
        new("return.approved", "Return Approved", NotificationCategory.Transactional,
            "Your return for {{orderNumber}} is approved",
            "Return approved",
            "<p>Hi {{customerName}},</p><p>Your return for order {{orderNumber}} has been approved. Please follow the instructions to ship the item back.</p>"),
        new("return.rejected", "Return Rejected", NotificationCategory.Transactional,
            "Update on your return for {{orderNumber}}",
            "Return could not be approved",
            "<p>Hi {{customerName}},</p><p>Unfortunately your return request for order {{orderNumber}} could not be approved. Reason: {{rejectionReason}}.</p>"),
        new("tax-exemption.approved", "Tax Exemption Approved", NotificationCategory.Transactional,
            "Your tax exemption has been approved",
            "Tax exemption approved",
            "<p>Hi {{customerName}},</p><p>Your tax exemption request has been approved. Tax will no longer be charged on your orders.</p><p>{{adminNote}}</p>"),
        new("tax-exemption.rejected", "Tax Exemption Rejected", NotificationCategory.Transactional,
            "Update on your tax exemption request",
            "Tax exemption could not be approved",
            "<p>Hi {{customerName}},</p><p>Your tax exemption request could not be approved, so tax will continue to apply to your orders. You're welcome to submit a new request.</p><p>{{adminNote}}</p>"),
        new("tax-exemption.submitted", "Tax Exemption Submitted (Admin)", NotificationCategory.Transactional,
            "New tax exemption request from {{customerName}}",
            "New tax exemption request to review",
            "<p>A customer has submitted a tax exemption request that is awaiting review.</p><p>Customer: <strong>{{customerName}}</strong> ({{customerEmail}})<br>Certificate number: {{certificateNumber}}<br>VAT ID: {{vatId}}<br>Documents: {{documentCount}}<br>Submitted: {{submittedOn}}</p><p>Please review it in the admin portal.</p>"),

        // ---- Marketing ----
        new("cart.abandoned", "Abandoned Cart Recovery", NotificationCategory.Marketing,
            "You left something behind at {{brandName}}",
            "Still thinking it over?",
            "<p>Hi {{customerName}},</p><p>You left items in your cart. Complete your purchase before they're gone.</p><p><a href=\"{{cartUrl}}\">Return to cart</a></p>"),
        new("review.request", "Product Review Request", NotificationCategory.Marketing,
            "How did we do, {{customerName}}?",
            "Share your feedback",
            "<p>Hi {{customerName}},</p><p>We'd love your review of your recent purchase from order {{orderNumber}}.</p><p><a href=\"{{reviewUrl}}\">Write a review</a></p>"),
        new("back-in-stock", "Back In Stock", NotificationCategory.Marketing,
            "{{productName}} is back in stock",
            "It's back!",
            "<p>Hi {{customerName}},</p><p>Good news — <strong>{{productName}}</strong> is back in stock. Get it before it sells out again.</p><p><a href=\"{{productUrl}}\">Shop now</a></p>"),
        new("wishlist.price-drop", "Wishlist Price Drop", NotificationCategory.Marketing,
            "Price drop on your wishlist item",
            "A price just dropped",
            "<p>Hi {{customerName}},</p><p><strong>{{productName}}</strong> from your wishlist is now {{newPrice}} (was {{oldPrice}}).</p><p><a href=\"{{productUrl}}\">View item</a></p>"),
        new("newsletter.welcome", "Newsletter Welcome", NotificationCategory.Marketing,
            "Welcome to the {{brandName}} newsletter",
            "You're subscribed 🎉",
            "<p>Hi {{customerName}},</p><p>Thanks for subscribing. Expect the best deals and news from {{brandName}} in your inbox.</p>"),
        new("newsletter.broadcast", "Newsletter Broadcast", NotificationCategory.Marketing,
            "{{subject}}",
            "{{heading}}",
            "<p>Hi {{customerName}},</p>{{content}}"),
        new("promotion.offer", "Promotional Offer", NotificationCategory.Marketing,
            "A special offer just for you, {{customerName}}",
            "An offer you'll like",
            "<p>Hi {{customerName}},</p><p>Use code <strong>{{promoCode}}</strong> to get {{discount}} off your next order. Hurry — expires {{promoExpiry}}.</p><p><a href=\"{{shopUrl}}\">Shop the sale</a></p>"),
    };

    private static string WrapHtml(string heading, string body) =>
        $$"""
        <!DOCTYPE html>
        <html>
          <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
          <body style="margin:0;padding:0;background:#f4f5f7;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f5f7;padding:24px 0;">
              <tr><td align="center">
                <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                  <tr><td style="background:{{"{{primaryColor}}"}};padding:20px 32px;color:#ffffff;font-size:20px;font-weight:bold;">{{"{{brandName}}"}}</td></tr>
                  <tr><td style="padding:32px;">
                    <h1 style="margin:0 0 16px;font-size:22px;">{{heading}}</h1>
                    {{body}}
                  </td></tr>
                  <tr><td style="padding:20px 32px;background:#f9fafb;color:#6b7280;font-size:12px;">
                    <p style="margin:0;">&copy; {{"{{brandName}}"}}. {{"{{footerAddress}}"}}</p>
                    {{"{{unsubscribeBlock}}"}}
                  </td></tr>
                </table>
              </td></tr>
            </table>
          </body>
        </html>
        """;

    private static string ToPlainText(string heading, string body)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ").Trim();
        return $"{heading}\n\n{text}";
    }

    /// <summary>Builds fresh <see cref="EmailTemplate"/> entities for seeding (one per notification event).</summary>
    public static IReadOnlyList<EmailTemplate> Build() =>
        Seeds.Select(s => new EmailTemplate
        {
            TemplateKey = s.Key,
            Name = s.Name,
            Category = s.Category,
            SubjectLine = s.Subject,
            HtmlBody = WrapHtml(s.Heading, s.Body),
            PlainTextBody = ToPlainText(s.Heading, s.Body),
            Enabled = true,
            IsCritical = s.Critical,
            Description = $"Default template for the '{s.Key}' notification event."
        }).ToList();

    public static IReadOnlyList<string> Keys => Seeds.Select(s => s.Key).ToList();
}
