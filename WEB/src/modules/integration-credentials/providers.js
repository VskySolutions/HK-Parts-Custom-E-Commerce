/*
 * Catalogue for the Integrations hub. Each entry drives the left-hand nav list (icon + label, grouped by
 * category) and the right-hand panel. `kind` selects which panel renders:
 *   - 'credential' (default): typed secret CRUD for a Credentials_* table (fields drive the form).
 *   - 'storage': the file-storage provider settings panel.
 *   - 'smtp': the SMTP sending-accounts panel.
 * `key` is the API route slug for credential items (`/api/integration-credentials/{key}`).
 * `pricing` ('free' | 'paid') and `website` (official console/developer URL for sign-up + API-credential
 * generation) are attached below via INTEGRATION_META and drive the badge + external link in the panel title.
 */

// Shared optional fee field for the payment gateways: a percentage of the order total added to the amount
// the customer pays at checkout when that gateway is used. `type: 'percent'` renders a numeric input.
const TRANSACTION_FEE_FIELD = {
  key: 'transactionFeePercent',
  label: 'Transaction fee (%)',
  type: 'percent',
  placeholder: 'e.g. 4',
  hints: ['Charged as a percent of the order total and added to what the customer pays at checkout when this gateway is used. Leave blank for no fee.']
}

