/*
 * Storefront home page API layer (WO-100).
 *
 * Wraps the PUBLIC dynamic home-page endpoint that returns the enabled Home Page
 * Sections (CMSHomePageSection) in display order. Uses the ANONYMOUS axios
 * instance (`anonApi`) — the storefront home is [AllowAnonymous] and must not
 * trigger the authenticated instance's 401 refresh interceptor.
 *
 * Response shape (camelCase, enums as string names):
 *   { sections: [ {
 *       id, type, displayName, config,
 *       banners?, categories?, products?, posts?, html?
 *   } ] }
 * where type is one of:
 *   HeroBanner | FeaturedCategories | ProductRow | BlogPostsRow | CustomHtmlBlock
 */
import { anonApi, unwrap } from 'services/api'

const HOME = '/api/storefront/home'

export const homeApi = {
  // Enabled home-page sections in display order (with each section's resolved payload).
  get () {
    return anonApi.get(HOME).then(unwrap)
  }
}

export default { homeApi }
