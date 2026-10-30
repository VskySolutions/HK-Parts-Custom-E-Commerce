/*
 * Operational Reports API layer (WO-60).
 *
 * Wraps the AdminReportsController operational endpoints under /api/admin/reports:
 * best-sellers, low-stock and customers — each with a matching `.csv` export that
 * returns the raw blob response for a browser download. Kept separate from the
 * existing WO-119 reportApi (store-performance) so neither file is modified.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const BASE = '/api/admin/reports'

export const operationalReportsApi = {
  // GET /best-sellers?period=&take= -> [{ productId, productName, unitsSold, revenue }]
  bestSellers (params = {}) {
    return api.get(`${BASE}/best-sellers`, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  bestSellersCsv (params = {}) {
    return api.get(`${BASE}/best-sellers.csv`, { params, paramsSerializer: qsSerializer, responseType: 'blob' })
  },

  // GET /low-stock -> [{ productName, variantSku, storeName, stockQuantity, lowStockThreshold }]
  lowStock (params = {}) {
    return api.get(`${BASE}/low-stock`, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  lowStockCsv () {
    return api.get(`${BASE}/low-stock.csv`, { responseType: 'blob' })
  },

  // GET /customers?period= -> { newRegistrations, totalActiveCustomers, topCustomers:[{ customerName, email, orderCount, totalSpent }] }
  customers (params = {}) {
    return api.get(`${BASE}/customers`, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  customersCsv (params = {}) {
    return api.get(`${BASE}/customers.csv`, { params, paramsSerializer: qsSerializer, responseType: 'blob' })
  }
}

// Period selector options + params builder (mirrors the analytics module; duplicated to keep the
// reports module self-contained).
export const periodOptions = [
  { label: 'Today', value: 'today' },
  { label: 'Last 7 days', value: 'last7' },
  { label: 'Last 30 days', value: 'last30' },
  { label: 'Custom', value: 'custom' }
]

export function periodParams (period, from, to) {
  if (period === 'custom') return { period: 'custom', from: from || undefined, to: to || undefined }
  return { period }
}

export function money (value, code) {
  if (value === null || value === undefined || value === '') return '—'
  const n = Number(value)
  if (Number.isNaN(n)) return '—'
  if (code) {
    try {
      return new Intl.NumberFormat('en-US', { style: 'currency', currency: code }).format(n)
    } catch (e) { /* unknown ISO code — fall through */ }
  }
  return n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// Trigger a browser download for a Blob under the given filename.
export function saveBlob (blob, filename) {
  const url = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  window.URL.revokeObjectURL(url)
}

export default { operationalReportsApi, periodOptions, periodParams, money, saveBlob }
