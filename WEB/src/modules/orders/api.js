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
  // Guest checkout A-22: fix a guest order's email; payload { email, resendConfirmation }. Returns the order.
  updateGuestEmail (id, payload) {
    return api.patch(`${ORDERS}/${id}/email`, payload).then(unwrap)
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
  // Payment actions (PaymentController, /api/payments). Capture = "Charge" an authorize-only payment
  // (amount null = the authorized amount). Sync re-reads the gateway, e.g. a payment held for review.
  capturePayment (paymentId, amount = null) {
    return api.post(`/api/payments/${paymentId}/capture`, { amount }).then(unwrap)
  },
  syncPayment (paymentId) {
    return api.post(`/api/payments/${paymentId}/sync`).then(unwrap)
  },
  // PDF documents (blob download)
  invoice (id, orderNumber) {
    return downloadPdf(`${ORDERS}/${id}/invoice`, `Invoice-${orderNumber || id}.pdf`)
  },
  // The invoice as data for the on-screen invoice page (same figures as the PDF).
  invoiceView (id) {
    return api.get(`${ORDERS}/${id}/invoice/view`).then(unwrap)
  },
  packingSlip (id) {
    return downloadPdf(`${ORDERS}/${id}/packing-slip`, `packing-slip-${id}.pdf`)
  }
}

/* Returns (RMA) workflow — AdminRmaController / AdminReturnSettingsController
 * (docs/order-return-requirements.bak.md). Every action returns the refreshed RMA. */
const RETURNS = '/api/admin/returns'
const rmaAction = (id, action, payload = {}) => api.post(`${RMA}/${id}/${action}`, payload).then(unwrap)

