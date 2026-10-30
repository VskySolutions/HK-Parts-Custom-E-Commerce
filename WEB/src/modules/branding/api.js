/*
 * Branding resource (WO-9 REQ-TEN-001).
 *
 * Reads/updates the deployment's singleton branding row and uploads logo /
 * favicon assets. Mirrors the widgetApi template in services/api.js: every
 * method resolves to the unwrapped payload via `.then(unwrap)`.
 *
 *   GET    /api/tenant/branding          -> BrandingDto
 *   PUT    /api/tenant/branding          -> BrandingDto (UpdateBrandingCommand body)
 *   POST   /api/tenant/branding/logo     -> BrandingDto (multipart "file")
 *   POST   /api/tenant/branding/favicon  -> BrandingDto (multipart "file")
 */
import { api, unwrap } from 'services/api'

export const brandingApi = {
  get () {
    return api.get('/api/tenant/branding').then(unwrap)
  },
  update (payload) {
    return api.put('/api/tenant/branding', payload).then(unwrap)
  },
  uploadLogo (file) {
    const data = new FormData()
    data.append('file', file)
    return api.post('/api/tenant/branding/logo', data).then(unwrap)
  },
  uploadFavicon (file) {
    const data = new FormData()
    data.append('file', file)
    return api.post('/api/tenant/branding/favicon', data).then(unwrap)
  }
}

export default brandingApi
