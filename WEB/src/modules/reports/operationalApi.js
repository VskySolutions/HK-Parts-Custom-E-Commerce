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
  },

  // Guest checkout A-40/A-41. GET /sales-by-state?period=&customerType= ->
  //   [{ state, countryCode, orders, revenue, guestOrders, guestRevenue, registeredOrders, registeredRevenue }]
  salesByState (params = {}) {
    return api.get(`${BASE}/sales-by-state`, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  salesByStateCsv (params = {}) {
    return api.get(`${BASE}/sales-by-state.csv`, { params, paramsSerializer: qsSerializer, responseType: 'blob' })
  },

  // PHP "Tax Report". GET /taxes?period=&from=&to= ->
  //   { totalTax, orderCount, taxedOrderCount, totalSales, fromUtc, toUtc, rows: [{ state, countryCode, orders, taxedOrders, sales, tax }] }
  taxes (params = {}) {
    return api.get(`${BASE}/taxes`, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  taxesCsv (params = {}) {
    return api.get(`${BASE}/taxes.csv`, { params, paramsSerializer: qsSerializer, responseType: 'blob' })
  },

  // Guest checkout A-40/A-43. GET /abandoned-carts?page=&pageSize=&idleHours=&customerType=&search=&onlyWithEmail= ->
  //   PaginatedList<{ cartId, isGuest, name, email, marketingOptIn, itemCount, units, subtotal, currencyCode, lastActivityUtc, reachedCheckout, recoveryEmailSentUtc }>
  abandonedCarts (params = {}) {
    return api.get(`${BASE}/abandoned-carts`, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  abandonedCartsCsv (params = {}) {
    return api.get(`${BASE}/abandoned-carts.csv`, { params, paramsSerializer: qsSerializer, responseType: 'blob' })
  }
}

// Guest checkout A-41: the customer-type filter shared by the sales reports.
export const customerTypeOptions = [
  { label: 'All customers', value: 'All' },
  { label: 'Guest', value: 'Guest' },
  { label: 'Registered', value: 'Registered' }
]

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
