/*
 * Tax Exemption admin module API (WO-126). Wraps AdminTaxExemptionRequestsController
 * (/api/admin/tax-exemption-requests): browse the queue, open a request, approve/reject.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const REQUESTS = '/api/admin/tax-exemption-requests'

export const taxExemptionApi = {
  list (params = {}) { return api.get(REQUESTS, { params, paramsSerializer: qsSerializer }).then(unwrap) },
  get (id) { return api.get(`${REQUESTS}/${id}`).then(unwrap) },
  approve (id, adminNote) { return api.post(`${REQUESTS}/${id}/approve`, { adminNote }).then(unwrap) },
  reject (id, adminNote) { return api.post(`${REQUESTS}/${id}/reject`, { adminNote }).then(unwrap) }
}

/** Status filter options (mirrors TaxExemptionRequestStatus). */
export const statusOptions = [
  { label: 'All', value: null },
  { label: 'Pending review', value: 'PendingReview' },
  { label: 'Approved', value: 'Approved' },
  { label: 'Rejected', value: 'Rejected' }
]

/** Badge colour per status, for list + drawer. */
export function statusColor (status) {
  switch (status) {
    case 'Approved': return 'positive'
    case 'Rejected': return 'negative'
    case 'PendingReview': return 'orange'
    default: return 'grey'
  }
}

export default { taxExemptionApi, statusOptions, statusColor }
