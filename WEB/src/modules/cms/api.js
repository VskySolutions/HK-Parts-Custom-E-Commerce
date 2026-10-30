/*
 * CMS module API layer (WO-55 Banners, WO-56 Newsletter, WO-105 Search Page Content).
 *
 * Mirrors the catalog/users style: the authenticated `api` instance, `qsSerializer`
 * for query strings, and `unwrap` on every response. Enums (banner displayLocation,
 * newsletter status) travel as their string names.
 */
import { api, unwrap, qsSerializer } from 'services/api'

// ---- Banners (WO-55) --------------------------------------------------------
export const bannerApi = {
  // GET /api/admin/banners -> PaginatedList<BannerDto> ({ items, totalCount, ... }).
  list (params = {}) {
    return api
      .get('/api/admin/banners', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  get (id) {
    return api.get(`/api/admin/banners/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post('/api/admin/banners', payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`/api/admin/banners/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`/api/admin/banners/${id}`).then(unwrap)
  }
}

// Known placement slots. Offered as suggestions but the field is free text — an
// operator can type any custom location key (the select runs in combobox mode).
export const bannerLocationOptions = [
  { label: 'Home — hero carousel', value: 'home-hero' },
  { label: 'Home — double banner', value: 'home-double' },
  { label: 'Category page', value: 'category' },
  { label: 'Search — no results', value: 'search-no-results' }
]

export function bannerLocationLabel (value) {
  const match = bannerLocationOptions.find((o) => o.value === value)
  return match ? match.label : value || '—'
}

// ---- Newsletter subscribers (WO-56) -----------------------------------------
export const newsletterApi = {
  // GET /api/admin/newsletter/subscribers -> PaginatedList<SubscriberDto>.
  listSubscribers (params = {}) {
    return api
      .get('/api/admin/newsletter/subscribers', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  // GET .../export.csv — returns the raw blob response so the caller can trigger a download.
  exportSubscribersCsv (params = {}) {
    return api.get('/api/admin/newsletter/subscribers/export.csv', {
      params,
      paramsSerializer: qsSerializer,
      responseType: 'blob'
    })
  }
}

export const newsletterStatusOptions = [
  { label: 'Pending', value: 'Pending' },
  { label: 'Subscribed', value: 'Subscribed' },
  { label: 'Unsubscribed', value: 'Unsubscribed' }
]

// Badge colour per subscription status.
export function newsletterStatusColor (status) {
  switch (status) {
    case 'Subscribed': return 'positive'
    case 'Pending': return 'orange'
    case 'Unsubscribed': return 'grey'
    default: return 'grey'
  }
}

// ---- Search page content (WO-105, singleton) --------------------------------
export const searchContentApi = {
  // GET /api/admin/search-page-content -> the single content record.
  get () {
    return api.get('/api/admin/search-page-content').then(unwrap)
  },
  // PUT /api/admin/search-page-content — replace the singleton.
  update (payload) {
    return api.put('/api/admin/search-page-content', payload).then(unwrap)
  }
}

// ---- Product collections (read-only, for the search-content selector) -------
export const productCollectionApi = {
  // GET /api/admin/product-collections -> rows { id, name }.
  list (params = {}) {
    return api
      .get('/api/admin/product-collections', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  }
}

export default {
  bannerApi,
  bannerLocationOptions,
  bannerLocationLabel,
  newsletterApi,
  newsletterStatusOptions,
  newsletterStatusColor,
  searchContentApi,
  productCollectionApi
}
