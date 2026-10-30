/*
 * Currencies resource (WO-91 REQ-TEN-006).
 *
 * Wraps the TenantCurrenciesController + TenantBaseCurrencyController surface.
 * Mirrors the widget/branding api templates: every method resolves to the
 * unwrapped payload via `.then(unwrap)`.
 *
 *   GET    /api/tenant/currencies[?enabledOnly=]  -> CurrencyDto[]
 *   GET    /api/tenant/currencies/{code}          -> CurrencyDto
 *   POST   /api/tenant/currencies                 -> CurrencyDto
 *   PUT    /api/tenant/currencies/{code}          -> CurrencyDto
 *   DELETE /api/tenant/currencies/{code}          -> 204
 *   GET    /api/tenant/currencies/auto-refresh    -> AutoRefreshConfigDto
 *   PUT    /api/tenant/currencies/auto-refresh    -> AutoRefreshConfigDto
 *   GET    /api/tenant/base-currency              -> CurrencyDto
 *   PUT    /api/tenant/base-currency              -> CurrencyDto (SuperAdmin)
 *
 * NOTE: there is no dedicated "update rate" endpoint on the backend; a manual
 * rate change reuses PUT /api/tenant/currencies/{code} carrying the currency's
 * other fields unchanged so nothing is wiped.
 */
import { api, unwrap } from 'services/api'

export const currenciesApi = {
  list (enabledOnly) {
    const params = enabledOnly === undefined ? undefined : { enabledOnly }
    return api.get('/api/tenant/currencies', { params }).then(unwrap)
  },
  get (code) {
    return api.get(`/api/tenant/currencies/${code}`).then(unwrap)
  },
  create (payload) {
    return api.post('/api/tenant/currencies', payload).then(unwrap)
  },
  update (code, payload) {
    return api.put(`/api/tenant/currencies/${code}`, payload).then(unwrap)
  },
  remove (code) {
    return api.delete(`/api/tenant/currencies/${code}`).then(unwrap)
  },
  // Manual rate update — reuses the Update endpoint, preserving symbol/flags.
  updateRate (code, { symbol, exchangeRate, isEnabled, isRateLocked }) {
    return currenciesApi.update(code, { symbol, exchangeRate, isEnabled, isRateLocked })
  },
  getBaseCurrency () {
    return api.get('/api/tenant/base-currency').then(unwrap)
  },
  setBaseCurrency (code) {
    return api.put('/api/tenant/base-currency', { code }).then(unwrap)
  },
  getAutoRefreshConfig () {
    return api.get('/api/tenant/currencies/auto-refresh').then(unwrap)
  },
  updateAutoRefreshConfig (payload) {
    return api.put('/api/tenant/currencies/auto-refresh', payload).then(unwrap)
  }
}

export default currenciesApi
