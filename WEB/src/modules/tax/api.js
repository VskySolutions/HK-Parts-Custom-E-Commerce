/* Tax admin module API layer (WO-120 + PHP tax parity): settings, state/ZIP rates, Avalara test + log, nexus. */
import { api, unwrap, qsSerializer } from 'services/api'

const BASE = '/api/admin/tax'

export const taxApi = {
  getConfig () { return api.get(BASE).then(unwrap) },
  updateConfig (payload) { return api.put(BASE, payload).then(unwrap) },
  nexusStatus (params = {}) {
    return api.get(`${BASE}/nexus-status`, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },

  // State rates: [{ id, countryCode, stateCode, stateName, ratePercent }]; save sends [{ stateCode, ratePercent }].
  stateRates () { return api.get(`${BASE}/state-rates`).then(unwrap) },
  saveStateRates (rates) { return api.put(`${BASE}/state-rates`, { rates }).then(unwrap) },

  // ZIP rates: PaginatedList<{ id, postalCode, ratePercent }>.
  zipRates (params = {}) {
    return api.get(`${BASE}/zip-rates`, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  createZipRate (payload) { return api.post(`${BASE}/zip-rates`, payload).then(unwrap) },
  updateZipRate (id, payload) { return api.put(`${BASE}/zip-rates/${id}`, payload).then(unwrap) },
  deleteZipRate (id) { return api.delete(`${BASE}/zip-rates/${id}`).then(unwrap) },
  // CSV "zip,rate" → { added, updated, skipped, errors }.
  importZipRates (file) {
    const form = new FormData()
    form.append('file', file)
    return api.post(`${BASE}/zip-rates/import`, form, { headers: { 'Content-Type': 'multipart/form-data' } }).then(unwrap)
  },

  // Avalara account (PHP Avalara Settings): { id, serviceUrl, accountNumber, companyCode, hasLicenseKey, isProduction }.
  // Save sends { serviceUrl, accountNumber, licenseKey, companyCode }; a blank licenseKey keeps the key on file.
  avalaraAccount () { return api.get(`${BASE}/avalara/account`).then(unwrap) },
  saveAvalaraAccount (payload) { return api.put(`${BASE}/avalara/account`, payload).then(unwrap) },

  // Avalara: { success, message }.
  testAvalara () { return api.post(`${BASE}/avalara/test`).then(unwrap) },
  logs (params = {}) {
    return api.get(`${BASE}/logs`, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  log (id) { return api.get(`${BASE}/logs/${id}`).then(unwrap) },
  clearLogs (olderThanDays = 0) { return api.delete(`${BASE}/logs`, { params: { olderThanDays } }).then(unwrap) }
}

// PHP "Default Tax Method" (none / state / zip / avalara) plus the providers this app already had.
export const taxProviderOptions = [
  { label: 'No Tax Charged', value: 'None' },
  { label: 'By State', value: 'ByState' },
  { label: 'By ZIP/Postal Code', value: 'ByZip' },
  { label: 'Avalara', value: 'Avalara' },
  { label: 'Flat rate (built-in)', value: 'FlatRate' },
  { label: 'TaxJar', value: 'TaxJar' },
  { label: 'Stripe Tax', value: 'StripeTax' }
]

export const taxLogActionOptions = [
  { label: 'All actions', value: '' },
  { label: 'Get Tax', value: 'Get Tax' },
  { label: 'Commit Order', value: 'Commit Order' },
  { label: 'Void Tax', value: 'Void Tax' },
  { label: 'Test Connection', value: 'Test Connection' }
]

export default { taxApi, taxProviderOptions, taxLogActionOptions }
