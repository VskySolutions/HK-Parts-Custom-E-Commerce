/*
 * Admin Alerts module API. Browse operational alerts (paged/filtered) and mark them resolved.
 * Wraps AdminAlertsController.
 *
 *   GET /api/admin/alerts?page&pageSize&search&severity&resolved -> PaginatedList<AdminAlertDto>
 *   PUT /api/admin/alerts/{id}/resolve                           -> 204
 */
import { api, unwrap, qsSerializer } from 'services/api'

const ALERTS = '/api/admin/alerts'

export const adminAlertApi = {
  list (params = {}) {
    return api.get(ALERTS, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  resolve (id) {
    return api.put(`${ALERTS}/${encodeURIComponent(id)}/resolve`).then(unwrap)
  }
}

export default { adminAlertApi }
