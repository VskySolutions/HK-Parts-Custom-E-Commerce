/*
 * Audit Trail API layer (WO-61): read-only actor/action history.
 *
 * Wraps the paged GET /api/admin/audit-trail endpoint (page, pageSize, dateFrom?, dateTo?,
 * userId?, action?, entityType?, search?) plus a display helper for the action badge.
 */
import { api, unwrap, qsSerializer } from 'services/api'

export const auditTrailApi = {
  list (params = {}) {
    return api.get('/api/admin/audit-trail', { params, paramsSerializer: qsSerializer }).then(unwrap)
  }
}

// Audit action -> badge colour (matches on the verb so it tolerates any action naming).
export function auditActionColor (action) {
  const s = (action || '').toLowerCase()
  if (s.includes('delete') || s.includes('remove')) return 'negative'
  if (s.includes('create') || s.includes('add') || s.includes('insert')) return 'positive'
  if (s.includes('update') || s.includes('edit') || s.includes('change') || s.includes('modif')) return 'primary'
  if (s.includes('login') || s.includes('logout') || s.includes('auth')) return 'teal'
  return 'grey'
}

export default { auditTrailApi, auditActionColor }
