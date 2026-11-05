/*
 * Price Rules admin API (AdminPriceRulesController). Enums travel as names:
 * discountType = Percent | FixedAmount, effectiveState = Inactive | Scheduled | Running | Expired.
 * Customer group id 00000000-0000-0000-0000-000000000000 is "Retail" (guests / no group).
 */
import { api, unwrap, qsSerializer } from 'services/api'

const BASE = '/api/admin/price-rules'

export const RETAIL_GROUP_ID = '00000000-0000-0000-0000-000000000000'

export const priceRuleApi = {
  list (params = {}) {
    return api.get(BASE, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  get (id) {
    return api.get(`${BASE}/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post(BASE, payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`${BASE}/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`${BASE}/${id}`).then(unwrap)
  },
  apply (id) {
    return api.post(`${BASE}/${id}/apply`).then(unwrap)
  },
  applyAll (params = {}) {
    return api.post(`${BASE}/apply-all`, null, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  preview (payload) {
    return api.post(`${BASE}/preview`, payload).then(unwrap)
  },
  products (id, params = {}) {
    return api.get(`${BASE}/${id}/products`, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  customerGroups () {
    return api.get(`${BASE}/customer-groups`).then(unwrap)
  },
  categoryTree () {
    return api.get('/api/admin/categories/tree').then(unwrap)
  }
}

/** "10%" or "$5.00". */
export function formatDiscount (type, amount) {
  const n = Number(amount) || 0
  return type === 'Percent'
    ? `${Number(n.toFixed(2))}%`
    : n.toLocaleString(undefined, { style: 'currency', currency: 'USD' })
}

export const EFFECTIVE_STATE_COLORS = {
  Running: 'positive',
  Scheduled: 'info',
  Expired: 'grey-7',
  Inactive: 'grey'
}
