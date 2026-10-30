/* Tax admin module API layer (WO-120): provider config + US economic-nexus status. */
import { api, unwrap, qsSerializer } from 'services/api'

export const taxApi = {
  getConfig () { return api.get('/api/admin/tax').then(unwrap) },
  updateConfig (payload) { return api.put('/api/admin/tax', payload).then(unwrap) },
  nexusStatus (params = {}) {
    return api.get('/api/admin/tax/nexus-status', { params, paramsSerializer: qsSerializer }).then(unwrap)
  }
}

export const taxProviderOptions = [
  { label: 'Flat rate (built-in)', value: 'FlatRate' },
  { label: 'TaxJar', value: 'TaxJar' },
  { label: 'Stripe Tax', value: 'StripeTax' }
]

export default { taxApi, taxProviderOptions }
