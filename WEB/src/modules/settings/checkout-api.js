/*
 * Checkout settings + customer blacklist admin API
 * (docs/checkout-shipping-payment-settings-requirements.md, section 7).
 *
 *   GET/PUT /api/admin/checkout/settings      -> CheckoutSettingsDto
 *   GET     /api/admin/blacklist              -> PaginatedList<CustomerBlacklistEntryDto>
 *   POST    /api/admin/blacklist              -> CustomerBlacklistEntryDto
 *   PUT     /api/admin/blacklist/:id          -> CustomerBlacklistEntryDto
 *   DELETE  /api/admin/blacklist/:id
 *   GET     /api/admin/blacklist/hits         -> PaginatedList<CustomerBlacklistHitDto>
 */
import { api, unwrap } from 'services/api'

export const checkoutSettingsApi = {
  get () {
    return api.get('/api/admin/checkout/settings').then(unwrap)
  },
  update (payload) {
    return api.put('/api/admin/checkout/settings', payload).then(unwrap)
  }
}

export const blacklistApi = {
  list (params) {
    return api.get('/api/admin/blacklist', { params }).then(unwrap)
  },
  create (payload) {
    return api.post('/api/admin/blacklist', payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`/api/admin/blacklist/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`/api/admin/blacklist/${id}`).then(unwrap)
  },
  hits (params) {
    return api.get('/api/admin/blacklist/hits', { params }).then(unwrap)
  },
  // Block / unblock history: one row per person with times blocked / unblocked. params: { search, page, pageSize }.
  history (params) {
    return api.get('/api/admin/blacklist/history', { params }).then(unwrap)
  },
  // Every block / unblock of one person (subjectKey from a history row), newest first.
  historyEvents (subjectKey) {
    return api.get('/api/admin/blacklist/history/events', { params: { subjectKey } }).then(unwrap)
  }
}

export const blacklistMatchModeOptions = [
  { label: 'Email only', value: 'EmailOnly', description: 'Never blocks a different person who shares a name.' },
  { label: 'Name and email', value: 'NameAndEmail', description: 'Both the billing name and the email must match.' },
  { label: 'Name or email', value: 'NameOrEmail', description: 'Either matches. This is how the old site worked.' }
]

/*
 * Guest checkout settings (docs/guest-checkout-requirements.md §4.1, A-01..A-05).
 *   GET/PUT /api/admin/checkout/guest-settings -> GuestCheckoutSettingsDto
 */
export const guestCheckoutSettingsApi = {
  get () {
    return api.get('/api/admin/checkout/guest-settings').then(unwrap)
  },
  update (payload) {
    return api.put('/api/admin/checkout/guest-settings', payload).then(unwrap)
  }
}

// The payment methods an admin can open to guests (A-03). Gift certificates and zero-total orders are always allowed.
export const guestPaymentMethodOptions = [
  { label: 'Credit card (Authorize.Net)', value: 'AuthorizeNet' },
  { label: 'Stripe', value: 'Stripe' },
  { label: 'PayPal', value: 'PayPal' },
  { label: 'Square', value: 'Square' },
  { label: 'Razorpay', value: 'Razorpay' },
  { label: 'Cash on delivery', value: 'CashOnDelivery' },
  { label: 'Bank transfer', value: 'BankTransfer' }
]
