/*
 * Email Templates resource group (WO-80, REQ-ENT-002 / REQ-ENT-003 / REQ-ENT-004).
 *
 * Wraps the admin email-template endpoints. Every method resolves to the
 * unwrapped payload via `.then(unwrap)`, mirroring the widget/branding APIs.
 *
 *   GET    /api/admin/email-templates                 -> EmailTemplateSummaryDto[]
 *   GET    /api/admin/email-templates/{key}           -> EmailTemplateDto
 *   PUT    /api/admin/email-templates/{key}           -> EmailTemplateDto
 *   PUT    /api/admin/email-templates/{key}/enabled   -> EmailTemplateDto
 *   POST   /api/admin/email-templates/{key}/preview   -> { subject, fromName, htmlBody, textBody }
 *   POST   /api/admin/email-templates/{key}/test-send -> { dispatched, message, recipientEmail }
 */
import { api, unwrap, qsSerializer } from 'services/api'

const BASE = '/api/admin/email-templates'
const path = (key) => `${BASE}/${encodeURIComponent(key)}`

export const emailTemplatesApi = {
  // params: { category?, enabled?, search? }
  list (params = {}) {
    return api.get(BASE, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  get (key) {
    return api.get(path(key)).then(unwrap)
  },
  update (key, payload) {
    return api.put(path(key), payload).then(unwrap)
  },
  setEnabled (key, enabled, confirm = false) {
    return api.put(`${path(key)}/enabled`, { enabled, confirm }).then(unwrap)
  },
  // body: { subject?, htmlBody?, plainTextBody? } — omitted fields fall back to the stored template.
  preview ({ key, body }) {
    return api.post(`${path(key)}/preview`, body || {}).then(unwrap)
  },
  testSend ({ key, recipientEmail }) {
    return api.post(`${path(key)}/test-send`, { recipientEmail }).then(unwrap)
  }
}

export default emailTemplatesApi
