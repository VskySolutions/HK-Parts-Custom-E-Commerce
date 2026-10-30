/*
 * Dynamic-variable reference catalog for email templates (WO-80, REQ-ENT-002).
 *
 * Mirrors the sample tokens the API substitutes at render time
 * (API/src/Application/Features/EmailTemplates/TemplatePreviewData.cs) and adds a
 * human-readable description for each. `extractTokens` finds the {{tokens}}
 * actually used by a template, so the editor can show a template-specific list
 * rather than the whole catalog.
 */

// token -> { description, sample }. Descriptions authored here; samples mirror TemplatePreviewData.
export const VARIABLE_CATALOG = {
  // Branding / layout chrome (shared HTML wrapper)
  brandName: { description: 'Your store / brand name.', sample: 'HK Parts' },
  primaryColor: { description: 'Brand primary colour (hex) used in the email header.', sample: '#4f46e5' },
  footerAddress: { description: 'Business mailing address shown in the footer.', sample: '123 Market Street, San Francisco, CA 94103' },
  unsubscribeBlock: { description: 'Unsubscribe link block (added automatically to marketing emails).', sample: '' },

  // Customer
  customerName: { description: "Recipient's full name.", sample: 'Jane Doe' },

  // Account lifecycle
  verificationUrl: { description: 'Email-verification link.', sample: 'https://example.com/verify-email?token=…' },
  resetUrl: { description: 'Password-reset link.', sample: 'https://example.com/reset-password?token=…' },
  expiryMinutes: { description: 'Minutes until the link expires.', sample: '60' },
  changedAt: { description: 'Date/time the password was changed.', sample: 'January 1, 2026 12:00 PM UTC' },
  ipAddress: { description: 'IP address of the sign-in.', sample: '203.0.113.42' },
  loginAt: { description: 'Date/time of the sign-in.', sample: 'January 1, 2026 12:00 PM UTC' },

  // Orders / billing
  orderNumber: { description: 'Order reference number.', sample: 'ORD-20260101120000' },
  orderDate: { description: 'Date the order was placed.', sample: 'January 1, 2026' },
  orderTotal: { description: 'Order total amount.', sample: '$129.00' },
  amountPaid: { description: 'Amount paid.', sample: '$129.00' },
  invoiceTotal: { description: 'Invoice amount due.', sample: '$129.00' },
  refundAmount: { description: 'Amount refunded.', sample: '$49.00' },

  // Shipping
  carrier: { description: 'Shipping carrier name.', sample: 'UPS' },
  trackingNumber: { description: 'Shipment tracking number.', sample: '1Z999AA10123456784' },
  trackingUrl: { description: 'Shipment tracking link.', sample: 'https://example.com/track/…' },
  trackingStatus: { description: 'Current shipment status.', sample: 'Out for delivery' },
  deliveredAt: { description: 'Delivery date.', sample: 'January 3, 2026' },

  // Quotes
  quoteNumber: { description: 'Quote reference number.', sample: 'QUO-20260101120000' },
  quoteExpiry: { description: 'Quote expiry date.', sample: 'January 31, 2026' },

  // Returns
  rejectionReason: { description: 'Reason a return was rejected.', sample: 'The item shows signs of use…' },

  // Tax exemption
  adminNote: { description: "Reviewer's note shown on a tax-exemption approval or rejection.", sample: 'Approved — certificate verified.' },

  // Marketing / catalog
  cartUrl: { description: "Link back to the customer's cart.", sample: 'https://example.com/cart' },
  reviewUrl: { description: 'Product-review link.', sample: 'https://example.com/orders/…/review' },
  productName: { description: 'Product name.', sample: 'Wireless Headphones' },
  productUrl: { description: 'Product page link.', sample: 'https://example.com/products/…' },
  newPrice: { description: 'New (dropped) price.', sample: '$79.00' },
  oldPrice: { description: 'Previous price.', sample: '$99.00' },
  promoCode: { description: 'Promotional discount code.', sample: 'SAVE20' },
  discount: { description: 'Discount amount / percentage.', sample: '20%' },
  promoExpiry: { description: 'Promotion expiry date.', sample: 'January 15, 2026' },
  shopUrl: { description: 'Link to the shop.', sample: 'https://example.com/shop' },

  // Newsletter broadcast (content-driven template)
  subject: { description: 'Broadcast subject line (newsletter).', sample: "This week's featured deals" },
  heading: { description: 'Broadcast heading (newsletter).', sample: 'Fresh picks just for you' },
  content: { description: 'Broadcast HTML content (newsletter).', sample: '<p>…</p>' }
}

// Same token grammar the backend renderer uses: {{ token }} with word chars.
const TOKEN_RE = /\{\{\s*(\w+)\s*\}\}/g

// Unique token names used across the given text fragments, in first-seen order.
export function extractTokens (...texts) {
  const seen = []
  for (const text of texts) {
    if (!text) continue
    TOKEN_RE.lastIndex = 0
    let m
    while ((m = TOKEN_RE.exec(text)) !== null) {
      if (!seen.includes(m[1])) seen.push(m[1])
    }
  }
  return seen
}

// Enrich detected tokens with catalog metadata (falls back for unknown tokens).
export function describeTokens (tokens) {
  return tokens.map((token) => ({
    token,
    description: VARIABLE_CATALOG[token]?.description || 'Custom placeholder.',
    sample: VARIABLE_CATALOG[token]?.sample ?? ''
  }))
}
