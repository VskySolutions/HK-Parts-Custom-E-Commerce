/*
 * FFL dealer directory admin API (docs/ffl-shipping-information-requirements.md §5.10, §6.1).
 *
 *   GET/PUT /api/admin/ffl/settings            -> FflSettingsDto (Geocodio key is write-only)
 *   GET     /api/admin/ffl/stats               -> FflDirectoryStatsDto
 *   GET     /api/admin/ffl/dealers             -> PaginatedList<FflDealerDto>  (search, state, activeOnly, page, pageSize)
 *   GET     /api/admin/ffl/dealers/search      -> FflSearchResultDto           (zip, radius)
 *   POST    /api/admin/ffl/dealers             -> FflDealerDto                 (add one dealer by hand; flagged manual)
 *   GET     /api/admin/ffl/dealers/:key        -> FflDealerDto
 *   PUT     /api/admin/ffl/dealers/:key/data   -> FflDealerDto                 (allowsTransfers, lastTransferFee)
 *   POST    /api/admin/ffl/import              -> ImportFflDealersResult       (multipart: file, deactivateMissing)
 *   POST    /api/admin/ffl/geocode             -> GeocodeFflDealersResult
 *   PUT     /api/admin/orders/:id/ffl-dealer   -> { message, dealer }          (dealerKey)
 */
import { api, unwrap } from 'services/api'

const BASE = '/api/admin/ffl'

export const fflAdminApi = {
  getSettings () {
    return api.get(BASE + '/settings').then(unwrap)
  },
  updateSettings (payload) {
    return api.put(BASE + '/settings', payload).then(unwrap)
  },
  stats () {
    return api.get(BASE + '/stats').then(unwrap)
  },
  list (params) {
    return api.get(BASE + '/dealers', { params }).then(unwrap)
  },
  search (zip, radius) {
    return api.get(BASE + '/dealers/search', { params: { zip, radius } }).then(unwrap)
  },
  dealer (key) {
    return api.get(BASE + '/dealers/' + encodeURIComponent(key)).then(unwrap)
  },
  createDealer (payload) {
    return api.post(BASE + '/dealers', payload).then(unwrap)
  },
  updateDealerData (key, payload) {
    return api.put(BASE + '/dealers/' + encodeURIComponent(key) + '/data', payload).then(unwrap)
  },
  importFile (file, deactivateMissing) {
    const form = new FormData()
    form.append('file', file)
    form.append('deactivateMissing', deactivateMissing ? 'true' : 'false')
    // A full ATF list is ~80k rows: allow a few minutes.
    return api.post(BASE + '/import', form, { timeout: 600000 }).then(unwrap)
  },
  geocode (maxZips) {
    return api.post(BASE + '/geocode', { maxZips }, { timeout: 600000 }).then(unwrap)
  },
  changeOrderDealer (orderId, dealerKey) {
    return api.put(`/api/admin/orders/${orderId}/ffl-dealer`, { dealerKey }).then(unwrap)
  }
}

/** ATF eZ Check, where staff validate a dealer's licence (FFL-91). */
export const FFL_EZ_CHECK_URL = 'https://fflezcheck.atf.gov/FFLEzCheck/'

export default { fflAdminApi }
