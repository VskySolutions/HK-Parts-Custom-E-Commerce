/*
 * Subscriptions module API resource (WO-49).
 *
 * Read-only admin list of recurring subscriptions plus the two lifecycle
 * actions (pause / cancel). Wraps /api/admin/subscriptions. Mirrors the orders
 * api style: `.then(unwrap)`, qsSerializer for the paged list query.
 *
 *   GET  /api/admin/subscriptions            -> PaginatedList<SubscriptionDto>
 *   POST /api/admin/subscriptions/{id}/pause
 *   POST /api/admin/subscriptions/{id}/cancel
 */
import { api, unwrap, qsSerializer } from 'services/api'

const BASE = '/api/admin/subscriptions'

export const subscriptionApi = {
  list (params = {}) {
    return api.get(BASE, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  pause (id) {
    return api.post(`${BASE}/${id}/pause`).then(unwrap)
  },
  cancel (id) {
    return api.post(`${BASE}/${id}/cancel`).then(unwrap)
  }
}

// Interval enum name -> human label.
const INTERVAL_LABELS = {
  Weekly: 'Weekly',
  BiWeekly: 'Bi-weekly',
  Monthly: 'Monthly',
  Quarterly: 'Quarterly'
}
export const intervalLabel = (v) => INTERVAL_LABELS[v] || v || '—'

// Status enum name -> badge colour.
export const subscriptionStatusColor = (status) => {
  switch (status) {
    case 'Active': return 'positive'
    case 'Paused': return 'orange'
    case 'Cancelled': return 'grey'
    default: return 'grey'
  }
}

export default subscriptionApi
