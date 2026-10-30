/*
 * Customers admin module API layer (WO-117): customer list + detail, single Customer Group
 * assignment, activate/deactivate, and store credit. Wraps AdminCustomersController.
 * Tax-exemption review lives in its own module (WO-126) — here it is read-only status only.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const CUSTOMERS = '/api/admin/customers'

export const customerAdminApi = {
  list (params = {}) { return api.get(CUSTOMERS, { params, paramsSerializer: qsSerializer }).then(unwrap) },
  get (id) { return api.get(`${CUSTOMERS}/${id}`).then(unwrap) },

  // Single pricing group (AC-CUS-003.2). getGroup → brief|null; setGroup(null) clears it (base pricing).
  getGroup (id) { return api.get(`${CUSTOMERS}/${id}/group`).then(unwrap) },
  setGroup (id, customerGroupId) {
    return api.put(`${CUSTOMERS}/${id}/group`, { customerId: id, customerGroupId }).then(unwrap)
  },

  // Activate / deactivate the customer's login (User.IsActive). Returns the refreshed detail.
  setActive (id, isActive) {
    return api.put(`${CUSTOMERS}/${id}/active`, { customerId: id, isActive }).then(unwrap)
  },

  getStoreCredit (id) { return api.get(`${CUSTOMERS}/${id}/store-credit`).then(unwrap) },
  issueStoreCredit (id, payload) { return api.post(`${CUSTOMERS}/${id}/store-credit`, { customerId: id, ...payload }).then(unwrap) }
}

/** Active pricing groups, for the customer-detail assignment single-select and the list filter. */
export const customerGroupOptionsApi = {
  listActive () {
    return api.get('/api/admin/customer-groups', { params: { activeOnly: true }, paramsSerializer: qsSerializer }).then(unwrap)
  }
}

export default { customerAdminApi, customerGroupOptionsApi }
