/*
 * Inquiries admin module API (REQ-INQ-001).
 *
 * Wraps AdminInquiriesController. An inquiry is a quote request: a stored order row that took no
 * payment, moved no stock and is excluded from every order/revenue listing.
 *
 *   GET  /api/admin/inquiries              -> PaginatedList<InquirySummaryDto>
 *   GET  /api/admin/inquiries/{id}         -> InquiryDto
 *   PUT  /api/admin/inquiries/{id}         -> InquiryDto  (pipeline status + internal notes)
 *   POST /api/admin/inquiries/{id}/quote   -> InquiryDto  (emails the buyer a quote)
 *   POST /api/admin/inquiries/{id}/convert -> OrderDto    (Standard mode only)
 */
import { api, unwrap, qsSerializer } from 'services/api'

const INQUIRIES = '/api/admin/inquiries'

export const inquiryApi = {
  list (params = {}) {
    return api.get(INQUIRIES, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  get (id) {
    return api.get(`${INQUIRIES}/${id}`).then(unwrap)
  },
  update (id, payload) {
    return api.put(`${INQUIRIES}/${id}`, payload).then(unwrap)
  },
  sendQuote (id, payload) {
    return api.post(`${INQUIRIES}/${id}/quote`, payload).then(unwrap)
  },
  convert (id, payload) {
    return api.post(`${INQUIRIES}/${id}/convert`, payload).then(unwrap)
  }
}

// The inquiry sales pipeline, in the order a request actually travels.
export const inquiryStatuses = [
  'New', 'InReview', 'Quoted', 'Accepted', 'Declined', 'Converted', 'Closed'
]

export const inquiryStatusOptions = inquiryStatuses.map((s) => ({ label: humanStatus(s), value: s }))

export function humanStatus (status) {
  return status === 'InReview' ? 'In review' : status
}

export function inquiryStatusColor (status) {
  switch (status) {
    case 'New': return 'blue'
    case 'InReview': return 'indigo'
    case 'Quoted': return 'orange'
    case 'Accepted': return 'teal'
    case 'Declined': return 'red'
    case 'Converted': return 'green'
    case 'Closed': return 'grey'
    default: return 'grey'
  }
}

export default { inquiryApi, inquiryStatusOptions, inquiryStatusColor, humanStatus }
