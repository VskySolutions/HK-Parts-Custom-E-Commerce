/*
 * eBay channel API (AdminEbayController). Every call is scoped to the selected store, sent as ?storeId=
 * (empty = the API's primary store). Enums travel as names: status = Draft | Active | Ended | Error,
 * environment = Sandbox | Production, policy type = Payment | Fulfillment | Return.
 */
import { ref } from 'vue'
import { Notify } from 'quasar'
import { api, unwrap, qsSerializer, getApiErrorMessage } from 'services/api'

const BASE = '/api/admin/ebay'
const STORE_KEY = 'ebay.storeId'

function readStore () {
  try { return localStorage.getItem(STORE_KEY) || null } catch (e) { return null }
}

/** The store every eBay page acts on (remembered per browser). */
export const ebayStoreId = ref(readStore())

export function setEbayStore (id) {
  ebayStoreId.value = id || null
  try {
    if (id) localStorage.setItem(STORE_KEY, id)
    else localStorage.removeItem(STORE_KEY)
  } catch (e) { /* storage unavailable: keep it in memory */ }
}

function scoped (params = {}) {
  return { params: { ...params, storeId: ebayStoreId.value || undefined }, paramsSerializer: qsSerializer }
}

export const ebayApi = {
  // Settings / OAuth
  getSettings () { return api.get(`${BASE}/settings`, scoped()).then(unwrap) },
  saveSettings (payload) { return api.put(`${BASE}/settings`, payload, scoped()).then(unwrap) },
  connectUrl () { return api.get(`${BASE}/oauth/connect-url`, scoped()).then(unwrap) },
  disconnect () { return api.post(`${BASE}/oauth/disconnect`, null, scoped()).then(unwrap) },
  policies (type, refresh = false) { return api.get(`${BASE}/policies`, scoped({ type, refresh })).then(unwrap) },
  syncLocation () { return api.post(`${BASE}/location/sync`, null, scoped()).then(unwrap) },

  // Categories
  refreshCategories () { return api.post(`${BASE}/categories/refresh`, null, scoped()).then(unwrap) },
  categories (params = {}) { return api.get(`${BASE}/categories`, scoped(params)).then(unwrap) },
  aspects (categoryId) { return api.get(`${BASE}/categories/${encodeURIComponent(categoryId)}/aspects`, scoped()).then(unwrap) },
  conditions (categoryId) { return api.get(`${BASE}/categories/${encodeURIComponent(categoryId)}/conditions`, scoped()).then(unwrap) },
  compatibility (categoryId) { return api.get(`${BASE}/categories/${encodeURIComponent(categoryId)}/compatibility`, scoped()).then(unwrap) },

  // Listings
  eligibleProducts (search) { return api.get(`${BASE}/products/eligible`, scoped({ search: search || undefined })).then(unwrap) },
  listings (params = {}) { return api.get(`${BASE}/listings`, scoped(params)).then(unwrap) },
  createListing (payload) { return api.post(`${BASE}/listings`, payload, scoped()).then(unwrap) },
  getListing (id) { return api.get(`${BASE}/listings/${id}`, scoped()).then(unwrap) },
  saveListing (id, payload) { return api.put(`${BASE}/listings/${id}`, payload, scoped()).then(unwrap) },
  endListing (id) { return api.post(`${BASE}/listings/${id}/end`, null, scoped()).then(unwrap) },
  syncQuantities () { return api.post(`${BASE}/listings/sync-quantities`, null, scoped()).then(unwrap) }
}

export const STATUS_BADGE = {
  Draft: { color: 'orange', label: 'Inactive' },
  Active: { color: 'positive', label: 'Active' },
  Error: { color: 'negative', label: 'Error' },
  Ended: { color: 'grey', label: 'Ended' }
}

function escapeHtml (text) {
  return String(text ?? '')
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&#39;')
}

/** eBay's 422 body → [{ code, message, field }] (empty for any other failure). */
export function ebayErrors (err) {
  const data = err?.response?.data
  return err?.response?.status === 422 && Array.isArray(data?.errors) ? data.errors : []
}

/**
 * Shows eBay business errors as one notification: a header, then one bullet per error. Falls back to the
 * generic API message for non-422 failures. Returns the error list.
 */
export function notifyEbayErrors (err, header = 'ERROR - Your listing has returned errors:') {
  const errors = ebayErrors(err)
  if (!errors.length) {
    Notify.create({ type: 'negative', message: getApiErrorMessage(err), multiLine: true, timeout: 6000 })
    return errors
  }
  const bullets = errors
    .map((e) => `<li>${escapeHtml(e.message)}${e.code ? ` <span style="opacity:.7">(${escapeHtml(e.code)})</span>` : ''}</li>`)
    .join('')
  Notify.create({
    type: 'negative',
    html: true,
    multiLine: true,
    timeout: 0,
    message: `<div style="font-weight:600">${escapeHtml(header)}</div><ul style="margin:4px 0 0 18px;padding:0">${bullets}</ul>`,
    actions: [{ icon: 'mdi-close', color: 'white', round: true, dense: true }]
  })
  return errors
}

export function money (value) {
  return Number(value || 0).toLocaleString(undefined, { style: 'currency', currency: 'USD' })
}