export const INTEGRATIONS = [
  // ---- Payments ----
  {
    key: 'stripe', kind: 'credential', label: 'Stripe', category: 'Payments', icon: 'o_credit_card',
    description: 'Cards, wallets and more via Stripe (redirect Checkout).',
    fields: [
      { key: 'publishableKey', label: 'Publishable Key', placeholder: 'pk_live_…' },
      { key: 'secretKey', label: 'Secret Key', secret: true, required: true, placeholder: 'sk_live_…' },
      {
        key: 'returnUrl', label: 'Return URL', required: true,
        prefill: '{origin}/shop/checkout',
        placeholder: 'https://your-store.com/shop/checkout',
        hints: ['Storefront page Stripe returns buyers to after payment — the base URL is prefilled; edit the route as needed.']
      },
      TRANSACTION_FEE_FIELD
    ]
  },
  {
    key: 'paypal', kind: 'credential', label: 'PayPal', category: 'Payments', icon: 'o_account_balance_wallet',
    description: 'PayPal Checkout (REST app credentials).',
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://api-m.sandbox.paypal.com',
        hints: ['Sandbox: https://api-m.sandbox.paypal.com', 'Live: https://api-m.paypal.com']
      },
      { key: 'clientId', label: 'Client ID', required: true },
      { key: 'secretKey', label: 'Secret', secret: true, required: true },
      {
        key: 'returnUrl', label: 'Return URL', required: true,
        prefill: '{origin}/shop/checkout',
        placeholder: 'https://your-store.com/shop/checkout',
        hints: ['Storefront page PayPal returns buyers to after approving payment — the base URL is prefilled; edit the route as needed.']
      },
      TRANSACTION_FEE_FIELD
    ]
  },
  {
    key: 'razorpay', kind: 'credential', label: 'Razorpay', category: 'Payments', icon: 'o_payments',
    description: 'Razorpay payments.',
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://api.razorpay.com/v1',
        hints: ['Live and test share one host — the key prefix (rzp_test_/rzp_live_) decides the mode.']
      },
      { key: 'keyId', label: 'Key ID', required: true, placeholder: 'rzp_live_…' },
      { key: 'keySecret', label: 'Key Secret', secret: true, required: true },
      TRANSACTION_FEE_FIELD
    ]
  },
  {
    key: 'square', kind: 'credential', label: 'Square', category: 'Payments', icon: 'o_qr_code_2',
    description: 'Square payments (on-site card entry via the Web Payments SDK).',
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://connect.squareupsandbox.com/v2',
        hints: ['Sandbox: https://connect.squareupsandbox.com/v2', 'Live: https://connect.squareup.com/v2']
      },
      {
        key: 'applicationId', label: 'Application ID', placeholder: 'sandbox-sq0idb-… / sq0idp-…',
        hints: ['Public app id the storefront card field (Web Payments SDK) uses — required to take card payments.']
      },
      {
        key: 'locationId', label: 'Location ID', placeholder: 'e.g. L1A2B3C4D5E6F',
        hints: ['The Square Location payments are taken at — required by the storefront card field and stamped on each charge.']
      },
      { key: 'accessToken', label: 'Access Token', secret: true, required: true },
      { key: 'applicationSecret', label: 'Application Secret', secret: true },
      TRANSACTION_FEE_FIELD
    ]
  },
  {
    key: 'authorizenet', kind: 'credential', label: 'Authorize.Net', category: 'Payments', icon: 'o_credit_score',
    description: 'Authorize.Net payment gateway.',
    fields: [
      {
        key: 'baseUrl',
        label: 'Endpoint URL',
        required: true,
        placeholder: 'https://apitest.authorize.net/xml/v1/request.api',
        hints: [
          'Sandbox: https://apitest.authorize.net/xml/v1/request.api',
          'Live: https://api.authorize.net/xml/v1/request.api'
        ]
      },
      { key: 'applicationLoginId', label: 'API Login ID', required: true },
      { key: 'transactionKey', label: 'Transaction Key', secret: true, required: true },
      { key: 'signatureKey', label: 'Signature Key', secret: true },
      {
        key: 'publicClientKey',
        label: 'Public Client Key',
        required: true,
        hints: [
          'Public key the storefront uses to tokenize the card (Accept.js). Not a secret.',
          'Authorize.Net Merchant Interface → Account → Settings → Manage Public Client Key.'
        ]
      },
      TRANSACTION_FEE_FIELD
    ]
  },

  // ---- Tax ----
  {
    key: 'taxjar', kind: 'credential', label: 'TaxJar', category: 'Tax', icon: 'o_receipt_long',
    description: 'TaxJar sales-tax API.',
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://api.sandbox.taxjar.com',
        hints: ['Sandbox: https://api.sandbox.taxjar.com', 'Live: https://api.taxjar.com']
      },
      { key: 'secretKey', label: 'API Token', secret: true, required: true }
    ]
  },
  {
    key: 'stripe-tax', kind: 'credential', label: 'Stripe Tax', category: 'Tax', icon: 'o_receipt',
    description: 'Stripe Tax calculation.',
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://api.stripe.com',
        hints: ['Live and test share one host — the key prefix (sk_test_/sk_live_) decides the mode.']
      },
      { key: 'publishableKey', label: 'Publishable Key', placeholder: 'pk_live_…' },
      { key: 'secretKey', label: 'Secret Key', secret: true, required: true, placeholder: 'sk_live_…' }
    ]
  },

  // ---- Shipping ----
  {
    key: 'fedex', kind: 'credential', label: 'FedEx', category: 'Shipping', icon: 'o_local_shipping',
    description: 'FedEx rates and labels.',
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://apis-sandbox.fedex.com',
        hints: ['Sandbox: https://apis-sandbox.fedex.com', 'Live: https://apis.fedex.com']
      },
      { key: 'apiKey', label: 'API Key', required: true },
      { key: 'apiSecret', label: 'API Secret', secret: true, required: true },
      {
        key: 'accountNumber',
        label: 'Account Number',
        required: true,
        placeholder: 'FedEx account number',
        hints: ['The key and secret only authenticate you — FedEx rejects a rate request that names no account with a 403.']
      }
    ]
  },
  {
    key: 'dhl', kind: 'credential', label: 'DHL Express', category: 'Shipping', icon: 'o_local_shipping',
    description: 'DHL Express rates and labels.',
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://express.api.dhl.com/mydhlapi/test',
        hints: ['Test: https://express.api.dhl.com/mydhlapi/test', 'Live: https://express.api.dhl.com/mydhlapi']
      },
      { key: 'apiKey', label: 'API Key', required: true },
      { key: 'apiSecret', label: 'API Secret', secret: true, required: true },
      {
        key: 'accountNumber',
        label: 'Account Number',
        placeholder: 'DHL Express account number',
        hints: ['Optional — without it MyDHL quotes generic products rather than your negotiated rates.']
      }
    ]
  },
  {
    key: 'usps', kind: 'credential', label: 'USPS', category: 'Shipping', icon: 'o_markunread_mailbox',
    description: 'USPS shipping.',
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://apis-tem.usps.com',
        hints: ['Test (TEM): https://apis-tem.usps.com', 'Live: https://apis.usps.com']
      },
      { key: 'consumerKey', label: 'Consumer Key', required: true },
      { key: 'consumerSecret', label: 'Consumer Secret', secret: true, required: true }
    ]
  },
  {
    key: 'ups', kind: 'credential', label: 'UPS', category: 'Shipping', icon: 'o_local_shipping',
    description: 'UPS rates and labels (OAuth2 client credentials).',
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://wwwcie.ups.com',
        hints: ['Test (CIE): https://wwwcie.ups.com', 'Live: https://onlinetools.ups.com']
      },
      { key: 'merchantId', label: 'Merchant ID', placeholder: 'UPS account / shipper number' },
      { key: 'clientId', label: 'Client ID', required: true },
      { key: 'clientSecret', label: 'Client Secret', secret: true, required: true }
    ]
  },

  // ---- Communication ----
  {
    key: 'smtp', kind: 'smtp', label: 'Email (SMTP) Accounts', category: 'Communication', icon: 'o_mail',
    description: 'SMTP sending accounts, categories and test-send.'
  },
  {
    key: 'twilio', kind: 'credential', label: 'Twilio', category: 'Communication', icon: 'o_sms',
    description: 'Twilio SMS / WhatsApp.',
    fields: [
      { key: 'accountSid', label: 'Account SID', required: true, placeholder: 'AC…' },
      { key: 'authToken', label: 'Auth Token', secret: true, required: true },
      { key: 'whatsAppNumber', label: 'WhatsApp Number', placeholder: '+1…' }
    ]
  },

  // ---- Storage ----
  {
    key: 'storage-settings', kind: 'storage', label: 'Storage Settings', category: 'Storage', icon: 'o_settings',
    description: 'Active file-storage provider, container and CDN.'
  },
  {
    key: 'azure-blob', kind: 'credential', label: 'Azure Blob Storage', category: 'Storage', icon: 'o_cloud',
    description: 'Azure Blob object storage connection string.',
    fields: [
      { key: 'connectionString', label: 'Connection String', secret: true, required: true }
    ]
  },

  // ---- Security ----
  {
    key: 'recaptcha', kind: 'recaptcha', label: 'reCAPTCHA', category: 'Security', icon: 'o_verified_user',
    description: 'Google reCAPTCHA bot protection for storefront forms.'
  }
]

