/*
 * Customer Groups admin module API (WO-22). Wraps AdminCustomerGroupsController
 * (/api/admin/customer-groups). Group prices are saved through the dedicated
 * group-prices endpoint, matching the product tier-price pattern.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const GROUPS = '/api/admin/customer-groups'

export const customerGroupApi = {
  list (params = {}) { return api.get(GROUPS, { params, paramsSerializer: qsSerializer }).then(unwrap) },
  get (id) { return api.get(`${GROUPS}/${id}`).then(unwrap) },
  create (payload) { return api.post(GROUPS, payload).then(unwrap) },
  update (id, payload) { return api.put(`${GROUPS}/${id}`, { ...payload, id }).then(unwrap) },
  remove (id) { return api.delete(`${GROUPS}/${id}`).then(unwrap) },
  // Replace the whole fixed-price set for the group. `prices` = [{ productId, productVariantId, price }].
  setGroupPrices (id, prices) {
    return api.put(`${GROUPS}/${id}/group-prices`, { groupId: id, prices }).then(unwrap)
  }
}

/** Pricing-rule options shared by the detail form (mirrors CustomerGroupPricingRuleType). */
export const pricingRuleOptions = [
  { label: 'No adjustment (base price)', value: 'None' },
  { label: 'Percentage discount', value: 'PercentageDiscount' },
  { label: 'Fixed group prices', value: 'FixedGroupPrice' }
]

export default { customerGroupApi, pricingRuleOptions }
