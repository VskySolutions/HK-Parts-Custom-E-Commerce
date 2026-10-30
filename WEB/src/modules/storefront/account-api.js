/*
 * Storefront customer account API layer (WO-21).
 *
 * - customerAuthApi: PUBLIC auth endpoints (register/verify/login/reset) on the
 *   ANONYMOUS instance, plus the shared refresh endpoint.
 * - accountApi: the AUTHENTICATED customer endpoints (profile, addresses, orders)
 *   on the isolated storefront customer instance (`customerApi`), which carries the
 *   customer bearer token from a LocalStorage slot separate from the admin session.
 *
 * JSON is camelCase; enums serialize as string names (AddressType = Shipping|Billing).
 */
import { customerApi, anonApi, unwrap, qsSerializer } from 'services/api'

const AUTH = '/api/customer/auth'
const PROFILE = '/api/customer/profile'
const ADDRESSES = '/api/customer/addresses'
const ORDERS = '/api/customer/orders'
const INQUIRIES = '/api/customer/inquiries'
const TAX_EXEMPTION = '/api/customer/tax-exemption'
const GDPR = '/api/customer/gdpr'
const SUBSCRIPTIONS = '/api/customer/subscriptions'
const POINTS = '/api/customer/points'

export const customerAuthApi = {
  // Create an account; a verification email is sent. Returns the new customer id.
  register (payload) {
    return anonApi.post(AUTH + '/register', payload).then(unwrap)
  },
  // Express registration from checkout: creates a verified account from the delivery details, emails a
  // generated password, and saves the address. Returns { email }. Lets a guest sign in and finish the
  // order without re-entering anything.
  registerAtCheckout (payload) {
    return anonApi.post(AUTH + '/register-at-checkout', payload).then(unwrap)
  },
  // Confirm an email address with the token from the verification link.
  verifyEmail (token) {
    return anonApi.post(AUTH + '/verify-email', { token }).then(unwrap)
  },
  // Authenticate; returns { accessToken, expiresAtUtc, refreshToken, user }.
  login (payload) {
    return anonApi.post(AUTH + '/login', payload).then(unwrap)
  },
  // Start the reset flow (always succeeds, even for unknown emails).
  requestPasswordReset (payload) {
    return anonApi.post(AUTH + '/request-password-reset', payload).then(unwrap)
  },
  // Set a new password using a valid reset token.
  resetPassword (token, newPassword) {
    return anonApi.post(AUTH + '/reset-password', { token, newPassword }).then(unwrap)
  },
  // Silent token refresh (the refresh endpoint is shared with the admin app).
  refresh (refreshToken) {
    return anonApi.post('/api/auth/refresh', { refreshToken }).then(unwrap)
  }
}

