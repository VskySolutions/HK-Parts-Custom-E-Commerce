/*
 * Barcode inventory scanner API (AdminInventoryScannerController, Inventory module).
 * The scanned code goes in the query string (params), never the path: Code 39 / Code 128 labels may
 * contain '/', '+', '%' or spaces.
 */
import { api, unwrap, getApiErrorMessage } from 'services/api'
import { parseUtc, formatDateTime } from 'src/utils/datetime'

const BASE = '/api/admin/inventory-scanner'

// How the product was reached; recorded on the audit row (InventoryScanSource).
export const ScanSource = Object.freeze({
  Camera: 'Camera',
  Gallery: 'Gallery',
  Manual: 'Manual',
  Hardware: 'Hardware'
})

export const inventoryScannerApi = {
  // "Today" is computed server-side in the tenant's display timezone.
  dashboard (take = 10) {
    return api.get(BASE + '/dashboard', { params: { take } }).then(unwrap)
  },
  // → ScannedProductDto. 400 empty / > 100 chars, 404 no match, 409 several matches ({ candidates }).
  lookup (code) {
    return api.get(BASE + '/lookup', { params: { code } }).then(unwrap)
  },
  getProduct (productId, variantId) {
    return api.get(BASE + '/products/' + productId, { params: { variantId: variantId || undefined } }).then(unwrap)
  },
  // → PaginatedList<InventoryScanLogDto>
  history (productId, { variantId, page = 1, pageSize = 20 } = {}) {
    return api.get(BASE + '/products/' + productId + '/history', {
      params: { variantId: variantId || undefined, page, pageSize }
    }).then(unwrap)
  },
  // { productId, productVariantId, expectedQty, expectedPrice, newQty, newPrice|null, notes, scannedCode,
  //   source, matchedBy } → InventoryScanLogDto. 409 → { currentQty, currentPrice, product }.
  update (payload) {
    return api.post(BASE + '/update', payload).then(unwrap)
  },
  log (id) {
    return api.get(BASE + '/logs/' + id).then(unwrap)
  }
}

export { formatDateTime }

export function formatMoney (value) {
  if (value === null || value === undefined || value === '') return '—'
  return '$' + Number(value).toFixed(2)
}

// Prefers the first field message of a 400 over the generic ProblemDetails title.
export function errorText (err) {
  const errors = err?.response?.data?.errors
  if (errors && typeof errors === 'object') {
    const first = Object.values(errors)[0]
    if (Array.isArray(first) && first.length) return first[0]
    if (typeof first === 'string') return first
  }
  return getApiErrorMessage(err)
}

// "5 minutes ago" style (the PHP dashboard used Carbon::diffForHumans).
export function timeAgo (value) {
  const d = parseUtc(value)
  if (!d) return ''
  const diff = Math.max(0, (Date.now() - d.getTime()) / 1000)
  if (diff < 45) return 'just now'
  const steps = [[60, 'minute'], [3600, 'hour'], [86400, 'day'], [604800, 'week'], [2629800, 'month'], [31557600, 'year']]
  let unit = 'minute'
  let size = 60
  for (const [s, u] of steps) { if (diff >= s) { size = s; unit = u } }
  const n = Math.max(1, Math.round(diff / size))
  return n + ' ' + unit + (n === 1 ? '' : 's') + ' ago'
}

/*
 * The product screen is addressed by id (from the lookup result), not by the raw code. The scanned code,
 * source and match type ride along in the query so the update can record them on the audit row.
 */
export function productRoute ({ productId, productVariantId } = {}, { code, source, matchedBy } = {}) {
  const query = {}
  if (productVariantId) query.variantId = productVariantId
  if (code) query.code = String(code).trim()
  if (source) query.source = source
  if (matchedBy) query.matchedBy = matchedBy
  return { name: 'inventory-scanner-product', params: { productId }, query }
}