export const rmaApi = {
  list (params = {}) {
    return api.get(RMA, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  pendingCount () {
    return api.get(`${RMA}/pending-count`).then(unwrap)
  },
  get (id) {
    return api.get(`${RMA}/${id}`).then(unwrap)
  },
  returnable (orderId) {
    return api.get(`${ORDERS}/${orderId}/returnable`).then(unwrap)
  },
  create (payload) {
    return api.post(RMA, payload).then(unwrap)
  },
  /* One-screen return + credit (PHP "Apply credit"): create → receive/restock → inspect → refund. */
  quickReturnForm (orderId) {
    return api.get(`${ORDERS}/${orderId}/quick-return`).then(unwrap)
  },
  applyQuickReturn (orderId, payload) {
    return api.post(`${ORDERS}/${orderId}/quick-return`, payload).then(unwrap)
  },
  approve: (id, payload) => rmaAction(id, 'approve', payload),
  reject: (id, payload) => rmaAction(id, 'reject', payload),
  cancel: (id, payload) => rmaAction(id, 'cancel', payload),
  requestInfo: (id, payload) => rmaAction(id, 'request-info', payload),
  addMessage: (id, payload) => rmaAction(id, 'messages', payload),
  markInTransit: (id, payload) => rmaAction(id, 'in-transit', payload),
  receive: (id, payload) => rmaAction(id, 'receive', payload),
  inspect: (id, payload) => rmaAction(id, 'inspect', payload),
  refund: (id, payload) => rmaAction(id, 'refund', payload),
  exchange: (id, payload) => rmaAction(id, 'exchange', payload),
  close: (id, payload) => rmaAction(id, 'close', payload),
  reopen: (id, payload) => rmaAction(id, 'reopen', payload),
  setFfl (id, payload) {
    return api.put(`${RMA}/${id}/ffl`, payload).then(unwrap)
  },
  setLabel (id, payload) {
    return api.put(`${RMA}/${id}/label`, payload).then(unwrap)
  },
  updateLines (id, payload) {
    return api.put(`${RMA}/${id}/lines`, payload).then(unwrap)
  },
  uploadAttachment (id, file) {
    const form = new FormData()
    form.append('file', file)
    return api.post(`${RMA}/${id}/attachments`, form).then(unwrap)
  },
  // Private files are streamed through the API (auth header), then opened as a blob URL.
  async openAttachment (id, attachmentId) {
    const res = await api.get(`${RMA}/${id}/attachments/${attachmentId}`, { responseType: 'blob' })
    const url = URL.createObjectURL(res.data)
    window.open(url, '_blank', 'noopener')
    setTimeout(() => URL.revokeObjectURL(url), 60000)
  },
  getSettings () {
    return api.get(`${RETURNS}/settings`).then(unwrap)
  },
  saveSettings (payload) {
    return api.put(`${RETURNS}/settings`, payload).then(unwrap)
  },
  reasons () {
    return api.get(`${RETURNS}/reasons`).then(unwrap)
  },
  saveReason (payload) {
    return payload.id
      ? api.put(`${RETURNS}/reasons/${payload.id}`, payload).then(unwrap)
      : api.post(`${RETURNS}/reasons`, payload).then(unwrap)
  },
  deleteReason (id) {
    return api.delete(`${RETURNS}/reasons/${id}`)
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
  if (s === 'paymentreview') return 'amber-9'
  return 'grey'
}

// Lifecycle transitions the advance endpoint accepts, by current status.
export const allowedTransitions = (status) => {
  switch (status) {
    case 'Pending': return ['Processing', 'Cancelled']
    case 'Processing': return ['Shipped', 'Cancelled']
    case 'Shipped': return ['Delivered', 'Cancelled']
    // Released by "Check gateway status" on the payment, never by hand.
    case 'PaymentReview': return ['Cancelled']
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
  if (s === 'heldforreview') return 'amber-9'
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

/* Carriers a shipment can go out with. Values must match the backend adapters' CarrierName
 * (ShippingLabelService / TrackingSyncWorker look the adapter up by this string). */
export const shipmentCarrierOptions = [
  { label: 'UPS', value: 'UPS' },
  { label: 'USPS', value: 'USPS' },
  { label: 'FedEx', value: 'FedEx' },
  { label: 'DHL Express', value: 'DHL Express' },
  { label: 'Freightview (LTL)', value: 'Freightview' }
]

/* The order's placed carrier when it is one of the options (case-insensitive), else ''. */
export function defaultShipmentCarrier (order) {
  const placed = order?.shippingCarrier || order?.shippings?.[0]?.carrier || ''
  return shipmentCarrierOptions.find((o) => o.value.toLowerCase() === placed.toLowerCase())?.value || ''
}

export const rmaStatusColor = (status) => {
  switch (status) {
    case 'Requested': return 'orange'
    case 'Approved': case 'InTransit': return 'blue'
    case 'Received': case 'Inspected': return 'purple'
    case 'Refunded': case 'Exchanged': case 'Closed': return 'positive'
    case 'Rejected': case 'Cancelled': case 'Expired': return 'negative'
    default: return 'grey'
  }
}

export const rmaStatusOptions = ['Requested', 'Approved', 'InTransit', 'Received', 'Inspected', 'Refunded', 'Exchanged', 'Closed', 'Rejected', 'Cancelled', 'Expired']
  .map((s) => ({ label: s.replace(/([a-z])([A-Z])/g, '$1 $2'), value: s }))

export const rmaResolutionOptions = [
  { label: 'Refund', value: 'Refund' },
  { label: 'Exchange', value: 'Replacement' },
  { label: 'Store credit', value: 'StoreCredit' }
]

export const rmaReturnMethodOptions = [
  { label: 'Customer ships it', value: 'SelfShip' },
  { label: 'Prepaid label', value: 'PrepaidLabel' },
  { label: 'Drop off at store', value: 'DropOff' }
]

export const rmaConditionOptions = [
  { label: 'Resellable', value: 'Resellable' },
  { label: 'Open box', value: 'OpenBox' },
  { label: 'Damaged', value: 'Damaged' },
  { label: 'Defective', value: 'Defective' },
  { label: 'Wrong item / not ours', value: 'WrongItem' },
  { label: 'Not received', value: 'NotReceived' }
]

export const rmaDispositionOptions = [
  { label: 'Restock', value: 'Restock' },
  { label: 'Restock as open box', value: 'RestockOpenBox' },
  { label: 'Return to vendor', value: 'ReturnToVendor' },
  { label: 'Scrap', value: 'Scrap' }
]

export const returnLabelModeOptions = [
  { label: 'Customer ships it themselves', value: 'CustomerShips' },
  { label: 'Prepaid label (store pays)', value: 'PrepaidMerchant' },
  { label: 'Prepaid label, cost deducted from refund', value: 'PrepaidDeducted' },
  { label: 'No return shipping (in store only)', value: 'Disabled' }
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

export default { orderApi, rmaApi, orderStatusColor, allowedTransitions, paymentStatusColor, paymentMethodLabel, humanizeEnum, shipmentCarrierOptions, defaultShipmentCarrier, rmaStatusColor, rmaStatusOptions, rmaResolutionOptions, rmaReturnMethodOptions, rmaConditionOptions, rmaDispositionOptions, returnLabelModeOptions, formatMoney, formatDate }
