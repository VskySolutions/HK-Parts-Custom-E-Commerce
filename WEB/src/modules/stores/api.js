/*
 * Stores admin module API layer (WO-113). Wraps AdminStoresController and
 * AdminStoreDeliveryZonesController on the authenticated instance.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const STORES = '/api/admin/stores'

export const storeApi = {
  list (params = {}) {
    return api.get(STORES, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  get (id) {
    return api.get(`${STORES}/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post(STORES, payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`${STORES}/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`${STORES}/${id}`).then(unwrap)
  }
}

export const deliveryZoneApi = {
  list (storeId) {
    return api.get(`${STORES}/${storeId}/delivery-zones`).then(unwrap)
  },
  create (storeId, payload) {
    return api.post(`${STORES}/${storeId}/delivery-zones`, { ...payload, storeId }).then(unwrap)
  },
  update (storeId, zoneId, payload) {
    return api.put(`${STORES}/${storeId}/delivery-zones/${zoneId}`, payload).then(unwrap)
  },
  remove (storeId, zoneId) {
    return api.delete(`${STORES}/${storeId}/delivery-zones/${zoneId}`).then(unwrap)
  }
}

// Store operational status derived from the enabled + maintenance flags.
export function storeStatus (store) {
  if (!store.isEnabled) return { label: 'Disabled', color: 'grey' }
  if (store.maintenanceMode) return { label: 'Maintenance', color: 'orange' }
  return { label: 'Active', color: 'positive' }
}

export const WEEK_DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

export default { storeApi, deliveryZoneApi, storeStatus, WEEK_DAYS }
