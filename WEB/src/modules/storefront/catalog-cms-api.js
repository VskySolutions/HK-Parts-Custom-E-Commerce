/*
 * Storefront catalog CMS content API (WO-101 / WO-105).
 *
 * Wraps the PUBLIC storefront content endpoints that back the configurable
 * category landing page (WO-101) and the search results page (WO-105). Uses the
 * ANONYMOUS axios instance (`anonApi`) — these routes are [AllowAnonymous] and
 * must not trigger the authenticated instance's 401 refresh interceptor.
 *
 *   GET /api/storefront/category-config/{categoryId}
 *     -> { bannerImageUrl, promotionalDescription (HTML),
 *          pinnedProducts: [StorefrontProductSummaryDto],
 *          ymalCollectionId, ymalProducts: [StorefrontProductSummaryDto] }
 *        Nulls / empty arrays when the category is unconfigured — callers then
 *        render the plain grid.
 *
 *   GET /api/storefront/search-content
 *     -> { heading, placeholderText, resultsCountLabel, noResultsMessage (HTML),
 *          noResultsBanner: { title, subtitle, imageUrl, linkUrl, ctaLabel } | null,
 *          noResultsProducts: [StorefrontProductSummaryDto] }
 *
 * Both calls resolve to `null`/defaults on failure so the pages degrade to their
 * pre-WO-101/105 behaviour when the content endpoints are unavailable.
 */
import { anonApi, unwrap } from 'services/api'

const BASE = '/api/storefront'

export const catalogCmsApi = {
  // Dynamic elements for a category landing page (banner, promo copy, pinned + YMAL products).
  categoryConfig (categoryId) {
    return anonApi.get(BASE + '/category-config/' + encodeURIComponent(categoryId)).then(unwrap)
  },
  // Configurable copy + no-results promotional content for the search results page.
  searchContent () {
    return anonApi.get(BASE + '/search-content').then(unwrap)
  }
}

/*
 * Minimal sanitiser for admin-authored CMS HTML rendered via v-html (promotional
 * descriptions, no-results message). Not a full sanitiser — it strips the obvious
 * script-injection vectors (script/style/iframe/etc. nodes, inline on* event
 * handlers and javascript: URLs). The content is authored by trusted admins; this
 * is defence-in-depth, consistent with the storefront rich-text convention.
 */
export function sanitizeCmsHtml (html) {
  if (!html || typeof html !== 'string') return ''

  // Browser path: parse into an inert document, then strip dangerous nodes/attrs.
  if (typeof window !== 'undefined' && window.DOMParser) {
    try {
      const doc = new DOMParser().parseFromString(html, 'text/html')
      doc.querySelectorAll('script, style, iframe, object, embed, link, meta, form').forEach((el) => el.remove())
      doc.querySelectorAll('*').forEach((el) => {
        for (const attr of Array.from(el.attributes)) {
          const name = attr.name.toLowerCase()
          const value = (attr.value || '').replace(/\s+/g, '').toLowerCase()
          const isUrlAttr = name === 'href' || name === 'src' || name === 'xlink:href'
          if (name.startsWith('on') || (isUrlAttr && value.startsWith('javascript:'))) {
            el.removeAttribute(attr.name)
          }
        }
      })
      return doc.body ? doc.body.innerHTML : ''
    } catch (e) {
      /* fall through to the regex fallback */
    }
  }

  // No-DOM fallback: strip script/style-ish blocks, inline handlers and js: URLs.
  return html
    .replace(/<\/?(script|style|iframe|object|embed|link|meta|form)[^>]*>/gi, '')
    .replace(/\son\w+\s*=\s*("[^"]*"|'[^']*'|[^\s>]+)/gi, '')
    .replace(/javascript:/gi, '')
}

export default {
  catalogCmsApi,
  sanitizeCmsHtml
}
