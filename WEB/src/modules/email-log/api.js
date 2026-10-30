/*
 * Email Log admin module API layer. Browse the full email send history (the EmailQueue the
 * dispatch worker delivers from) and re-send any entry. Wraps AdminEmailLogController.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const EMAIL_LOG = '/api/admin/email-log'

export const emailLogApi = {
  list (params = {}) {
    return api.get(EMAIL_LOG, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  get (id) {
    return api.get(`${EMAIL_LOG}/${id}`).then(unwrap)
  },
  resend (id) {
    return api.post(`${EMAIL_LOG}/${encodeURIComponent(id)}/resend`).then(unwrap)
  }
}

export default { emailLogApi }
