/*
 * Pricing admin module API layer (WO-115): discounts + coupon codes.
 * Wraps AdminDiscountsController + AdminCouponsController. Enums transport as names.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const DISCOUNTS = '/api/admin/discounts'
const COUPONS = '/api/admin/coupons'

export const discountApi = {
  list (params = {}) {
    return api.get(DISCOUNTS, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  get (id) { return api.get(`${DISCOUNTS}/${id}`).then(unwrap) },
  create (payload) { return api.post(DISCOUNTS, payload).then(unwrap) },
  update (id, payload) { return api.put(`${DISCOUNTS}/${id}`, payload).then(unwrap) },
  remove (id) { return api.delete(`${DISCOUNTS}/${id}`).then(unwrap) }
}

export const couponApi = {
  list (params = {}) {
    return api.get(COUPONS, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  get (id) { return api.get(`${COUPONS}/${id}`).then(unwrap) },
  create (payload) { return api.post(COUPONS, payload).then(unwrap) },
  update (id, payload) { return api.put(`${COUPONS}/${id}`, payload).then(unwrap) },
  remove (id) { return api.delete(`${COUPONS}/${id}`).then(unwrap) }
}

export const discountScopeOptions = [
  { label: 'Cart total', value: 'CartTotal' },
  { label: 'Order subtotal', value: 'OrderSubtotal' },
  { label: 'Product', value: 'Product' },
  { label: 'Category', value: 'Category' }
]

export const discountTypeOptions = [
  { label: 'Percentage', value: 'Percentage' },
  { label: 'Fixed amount', value: 'FixedAmount' }
]

export const couponUsageOptions = [
  { label: 'Single use', value: 'SingleUse' },
  { label: 'Limited', value: 'Limited' },
  { label: 'Unlimited', value: 'Unlimited' }
]

export function discountValueLabel (d) {
  if (!d) return '—'
  return d.type === 'Percentage' ? `${d.value}%` : Number(d.value).toFixed(2)
}

export default { discountApi, couponApi, discountScopeOptions, discountTypeOptions, couponUsageOptions, discountValueLabel }
