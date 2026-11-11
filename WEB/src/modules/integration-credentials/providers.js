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

// UPS service codes (ups-shipping-requirements.md §3.2); shared by the allowed and FFL service pickers.
const UPS_SERVICE_OPTIONS = [
  { label: '03 — Ground', value: '03' },
  { label: '12 — 3 Day Select', value: '12' },
  { label: '02 — 2nd Day Air', value: '02' },
  { label: '59 — 2nd Day Air AM', value: '59' },
  { label: '13 — Next Day Air Saver', value: '13' },
  { label: '01 — Next Day Air', value: '01' },
  { label: '14 — Next Day Air Early AM', value: '14' },
  { label: '65 — UPS Saver', value: '65' },
  { label: '11 — UPS Standard (Canada / Mexico)', value: '11' },
  { label: '07 — Worldwide Express', value: '07' },
  { label: '08 — Worldwide Expedited', value: '08' },
  { label: '54 — Worldwide Express Plus', value: '54' },
  { label: '96 — Worldwide Express Freight', value: '96' }
]

// FedEx REST service types (fedex-shipping-requirements.md §3.2); shared by the allowed and FFL pickers.
const FEDEX_SERVICE_OPTIONS = [
  { label: 'Ground', value: 'FEDEX_GROUND' },
  { label: 'Ground Home Delivery', value: 'GROUND_HOME_DELIVERY' },
  { label: 'Express Saver', value: 'FEDEX_EXPRESS_SAVER' },
  { label: '2 Day', value: 'FEDEX_2_DAY' },
  { label: '2 Day AM', value: 'FEDEX_2_DAY_AM' },
  { label: 'Standard Overnight', value: 'STANDARD_OVERNIGHT' },
  { label: 'Priority Overnight', value: 'PRIORITY_OVERNIGHT' },
  { label: 'First Overnight', value: 'FIRST_OVERNIGHT' },
  { label: 'Ground Economy (Smart Post)', value: 'SMART_POST' },
  { label: 'International Economy', value: 'INTERNATIONAL_ECONOMY' },
  { label: 'International Priority', value: 'INTERNATIONAL_PRIORITY' },
  { label: 'International Priority (FEDEX_INTERNATIONAL_PRIORITY)', value: 'FEDEX_INTERNATIONAL_PRIORITY' },
  { label: 'International Priority Express', value: 'FEDEX_INTERNATIONAL_PRIORITY_EXPRESS' },
  { label: 'International First', value: 'INTERNATIONAL_FIRST' },
  { label: 'International Connect Plus', value: 'FEDEX_INTERNATIONAL_CONNECT_PLUS' },
  { label: 'Europe First International Priority', value: 'EUROPE_FIRST_INTERNATIONAL_PRIORITY' },
  { label: '1 Day Freight', value: 'FEDEX_1_DAY_FREIGHT' },
  { label: '2 Day Freight', value: 'FEDEX_2_DAY_FREIGHT' },
  { label: '3 Day Freight', value: 'FEDEX_3_DAY_FREIGHT' },
  { label: 'First Overnight Freight', value: 'FIRST_OVERNIGHT_FREIGHT' },
  { label: 'International Economy Freight', value: 'INTERNATIONAL_ECONOMY_FREIGHT' },
  { label: 'International Priority Freight', value: 'INTERNATIONAL_PRIORITY_FREIGHT' },
  { label: 'Same Day', value: 'SAME_DAY' },
  { label: 'Same Day City', value: 'SAME_DAY_CITY' }
]

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
    description: 'Authorize.Net card gateway (Accept.js). Add one row per environment (sandbox and production) — each keeps its own keys and settings.',
    // Test button: authenticateTestRequest with the saved row (AN-41).
    testable: true,
    testLabel: 'Test connection',
    drawerWidth: 820,
    fields: [
      {
        key: 'baseUrl',
        label: 'Endpoint URL',
        required: true,
        placeholder: 'https://apitest.authorize.net/xml/v1/request.api',
        hints: [
          'Sandbox: https://apitest.authorize.net/xml/v1/request.api',
          'Production: https://api.authorize.net/xml/v1/request.api',
          'Set Environment to match: it also picks the sandbox or live Accept.js library on the storefront.'
        ]
      },
      { key: 'applicationLoginId', label: 'API Login ID', required: true, col: 'half' },
      {
        key: 'publicClientKey',
        label: 'Public Client Key',
        required: true,
        col: 'half',
        hints: ['Not a secret: Accept.js uses it in the browser. Merchant Interface → Account → Settings → Manage Public Client Key.']
      },
      // Write-only (AN-40): never sent back to the browser; shown masked; blank keeps the stored key.
      { key: 'transactionKey', label: 'Transaction Key', secret: true, writeOnly: true, required: true, col: 'half' },
      {
        key: 'signatureKey',
        label: 'Signature Key',
        secret: true,
        writeOnly: true,
        col: 'half',
        hints: ['Only needed to verify webhooks.']
      },
      { key: 'displayName', label: 'Display name', col: 'half', placeholder: 'Credit Card', prefill: 'Credit Card', hints: ['What the buyer sees at checkout.'] },
      {
        key: 'transactionType',
        label: 'Transaction type',
        type: 'select',
        col: 'half',
        prefill: 'AuthCapture',
        options: [
          { label: 'Authorize and capture', value: 'AuthCapture' },
          { label: 'Authorize only (capture later with "Charge")', value: 'AuthOnly' }
        ],
        hints: ['Authorize only holds the funds; authorizations expire after 30 days.']
      },
      { key: 'minOrderAmount', label: 'Minimum order amount (USD)', col: 'half', type: 'number', min: 0, placeholder: 'No minimum' },
      { key: 'maxOrderAmount', label: 'Maximum order amount (USD)', col: 'half', type: 'number', min: 0, placeholder: 'No maximum' },
      {
        key: 'acceptedCardTypes',
        label: 'Accepted card types',
        type: 'multiselect',
        options: [
          { label: 'Visa', value: 'Visa' },
          { label: 'Mastercard', value: 'MasterCard' },
          { label: 'American Express', value: 'AmericanExpress' },
          { label: 'Discover', value: 'Discover' },
          { label: 'JCB', value: 'JCB' },
          { label: 'Diners Club', value: 'DinersClub' }
        ],
        hints: ['Leave empty to accept every card. A card of another brand is voided and the buyer asked for a different card.']
      },
      {
        key: 'allowedCustomerGroupIds',
        label: 'Allowed customer groups',
        type: 'multiselect',
        optionsSource: 'customerGroups',
        hints: ['Leave empty for all customers (guests included).']
      },
      {
        key: 'defaultOrderStatus',
        label: 'Default order status',
        type: 'select',
        col: 'half',
        prefill: 'Pending',
        options: [
          { label: 'Pending', value: 'Pending' },
          { label: 'Processing', value: 'Processing' }
        ],
        hints: ['Status of an order whose payment was approved. Orders held for fraud review always go to Payment Review.']
      },
      { ...TRANSACTION_FEE_FIELD, col: 'half' },
      { key: 'customerProfilesEnabled', label: 'Save customer profiles (CIM) for signed-in customers', type: 'toggle', hints: ['Stores the Authorize.Net customer and payment profile IDs on the payment.'] },
      { key: 'loggingEnabled', label: 'Log gateway calls', type: 'toggle', hints: ['Call type, amount, result codes, transaction ID and duration. The card nonce and keys are never logged.'] }
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
    description: 'FedEx live rates (REST Rate API, OAuth2 client credentials).',
    // Test button: rates a 5 lb residential parcel 28270 → 10001 and shows the token status (FX-40).
    testable: true,
    drawerWidth: 820,
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://apis-sandbox.fedex.com',
        hints: ['Sandbox: https://apis-sandbox.fedex.com', 'Live: https://apis.fedex.com', 'Create a project at developer.fedex.com with the Rate API. The old SOAP key / password / meter number do not work.']
      },
      { key: 'apiKey', label: 'Client ID (API key)', required: true, col: 'half' },
      { key: 'apiSecret', label: 'Client Secret', secret: true, required: true, col: 'half' },
      {
        key: 'accountNumber',
        label: 'Account Number',
        required: true,
        placeholder: 'FedEx account number',
        col: 'half',
        hints: ['The key and secret only authenticate you — FedEx rejects a rate request that names no account with a 403.']
      },
      { key: 'displayName', label: 'Display name', placeholder: 'FedEx', prefill: 'FedEx', col: 'half', hints: ['Options show as "<display name> - <service>".'] },
      {
        key: 'rateMode',
        label: 'Rate mode',
        type: 'select',
        prefill: 'ACCOUNT',
        col: 'half',
        options: [
          { label: 'Account (discounted)', value: 'ACCOUNT' },
          { label: 'List', value: 'LIST' }
        ]
      },
      {
        key: 'pickupType',
        label: 'Pickup type',
        type: 'select',
        prefill: 'USE_SCHEDULED_PICKUP',
        col: 'half',
        options: [
          { label: 'Scheduled pickup', value: 'USE_SCHEDULED_PICKUP' },
          { label: 'Drop off at FedEx location', value: 'DROPOFF_AT_FEDEX_LOCATION' }
        ],
        hints: ['It changes the rate.']
      },
      {
        key: 'allowedServices',
        label: 'Allowed services',
        type: 'multiselect',
        requiredWhenActive: true,
        prefill: ['FEDEX_GROUND', 'GROUND_HOME_DELIVERY', 'FEDEX_EXPRESS_SAVER', 'FEDEX_2_DAY', 'STANDARD_OVERNIGHT', 'PRIORITY_OVERNIGHT'],
        options: FEDEX_SERVICE_OPTIONS,
        hints: ['At least one is required while active. Freight, Same Day, Europe-only and international services are off by default.']
      },
      {
        key: 'fflAllowedServices',
        label: 'FFL allowed services',
        type: 'multiselect',
        options: FEDEX_SERVICE_OPTIONS,
        hints: ['Optional. When set, a cart holding FFL items is offered only these (e.g. the overnight services). Empty = no extra filter. An FFL cart is always rated as non-residential (dealer).']
      },
      {
        key: 'shipToCountries',
        label: 'Ship-to countries',
        type: 'tags',
        prefill: ['US'],
        col: 'half',
        placeholder: 'ISO code (e.g. US), Enter',
        hints: ['Leave empty for all.']
      },
      { key: 'handlingFee', label: 'Handling fee (USD)', type: 'number', min: 0, placeholder: '0.00', col: 'half', hints: ['Added to every FedEx option at checkout.'] },
      { key: 'minimumOrderAmount', label: 'Minimum order amount (USD)', type: 'number', min: 0, placeholder: 'No minimum', col: 'half' },
      { key: 'defaultLengthIn', label: 'Default package length (in)', type: 'number', min: 0, placeholder: 'e.g. 11', col: 'third' },
      { key: 'defaultWidthIn', label: 'Default package width (in)', type: 'number', min: 0, placeholder: 'e.g. 11', col: 'third' },
      { key: 'defaultHeightIn', label: 'Default package height (in)', type: 'number', min: 0, placeholder: 'e.g. 11', col: 'third', hints: ['Used when a product has no dimensions. Blank: no dimensions sent.'] },
      { key: 'sendPackageInfo', label: 'Send package dimensions (one package per unit)', type: 'toggle', col: 'half', hints: ['Uses each product\'s weight and dimensions, so dimensional weight is right.'] },
      { key: 'loggingEnabled', label: 'Log FedEx requests and responses', type: 'toggle', col: 'half', hints: ['Tokens and secrets are never logged.'] }
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
    description: 'USPS domestic + international rates (USPS REST APIs, OAuth2). Also prices the Custom method\'s "International Shipping".',
    // Test button: rates a 2 lb parcel 28270 → 10001 and → Canada with the saved row (US-40).
    testable: true,
    // Many settings: open wider than the default 460px drawer (remembered separately from other forms).
    drawerWidth: 820,
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://apis-tem.usps.com',
        hints: ['Test (TEM): https://apis-tem.usps.com', 'Live: https://apis.usps.com', 'HTTPS only. The old Web Tools USERID does not work — create an app on developers.usps.com.']
      },
      { key: 'consumerKey', label: 'Consumer Key', required: true, col: 'half' },
      { key: 'consumerSecret', label: 'Consumer Secret', secret: true, required: true, col: 'half' },
      { key: 'displayName', label: 'Display name', col: 'half', placeholder: 'USPS', prefill: 'USPS', hints: ['Options show as "<display name> - <service>".'] },
      {
        key: 'priceType',
        col: 'half',
        label: 'Price type',
        type: 'select',
        prefill: 'RETAIL',
        options: [
          { label: 'Retail', value: 'RETAIL' },
          { label: 'Commercial', value: 'COMMERCIAL' }
        ],
        hints: ['Commercial needs a USPS business account.']
      },
      {
        key: 'allowedDomesticServices',
        label: 'Allowed domestic services',
        type: 'multiselect',
        requiredWhenActive: true,
        prefill: ['USPS_GROUND_ADVANTAGE', 'PRIORITY_MAIL', 'PRIORITY_MAIL_EXPRESS'],
        options: [
          { label: 'Ground Advantage', value: 'USPS_GROUND_ADVANTAGE' },
          { label: 'Priority Mail', value: 'PRIORITY_MAIL' },
          { label: 'Priority Mail Express', value: 'PRIORITY_MAIL_EXPRESS' },
          { label: 'Parcel Select', value: 'PARCEL_SELECT' },
          { label: 'Media Mail', value: 'MEDIA_MAIL' },
          { label: 'Library Mail', value: 'LIBRARY_MAIL' },
          { label: 'Bound Printed Matter', value: 'BOUND_PRINTED_MATTER' }
        ],
        hints: ['At least one is required while active. First-Class Package / Parcel Select Ground are now Ground Advantage.']
      },
      {
        key: 'allowedInternationalServices',
        label: 'Allowed international services',
        type: 'multiselect',
        prefill: ['PRIORITY_MAIL_INTERNATIONAL', 'PRIORITY_MAIL_EXPRESS_INTERNATIONAL', 'FIRST-CLASS_PACKAGE_INTERNATIONAL_SERVICE'],
        options: [
          { label: 'Priority Mail International', value: 'PRIORITY_MAIL_INTERNATIONAL' },
          { label: 'Priority Mail Express International', value: 'PRIORITY_MAIL_EXPRESS_INTERNATIONAL' },
          { label: 'First-Class Package International', value: 'FIRST-CLASS_PACKAGE_INTERNATIONAL_SERVICE' }
        ],
        hints: ['The Custom method charges the cheapest of these for non-US orders.']
      },
      { key: 'handlingFee', label: 'Handling fee (USD)', col: 'half', type: 'number', min: 0, placeholder: '0.00', hints: ['Added to every USPS option at checkout.'] },
      { key: 'minimumOrderAmount', label: 'Minimum order amount (USD)', col: 'half', type: 'number', min: 0, placeholder: 'No minimum' },
      {
        key: 'shipToCountries',
        label: 'Ship-to countries',
        type: 'tags',
        prefill: ['US'],
        placeholder: 'Type an ISO code (e.g. US) and press Enter',
        hints: ['Countries the USPS method is offered for. Leave empty for all. Keep it to US to leave international to the Custom method.']
      },
      { key: 'defaultLengthIn', label: 'Default package length (in)', col: 'third', type: 'number', min: 0, placeholder: '12' },
      { key: 'defaultWidthIn', label: 'Default package width (in)', col: 'third', type: 'number', min: 0, placeholder: '9' },
      { key: 'defaultHeightIn', label: 'Default package height (in)', col: 'third', type: 'number', min: 0, placeholder: '6', hints: ['Used when the cart has no dimensions. Blank: 12 × 9 × 6 domestic, 15 × 15 × 15 international.'] },
      { key: 'loggingEnabled', label: 'Log USPS requests and responses', type: 'toggle', hints: ['Tokens and secrets are never logged.'] }
    ]
  },
  {
    key: 'ups', kind: 'credential', label: 'UPS', category: 'Shipping', icon: 'o_local_shipping',
    description: 'UPS live rates (Rating API, OAuth2 client credentials with HK Parts\' own UPS app).',
    // Test button: rates a 5 lb residential parcel 28270 → 10001 and shows the token status (UP-40/41).
    testable: true,
    drawerWidth: 820,
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://wwwcie.ups.com',
        hints: ['Test (CIE): https://wwwcie.ups.com', 'Live: https://onlinetools.ups.com', 'Create the app at developer.ups.com with the OAuth (Client Credentials) and Rating products.']
      },
      { key: 'clientId', label: 'Client ID', required: true, col: 'half' },
      { key: 'clientSecret', label: 'Client Secret', secret: true, required: true, col: 'half' },
      { key: 'merchantId', label: 'Shipper (account) number', placeholder: 'UPS account / shipper number', col: 'half', hints: ['Sent as ShipperNumber and the bill-shipper account. Required for negotiated rates.'] },
      { key: 'apiVersion', label: 'Rating API version', placeholder: 'v2409', prefill: 'v2409', col: 'half', hints: ['Confirm the current version in the UPS API catalog.'] },
      { key: 'displayName', label: 'Display name', placeholder: 'UPS', prefill: 'UPS', col: 'half', hints: ['Options show as "<display name> - <service>".'] },
      {
        key: 'packageType',
        label: 'Default package type',
        type: 'select',
        prefill: '02',
        col: 'half',
        options: [
          { label: '02 — Customer supplied package', value: '02' },
          { label: '01 — UPS Letter', value: '01' },
          { label: '03 — Tube', value: '03' },
          { label: '04 — PAK', value: '04' },
          { label: '21 — UPS Express Box', value: '21' },
          { label: '24 — UPS 25KG Box', value: '24' },
          { label: '25 — UPS 10KG Box', value: '25' }
        ]
      },
      {
        key: 'allowedServices',
        label: 'Allowed services',
        type: 'multiselect',
        requiredWhenActive: true,
        prefill: ['03', '12', '02', '13', '01'],
        options: UPS_SERVICE_OPTIONS,
        hints: ['At least one is required while active. International services are off by default — firearms parts often cannot ship abroad.']
      },
      {
        key: 'fflAllowedServices',
        label: 'FFL allowed services',
        type: 'multiselect',
        prefill: ['01', '13', '14'],
        options: UPS_SERVICE_OPTIONS,
        hints: ['A cart holding FFL items is offered only these (and only if also allowed above). It ships to the dealer, so it is rated as non-residential.']
      },
      {
        key: 'pickupType',
        label: 'Pickup type',
        type: 'select',
        prefill: '01',
        col: 'half',
        options: [
          { label: '01 — Daily Pickup', value: '01' },
          { label: '03 — Customer Counter', value: '03' },
          { label: '06 — One Time Pickup', value: '06' },
          { label: '07 — On Call Air', value: '07' },
          { label: '19 — Letter Center', value: '19' },
          { label: '20 — Air Service Center', value: '20' }
        ]
      },
      {
        key: 'shipToCountries',
        label: 'Ship-to countries',
        type: 'tags',
        prefill: ['US'],
        col: 'half',
        placeholder: 'ISO code (e.g. US), Enter',
        hints: ['Leave empty for all.']
      },
      { key: 'handlingFee', label: 'Handling fee (USD)', type: 'number', min: 0, placeholder: '0.00', col: 'half', hints: ['Added to every UPS option at checkout.'] },
      { key: 'minimumOrderAmount', label: 'Minimum order amount (USD)', type: 'number', min: 0, placeholder: 'No minimum', col: 'half' },
      { key: 'useNegotiatedRates', label: 'Use negotiated rates', type: 'toggle', col: 'third' },
      { key: 'sendPackageInfo', label: 'Send package info (one package per unit)', type: 'toggle', col: 'third', hints: ['Uses each product\'s weight and dimensions.'] },
      { key: 'loggingEnabled', label: 'Log UPS requests and responses', type: 'toggle', col: 'third', hints: ['Tokens and secrets are never logged.'] }
    ]
  },
  {
    key: 'freightview', kind: 'credential', label: 'Freightview (LTL)', category: 'Shipping', icon: 'o_rv_hookup',
    description: 'LTL freight quotes for heavy / palletised items.',
    fields: [
      {
        key: 'baseUrl',
        label: 'Base URL',
        required: true,
        placeholder: 'https://www.freightview.com/api/v1.0',
        hints: ['Freightview API host including the version path.']
      },
      { key: 'apiKey', label: 'API Key', secret: true, required: true },
      {
        key: 'originType',
        label: 'Origin Type',
        placeholder: 'business dock',
        hints: ['e.g. business dock, business no dock, residential.']
      },
      { key: 'contactName', label: 'Contact Name' },
      { key: 'contactPhone', label: 'Contact Phone' },
      { key: 'contactEmail', label: 'Contact Email' },
      { key: 'pickupInstructions', label: 'Pickup Instructions' },
      { key: 'dockOpenTime', label: 'Dock Opens', placeholder: '8:00 AM' },
      { key: 'dockCloseTime', label: 'Dock Closes', placeholder: '5:00 PM' },
      // Common method settings (AD-04), as on USPS / UPS / FedEx.
      { key: 'displayName', label: 'Display name', placeholder: 'e.g. Freight', col: 'half', hints: ['Prefixed to every Freightview option at checkout. Blank = carrier and service only.'] },
      {
        key: 'shipToCountries',
        label: 'Ship-to countries',
        type: 'tags',
        col: 'half',
        placeholder: 'ISO code (e.g. US), Enter',
        hints: ['Leave empty for all.']
      },
      { key: 'handlingFee', label: 'Handling fee (USD)', type: 'number', min: 0, placeholder: '0.00', col: 'half', hints: ['Added to every Freightview option at checkout.'] },
      { key: 'minimumOrderAmount', label: 'Minimum order amount (USD)', type: 'number', min: 0, placeholder: 'No minimum', col: 'half' }
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
  freightview: { pricing: 'paid', website: 'https://www.freightview.com' },
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
