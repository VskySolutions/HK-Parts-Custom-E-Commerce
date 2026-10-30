/*
 * Log Viewer API layer (WO-70): read-only application logs.
 *
 * Wraps the paged GET /api/admin/logs endpoint (page, pageSize, dateFrom?, dateTo?,
 * level?, correlationId?, search?) plus small display helpers for the level badge.
 */
import { api, unwrap, qsSerializer } from 'services/api'

export const logApi = {
  list (params = {}) {
    return api.get('/api/admin/logs', { params, paramsSerializer: qsSerializer }).then(unwrap)
  }
}

// Log level -> badge colour.
export function logLevelColor (level) {
  const s = (level || '').toLowerCase()
  if (s === 'fatal' || s === 'critical') return 'purple'
  if (s === 'error') return 'negative'
  if (s === 'warning' || s === 'warn') return 'orange'
  if (s === 'information' || s === 'info') return 'primary'
  if (s === 'debug') return 'teal'
  return 'grey'
}

// Level filter options (the store keeps Error/Fatal; "All" clears the filter).
export const logLevelOptions = [
  { label: 'All levels', value: null },
  { label: 'Error', value: 'Error' },
  { label: 'Fatal', value: 'Fatal' }
]

export default { logApi, logLevelColor, logLevelOptions }