/*
 * "Free"/"Paid" classification and the official console/developer URL where an admin signs up and generates
 * the API credentials for each integration. Kept as a side table so the catalogue above stays about form
 * structure; merged onto each entry below. Internal settings panels (SMTP, Storage Settings) are omitted —
 * they have no external sign-up, so no badge/link renders for them.
 */
const INTEGRATION_META = {
  stripe: { pricing: 'paid', website: 'https://dashboard.stripe.com/apikeys' },
  paypal: { pricing: 'paid', website: 'https://developer.paypal.com/dashboard/applications' },
  razorpay: { pricing: 'paid', website: 'https://dashboard.razorpay.com/app/keys' },
  square: { pricing: 'paid', website: 'https://developer.squareup.com/apps' },
  authorizenet: { pricing: 'paid', website: 'https://developer.authorize.net' },
  taxjar: { pricing: 'paid', website: 'https://app.taxjar.com/account#api-access' },
  'stripe-tax': { pricing: 'paid', website: 'https://dashboard.stripe.com/tax' },
  fedex: { pricing: 'free', website: 'https://developer.fedex.com' },
  dhl: { pricing: 'free', website: 'https://developer.dhl.com' },
  usps: { pricing: 'free', website: 'https://developer.usps.com' },
  ups: { pricing: 'free', website: 'https://developer.ups.com' },
  twilio: { pricing: 'paid', website: 'https://console.twilio.com' },
  'azure-blob': { pricing: 'paid', website: 'https://portal.azure.com' },
  recaptcha: { pricing: 'free', website: 'https://www.google.com/recaptcha/admin/create' }
}

// Merge pricing/website onto each catalogue entry (missing keys are a safe no-op via Object.assign(_, undefined)).
for (const it of INTEGRATIONS) Object.assign(it, INTEGRATION_META[it.key])

/** Category display order for the grouped left-hand list. */
const CATEGORY_ORDER = ['Payments', 'Tax', 'Shipping', 'Communication', 'Storage', 'Security']

const CATEGORY_ICON = {
  Payments: 'o_payments',
  Tax: 'o_receipt_long',
  Shipping: 'o_local_shipping',
  Communication: 'o_forum',
  Storage: 'o_cloud',
  Security: 'o_shield'
}

/** Integrations grouped into ordered categories for rendering the left nav. */
export const INTEGRATION_GROUPS = CATEGORY_ORDER
  .map((category) => ({
    category,
    icon: CATEGORY_ICON[category],
    items: INTEGRATIONS.filter((i) => i.category === category)
  }))
  .filter((g) => g.items.length > 0)

export function findIntegration (key) {
  return INTEGRATIONS.find((i) => i.key === key) || null
}
