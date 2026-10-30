import { api, unwrap } from 'services/api'

const SEO = '/api/admin/seo'

// SEO admin: robots.txt editor + sitemap status/refresh (WO-57). Schema markup is automatic (no config).
export const seoApi = {
  getSettings () { return api.get(`${SEO}/settings`).then(unwrap) },
  updateRobots (content) { return api.put(`${SEO}/robots`, { content }).then(unwrap) },
  sitemapStatus () { return api.get(`${SEO}/sitemap/status`).then(unwrap) },
  refreshSitemap () { return api.post(`${SEO}/sitemap/refresh`).then(unwrap) }
}
