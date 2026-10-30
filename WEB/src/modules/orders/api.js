/*
 * Orders admin module API layer (WO-114).
 *
 * Wraps AdminOrdersController, AdminOrderLifecycleController, AdminShipmentsController,
 * AdminOrderDocumentsController and AdminRmaController. Uses the authenticated `api`
 * instance. Enums transport as their PascalCase string names.
 */
import { api, unwrap, qsSerializer } from 'services/api'
import { formatDateTime } from 'src/utils/datetime'

const ORDERS = '/api/admin/orders'
const RMA = '/api/admin/rma'

export const orderApi = {
  list (params = {}) {
    return api.get(ORDERS, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  get (id) {
    return api.get(`${ORDERS}/${id}`).then(unwrap)
  },
  history (id) {
    return api.get(`${ORDERS}/${id}/history`).then(unwrap)
  },
  payments (id) {
    return api.get(`${ORDERS}/${id}/payments`).then(unwrap)
  },
  advance (id, payload) {
    return api.put(`${ORDERS}/${id}/advance`, payload).then(unwrap)
  },
  reroute (id, targetStoreId = null) {
    return api.put(`${ORDERS}/${id}/reroute`, { targetStoreId }).then(unwrap)
  },
  // Shipments
  shipments (id) {
    return api.get(`${ORDERS}/${id}/shipments`).then(unwrap)
  },
  createShipment (id, payload) {
    return api.post(`${ORDERS}/${id}/shipments`, payload).then(unwrap)
  },
  generateLabel (shipmentId) {
    return api.post(`${ORDERS}/shipments/${shipmentId}/label`).then(unwrap)
  },
  tracking (id) {
    return api.get(`${ORDERS}/${id}/tracking`).then(unwrap)
  },
  // Refund
  refund (id, payload) {
    return api.post(`${ORDERS}/${id}/refund`, payload).then(unwrap)
  },
  // PDF documents (blob download)
  invoice (id) {
    return downloadPdf(`${ORDERS}/${id}/invoice`, `invoice-${id}.pdf`)
  },
  packingSlip (id) {
    return downloadPdf(`${ORDERS}/${id}/packing-slip`, `packing-slip-${id}.pdf`)
  }
}

export const rmaApi = {
  list (params = {}) {
    return api.get(RMA, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  get (id) {
    return api.get(`${RMA}/${id}`).then(unwrap)
  },
  resolve (id, payload) {
    return api.put(`${RMA}/${id}`, payload).then(unwrap)
  }
}

// Fetch a PDF as a blob and trigger a browser download.
async function downloadPdf (url, filename) {
  const res = await api.get(url, { responseType: 'blob' })
  const blob = new Blob([res.data], { type: 'application/pdf' })
  const href = window.URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = href
  link.download = filename
  document.body.appendChild(link)
  link.click()
  link.remove()
  window.URL.revokeObjectURL(href)
}

// ---- Display helpers --------------------------------------------------------
export const orderStatusColor = (status) => {
  const s = (status || '').toLowerCase()
  if (s.includes('cancel') || s.includes('reject') || s.includes('unrouted')) return 'negative'
  if (s.includes('deliver') || s.includes('complete')) return 'positive'
  if (s.includes('ship')) return 'teal'
  if (s.includes('process') || s.includes('prepar') || s.includes('pending') || s.includes('routing')) return 'orange'
  if (s.includes('readyforpickup')) return 'purple'
  return 'grey'
}

// Lifecycle transitions the advance endpoint accepts, by current status.
export const allowedTransitions = (status) => {
  switch (status) {
    case 'Pending': return ['Processing', 'Cancelled']
    case 'Processing': return ['Shipped', 'Cancelled']
    case 'Shipped': return ['Delivered', 'Cancelled']
    default: return []
  }
}

// Payment record status → badge colour (PaymentStatus enum, PascalCase names).
export const paymentStatusColor = (status) => {
  const s = (status || '').toLowerCase()
  if (s === 'failed' || s === 'voided') return 'negative'
  if (s === 'captured') return 'positive'
  if (s === 'refunded' || s === 'partiallyrefunded') return 'deep-orange'
  if (s === 'authorized') return 'teal'
  if (s === 'pending' || s === 'awaitingpayment') return 'orange'
  return 'grey'
}

// Payment method enum name → human label.
const PAYMENT_METHOD_LABELS = {
  Stripe: 'Stripe',
  PayPal: 'PayPal',
  Razorpay: 'Razorpay',
  Square: 'Square',
  AuthorizeNet: 'Authorize.Net',
  CashOnDelivery: 'Cash on Delivery',
  BankTransfer: 'Bank Transfer'
}
export const paymentMethodLabel = (method) => PAYMENT_METHOD_LABELS[method] || method || '—'

// Split a PascalCase enum name (e.g. PartiallyRefunded) into words for display.
export const humanizeEnum = (value) => (value || '').replace(/([a-z])([A-Z])/g, '$1 $2')

export const rmaStatusColor = (status) => {
  const s = (status || '').toLowerCase()
  if (s === 'rejected' || s === 'cancelled') return 'negative'
  if (s === 'completed' || s === 'approved') return 'positive'
  return 'orange'
}

export const rmaResolutionOptions = [
  { label: 'Refund', value: 'Refund' },
  { label: 'Replacement', value: 'Replacement' },
  { label: 'Store Credit', value: 'StoreCredit' }
]

export function formatMoney (value) {
  if (value === null || value === undefined) return '—'
  return Number(value).toFixed(2)
}

// App-wide standard: MM/DD/YYYY hh:mm AM/PM (UTC-aware). All these fields are timestamps, so the
// legacy `withTime` flag is ignored — every value renders with the time. (See src/utils/datetime.js.)
export function formatDate (value) {
  return formatDateTime(value)
}

export default { orderApi, rmaApi, orderStatusColor, allowedTransitions, paymentStatusColor, paymentMethodLabel, humanizeEnum, rmaStatusColor, rmaResolutionOptions, formatMoney, formatDate }