export const accountApi = {
  // ---- Profile ----
  getProfile () {
    return customerApi.get(PROFILE).then(unwrap)
  },
  updateProfile ({ firstName, lastName, phoneNumber, preferredTimeZone }) {
    return customerApi.put(PROFILE, { firstName, lastName, phoneNumber, preferredTimeZone }).then(unwrap)
  },
  changeEmail (newEmail) {
    return customerApi.put(PROFILE + '/email', { newEmail }).then(unwrap)
  },
  changePassword ({ currentPassword, newPassword }) {
    return customerApi.put(PROFILE + '/password', { currentPassword, newPassword }).then(unwrap)
  },

  // ---- Address book ----
  // Grouped by type ({ shipping:[], billing:[] }) for the address-book UI.
  addressBook () {
    return customerApi.get(ADDRESSES, { params: { grouped: true }, paramsSerializer: qsSerializer }).then(unwrap)
  },
  createAddress (payload) {
    return customerApi.post(ADDRESSES, payload).then(unwrap)
  },
  updateAddress (id, payload) {
    return customerApi.put(ADDRESSES + '/' + encodeURIComponent(id), payload).then(unwrap)
  },
  // Make a saved address the default for its type — needs only the id (no full-address re-validation).
  setDefaultAddress (id) {
    return customerApi.put(ADDRESSES + '/' + encodeURIComponent(id) + '/default').then(unwrap)
  },
  removeAddress (id) {
    return customerApi.delete(ADDRESSES + '/' + encodeURIComponent(id)).then(unwrap)
  },

  // ---- Inquiries (quote requests, REQ-INQ-001) ----
  inquiries (params = {}) {
    return customerApi.get(INQUIRIES, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },

  // ---- Order history ----
  orders (params = {}) {
    return customerApi.get(ORDERS, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  // Full order detail (line items + ship-to) for the current customer.
  getOrder (id) {
    return customerApi.get(ORDERS + '/' + encodeURIComponent(id)).then(unwrap)
  },
  // The order's status timeline (oldest transition first) for the current customer.
  orderTimeline (id) {
    return customerApi.get(ORDERS + '/' + encodeURIComponent(id) + '/timeline').then(unwrap)
  },
  // Download the order's invoice PDF (blob → browser download).
  downloadInvoice (id) {
    return downloadPdf(ORDERS + '/' + encodeURIComponent(id) + '/invoice', `invoice-${id}.pdf`)
  },

  // ---- Tax exemption (WO-126) ----
  // Current status + latest request: { status, isTaxExempt, canSubmit, latestRequest }.
  taxExemption () {
    return customerApi.get(TAX_EXEMPTION).then(unwrap)
  },
  // Upload one supporting document (multipart). Returns { mediaId, fileName, url }.
  uploadTaxDocument (file) {
    const form = new FormData()
    form.append('file', file)
    return customerApi.post(TAX_EXEMPTION + '/documents', form, {
      headers: { 'Content-Type': 'multipart/form-data' }
    }).then(unwrap)
  },
  // Submit/re-submit a request: { certificateNumber, vatId, documentMediaIds:[] }.
  submitTaxExemption (payload) {
    return customerApi.post(TAX_EXEMPTION + '/request', payload).then(unwrap)
  },

  // ---- Data & privacy / GDPR (WO-23) ----
  // Request a full personal-data export (POST) and save it as a JSON file.
  async exportData () {
    const res = await customerApi.post(GDPR + '/export', null, { responseType: 'blob' })
    saveBlob(res.data, 'my-data.json', 'application/json')
  },
  // Irreversibly anonymise the account. The caller signs the customer out afterwards.
  deleteAccount () {
    return customerApi.post(GDPR + '/delete-account').then(unwrap)
  },

  // ---- Subscriptions (WO-49) ----
  // [{ id, productName, interval, status, nextOrderOnUtc, quantity }].
  subscriptions () {
    return customerApi.get(SUBSCRIPTIONS).then(unwrap)
  },
  pauseSubscription (id) {
    return customerApi.post(SUBSCRIPTIONS + '/' + encodeURIComponent(id) + '/pause').then(unwrap)
  },
  resumeSubscription (id) {
    return customerApi.post(SUBSCRIPTIONS + '/' + encodeURIComponent(id) + '/resume').then(unwrap)
  },
  // Change the delivery frequency: interval ∈ Weekly|BiWeekly|Monthly|Quarterly.
  changeSubscriptionInterval (id, interval) {
    return customerApi.put(SUBSCRIPTIONS + '/' + encodeURIComponent(id) + '/interval', { interval }).then(unwrap)
  },
  cancelSubscription (id) {
    return customerApi.post(SUBSCRIPTIONS + '/' + encodeURIComponent(id) + '/cancel').then(unwrap)
  },

  // ---- Loyalty points (WO-27) ----
  // { balance, transactions:[{ points, type, reason, createdOnUtc, balanceAfter }] }.
  points () {
    return customerApi.get(POINTS).then(unwrap)
  }
}

// Trigger a browser download from raw blob data (shared by the PDF + JSON exporters).
function saveBlob (data, filename, mime) {
  const blob = new Blob([data], { type: mime })
  const href = window.URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = href
  link.download = filename
  document.body.appendChild(link)
  link.click()
  link.remove()
  window.URL.revokeObjectURL(href)
}

// Fetch a PDF as a blob (on the authenticated customer instance) and trigger a browser download.
async function downloadPdf (url, filename) {
  const res = await customerApi.get(url, { responseType: 'blob' })
  saveBlob(res.data, filename, 'application/pdf')
}

export default { customerAuthApi, accountApi }
