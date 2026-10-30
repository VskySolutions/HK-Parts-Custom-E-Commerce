/*
 * Settings module API (REQ-INQ-001 commerce mode).
 *
 *   GET /api/admin/commerce/mode  -> CommerceModeDto
 *   PUT /api/admin/commerce/mode  -> CommerceModeDto (UpdateCommerceModeCommand body)
 *
 * The mode is stored as `commerce.*` platform settings, so every change is audited in the settings
 * change history and applies on the next request without a restart.
 */
import { api, unwrap } from 'services/api'

export const commerceApi = {
  getMode () {
    return api.get('/api/admin/commerce/mode').then(unwrap)
  },
  updateMode (payload) {
    return api.put('/api/admin/commerce/mode', payload).then(unwrap)
  }
}

export default { commerceApi }
