/*
 * Loyalty module API resource (WO-27).
 *
 * The loyalty program config is a keyed singleton (no id, no list) exposed at
 * /api/admin/loyalty. Mirrors the tax/recaptcha settings style: every method
 * resolves to the unwrapped payload via `.then(unwrap)`.
 *
 *   GET /api/admin/loyalty -> { enabled, earnRate, redeemRate }
 *   PUT /api/admin/loyalty -> { enabled, earnRate, redeemRate }
 */
import { api, unwrap } from 'services/api'

export const loyaltyApi = {
  get () {
    return api.get('/api/admin/loyalty').then(unwrap)
  },
  update (payload) {
    return api.put('/api/admin/loyalty', payload).then(unwrap)
  }
}

export default loyaltyApi
