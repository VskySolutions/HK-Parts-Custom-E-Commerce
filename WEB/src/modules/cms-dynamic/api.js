/*
 * CMS dynamic-content admin API layer:
 *   WO-96 Home page sections, WO-97 Product collections,
 *   WO-98 Featured products/categories, WO-99 Category page config.
 *
 * Lives in its own module folder (the sibling `modules/cms-content` folder is owned by the CMS
 * Pages/Blog work, WO-54). Mirrors the catalog/cms style: the authenticated `api` instance,
 * `qsSerializer` for query strings, and `unwrap` on every response. There is no drag-and-drop
 * library in the app, so every ordering surface persists by PUTting the FULL ordered id list to a
 * `/reorder` endpoint. Enum-ish values (sectionType, productRowSource, rule) travel as strings.
 */
import { api, unwrap, qsSerializer } from 'services/api'

// ---- Home page sections (WO-96) ---------------------------------------------
const HOME_SECTIONS = '/api/admin/home-sections'
export const homeSectionApi = {
  // GET (ordered by displayOrder) -> [{ id, sectionType, displayName, displayOrder, isEnabled, config }].
  list () { return api.get(HOME_SECTIONS).then(unwrap) },
  get (id) { return api.get(`${HOME_SECTIONS}/${id}`).then(unwrap) },
  create (payload) { return api.post(HOME_SECTIONS, payload).then(unwrap) },
  update (id, payload) { return api.put(`${HOME_SECTIONS}/${id}`, { ...payload, id }).then(unwrap) },
  remove (id) { return api.delete(`${HOME_SECTIONS}/${id}`).then(unwrap) },
  // Persist the whole ordered set of section ids.
  reorder (orderedIds) { return api.put(`${HOME_SECTIONS}/reorder`, { orderedIds }).then(unwrap) },
  // Toggle a single section on/off without a full update.
  setEnabled (id, enabled) { return api.put(`${HOME_SECTIONS}/${id}/enabled`, { enabled }).then(unwrap) }
}

// ---- Product collections (WO-97) --------------------------------------------
const COLLECTIONS = '/api/admin/product-collections'
export const collectionApi = {
  // Paged: { page, pageSize, search, isEnabled? } -> rows { id, name, productCount, updatedOnUtc }.
  list (params = {}) { return api.get(COLLECTIONS, { params, paramsSerializer: qsSerializer }).then(unwrap) },
  // GET /{id} -> { id, name, description, slug, isEnabled, items:[{ productId, productName, sku, imageUrl, displayOrder }] }.
  get (id) { return api.get(`${COLLECTIONS}/${id}`).then(unwrap) },
  create (payload) { return api.post(COLLECTIONS, payload).then(unwrap) },
  update (id, payload) { return api.put(`${COLLECTIONS}/${id}`, { ...payload, id }).then(unwrap) },
  remove (id) { return api.delete(`${COLLECTIONS}/${id}`).then(unwrap) },
  // Items are a sub-resource saved explicitly (add / remove / reorder).
  addItem (id, productId) { return api.post(`${COLLECTIONS}/${id}/items`, { productId }).then(unwrap) },
  removeItem (id, productId) { return api.delete(`${COLLECTIONS}/${id}/items/${productId}`).then(unwrap) },
  reorderItems (id, orderedProductIds) { return api.put(`${COLLECTIONS}/${id}/items/reorder`, { orderedProductIds }).then(unwrap) }
}

// ---- Featured products & categories (WO-98) ---------------------------------
const FEATURED = '/api/admin/featured'
export const featuredApi = {
  // GET -> [{ id, name, sku, price, imageUrl, featuredDisplayOrder, isPublished }].
  listProducts () { return api.get(`${FEATURED}/products`).then(unwrap) },
  // PUT body { isFeatured, featuredDisplayOrder? } — flip a product's featured flag / order.
  setProduct (productId, payload) { return api.put(`${FEATURED}/products/${productId}`, payload).then(unwrap) },
  reorderProducts (orderedProductIds) { return api.put(`${FEATURED}/products/reorder`, { orderedProductIds }).then(unwrap) },
  // GET -> [{ id, name, slug, imageUrl, displayOrder }] (featured categories only).
  listCategories () { return api.get(`${FEATURED}/categories`).then(unwrap) },
  // PUT body { isFeatured } — add/remove a category from the featured set.
  setCategory (categoryId, isFeatured) { return api.put(`${FEATURED}/categories/${categoryId}`, { isFeatured }).then(unwrap) }
}

// ---- Category page config (WO-99, keyed by categoryId) ----------------------
const CATEGORY_CONFIGS = '/api/admin/category-page-configs'
export const categoryPageConfigApi = {
  // GET -> { categoryId, bannerMediaId, bannerImageUrl?, promotionalDescription, ymalCollectionId, pinnedProducts:[...] }
  // (returns an empty/default record if none is saved yet).
  get (categoryId) { return api.get(`${CATEGORY_CONFIGS}/${categoryId}`).then(unwrap) },
  // PUT body { bannerMediaId, promotionalDescription, ymalCollectionId, pinnedProductIds:[...] }.
  update (categoryId, payload) { return api.put(`${CATEGORY_CONFIGS}/${categoryId}`, payload).then(unwrap) },
  remove (categoryId) { return api.delete(`${CATEGORY_CONFIGS}/${categoryId}`).then(unwrap) }
}

// ---- Shared option catalogs -------------------------------------------------
// The five home-section types. `icon` doubles as the "Add section" menu icon and the row badge glyph.
export const sectionTypeOptions = [
  { label: 'Hero banner', value: 'HeroBanner', icon: 'o_view_carousel' },
  { label: 'Featured categories', value: 'FeaturedCategories', icon: 'o_category' },
  { label: 'Product row', value: 'ProductRow', icon: 'o_view_week' },
  { label: 'Blog posts row', value: 'BlogPostsRow', icon: 'o_article' },
  { label: 'Custom HTML block', value: 'CustomHtmlBlock', icon: 'o_code' }
]

export function sectionTypeMeta (value) {
  return sectionTypeOptions.find((o) => o.value === value) ||
    { label: value || '—', value, icon: 'o_dashboard_customize' }
}

// ProductRow: where the row's products come from.
export const productRowSourceOptions = [
  { label: 'From a collection', value: 'Collection' },
  { label: 'Automatic rule', value: 'Auto' }
]

// ProductRow (Auto source): the rule that fills the row.
export const autoRuleOptions = [
  { label: 'New arrivals', value: 'NewArrivals' },
  { label: 'Best sellers', value: 'BestSellers' },
  { label: 'On sale', value: 'OnSale' },
  { label: 'Featured', value: 'Featured' }
]

export function autoRuleLabel (value) {
  const match = autoRuleOptions.find((o) => o.value === value)
  return match ? match.label : (value || '—')
}

// Section types whose config carries a maxItems (the "row" style sections).
export const MAX_ITEMS_TYPES = ['FeaturedCategories', 'ProductRow', 'BlogPostsRow']

export default {
  homeSectionApi,
  collectionApi,
  featuredApi,
  categoryPageConfigApi,
  sectionTypeOptions,
  sectionTypeMeta,
  productRowSourceOptions,
  autoRuleOptions,
  autoRuleLabel,
  MAX_ITEMS_TYPES
}
