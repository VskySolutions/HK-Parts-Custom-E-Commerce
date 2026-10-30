/*
 * Storefront CMS module API layer (WO-54).
 *
 * Wraps the PUBLIC storefront CMS endpoints (/api/storefront/cms/*): single
 * pages, the footer/nav grouping, and the blog. Uses the ANONYMOUS axios
 * instance (`anonApi`) — these routes are [AllowAnonymous] and must not trigger
 * the authenticated instance's 401 refresh interceptor.
 *
 * Re-exports sanitizeCmsHtml (WO-101/105) so the page/blog viewers clean their
 * rich `body` HTML with the SAME sanitiser the rest of the storefront uses for
 * admin-authored content — a single source of truth, surfaced here for
 * module-local ergonomics.
 */
import { anonApi, unwrap, qsSerializer } from 'services/api'
import { sanitizeCmsHtml } from 'modules/storefront/catalog-cms-api'

const CMS = '/api/storefront/cms'

export const cmsApi = {
  // A single published CMS page by slug → { title, body (HTML), metaTitle, metaDescription }.
  // Returns 404 when the page is missing or unpublished.
  page (slug) {
    return anonApi.get(CMS + '/pages/' + encodeURIComponent(slug)).then(unwrap)
  },
  // Published pages grouped for footer/nav → [{ groupName, groupSlug, pages: [{ title, slug }] }].
  navigation () {
    return anonApi.get(CMS + '/navigation').then(unwrap)
  },
  // Published pages flagged "show in top bar" → [{ title, slug }] for the nav-bar quick links.
  headerNavigation () {
    return anonApi.get(CMS + '/header-navigation').then(unwrap)
  },
  // Paged blog listing → { items: [{ title, slug, summary, author, publishedOnUtc, featuredImageUrl }], totalCount }.
  blog (params = {}) {
    return anonApi
      .get(CMS + '/blog', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  // A single published blog post by slug → { title, body (HTML), author, publishedOnUtc, featuredImageUrl, tags }.
  blogPost (slug) {
    return anonApi.get(CMS + '/blog/' + encodeURIComponent(slug)).then(unwrap)
  }
}

export { sanitizeCmsHtml }

export default { cmsApi, sanitizeCmsHtml }
