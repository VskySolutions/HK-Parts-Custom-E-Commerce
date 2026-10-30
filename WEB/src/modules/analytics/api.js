/*
 * Analytics module API layer (WO-59): sales analytics dashboard.
 *
 * Wraps the AdminAnalyticsController endpoints under /api/admin/analytics using the
 * authenticated `api` instance. Also exports the shared period selector options +
 * a params builder and a currency-aware money formatter reused across the page.
 */
import { api, unwrap, qsSerializer } from 'services/api'

export const analyticsApi = {
  // GET /summary?period=&from=&to= -> { totalOrders, totalRevenue, averageOrderValue, newCustomers }
  summary (params = {}) {
    return api.get('/api/admin/analytics/summary', { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  // GET /trend?period=&from=&to= -> [{ date, orderCount, revenue }]
  trend (params = {}) {
    return api.get('/api/admin/analytics/trend', { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  // GET /recent-orders?take=10 -> [{ id, orderNumber, placedOnUtc, customerName, status, totalAmount, currencyCode }]
  recentOrders (params = {}) {
    return api.get('/api/admin/analytics/recent-orders', { params, paramsSerializer: qsSerializer }).then(unwrap)
  }
}

// Period selector options shared by the dashboard (and mirrored by the reports module).
export const periodOptions = [
  { label: 'Today', value: 'today' },
  { label: 'Last 7 days', value: 'last7' },
  { label: 'Last 30 days', value: 'last30' },
  { label: 'Custom', value: 'custom' }
]

// Build the query params for a period; custom carries from/to (yyyy-MM-dd) when provided.
export function periodParams (period, from, to) {
  if (period === 'custom') return { period: 'custom', from: from || undefined, to: to || undefined }
  return { period }
}

// Currency-aware money formatting. Falls back to a plain 2-decimal, thousands-separated number when the
// currency code is missing/unknown (matches the plain toFixed convention used elsewhere in the app).
export function money (value, code) {
  if (value === null || value === undefined || value === '') return '—'
  const n = Number(value)
  if (Number.isNaN(n)) return '—'
  if (code) {
    try {
      return new Intl.NumberFormat('en-US', { style: 'currency', currency: code }).format(n)
    } catch (e) { /* unknown ISO code — fall through to plain formatting */ }
  }
  return n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

// Order status -> badge colour (mirrors modules/orders/api.js orderStatusColor; kept local so the
// analytics module stays self-contained).
export function statusColor (status) {
  const s = (status || '').toLowerCase()
  if (s.includes('cancel') || s.includes('reject') || s.includes('unrouted')) return 'negative'
  if (s.includes('deliver') || s.includes('complete')) return 'positive'
  if (s.includes('ship')) return 'teal'
  if (s.includes('process') || s.includes('prepar') || s.includes('pending') || s.includes('routing')) return 'orange'
  if (s.includes('readyforpickup')) return 'purple'
  return 'grey'
}

export default { analyticsApi, periodOptions, periodParams, money, statusColor }
