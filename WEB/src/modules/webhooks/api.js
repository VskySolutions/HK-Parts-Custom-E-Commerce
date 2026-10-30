/*
 * Webhooks admin module API layer (WO-118). Wraps AdminWebhooksController: subscription CRUD,
 * the subscribable event-type catalog, and delivery history. The signing secret is returned by the
 * API only on create (never by list/update), so the caller must surface it once at that moment.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const BASE = '/api/admin/webhooks'

export const webhookApi = {
  list () { return api.get(`${BASE}/subscriptions`).then(unwrap) },
  create (payload) { return api.post(`${BASE}/subscriptions`, payload).then(unwrap) },
  update (id, payload) { return api.put(`${BASE}/subscriptions/${id}`, payload).then(unwrap) },
  remove (id) { return api.delete(`${BASE}/subscriptions/${id}`).then(unwrap) },
  eventTypes () { return api.get(`${BASE}/event-types`).then(unwrap) },
  deliveries (params = {}) { return api.get(`${BASE}/deliveries`, { params, paramsSerializer: qsSerializer }).then(unwrap) }
}

export function webhookDeliveryStatusColor (status) {
  return {
    Pending: 'orange',
    Succeeded: 'positive',
    Failed: 'red',
    PermanentlyFailed: 'grey'
  }[status] || 'grey'
}

export default { webhookApi, webhookDeliveryStatusColor }
