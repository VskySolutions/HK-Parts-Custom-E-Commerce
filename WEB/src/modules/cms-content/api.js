/*
 * CMS content admin module API (WO-54): CMS Pages, Page Groups, and Blog Posts.
 *
 * Lives in its own module folder because the existing `modules/cms` folder is owned by
 * the Banners/Newsletter/Search-content work (WO-55/56/105). Mirrors the catalog/users
 * style: the authenticated `api` instance, `qsSerializer` for query strings, and `unwrap`
 * on every response. Updates echo the id in the body to match the backend
 * `command with { Id = id }` convention. Status enums travel as their string names.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const PAGES = '/api/admin/cms-pages'
const GROUPS = '/api/admin/cms-page-groups'
const POSTS = '/api/admin/blog-posts'

// ---- CMS Pages --------------------------------------------------------------
export const cmsPageApi = {
  // GET /api/admin/cms-pages -> PaginatedList<CmsPageDto> ({ items, totalCount, ... }).
  list (params = {}) { return api.get(PAGES, { params, paramsSerializer: qsSerializer }).then(unwrap) },
  get (id) { return api.get(`${PAGES}/${id}`).then(unwrap) },
  create (payload) { return api.post(PAGES, payload).then(unwrap) },
  update (id, payload) { return api.put(`${PAGES}/${id}`, { ...payload, id }).then(unwrap) },
  remove (id) { return api.delete(`${PAGES}/${id}`).then(unwrap) },
  // Quick status transition (Draft | Published | Archived) without a full update.
  setStatus (id, status) { return api.put(`${PAGES}/${id}/status`, { status }).then(unwrap) }
}

// ---- CMS Page Groups --------------------------------------------------------
export const cmsPageGroupApi = {
  // GET /api/admin/cms-page-groups -> a bare array OR an { items } envelope; callers normalise both.
  list (params = {}) { return api.get(GROUPS, { params, paramsSerializer: qsSerializer }).then(unwrap) },
  get (id) { return api.get(`${GROUPS}/${id}`).then(unwrap) },
  create (payload) { return api.post(GROUPS, payload).then(unwrap) },
  update (id, payload) { return api.put(`${GROUPS}/${id}`, { ...payload, id }).then(unwrap) },
  remove (id) { return api.delete(`${GROUPS}/${id}`).then(unwrap) }
}

// ---- Blog Posts -------------------------------------------------------------
export const blogPostApi = {
  // GET /api/admin/blog-posts -> PaginatedList<BlogPostDto>.
  list (params = {}) { return api.get(POSTS, { params, paramsSerializer: qsSerializer }).then(unwrap) },
  get (id) { return api.get(`${POSTS}/${id}`).then(unwrap) },
  create (payload) { return api.post(POSTS, payload).then(unwrap) },
  update (id, payload) { return api.put(`${POSTS}/${id}`, { ...payload, id }).then(unwrap) },
  remove (id) { return api.delete(`${POSTS}/${id}`).then(unwrap) },
  setStatus (id, status) { return api.put(`${POSTS}/${id}/status`, { status }).then(unwrap) }
}

// Publish state shared by CMS pages and blog posts (mirrors the backend enum).
export const statusOptions = [
  { label: 'Draft', value: 'Draft' },
  { label: 'Published', value: 'Published' },
  { label: 'Archived', value: 'Archived' }
]

// Same set with an "All" sentinel, for list filters.
export const statusFilterOptions = [
  { label: 'All', value: null },
  ...statusOptions
]

// Badge colour per publish state, for list tables + detail headers.
export function statusColor (status) {
  switch (status) {
    case 'Published': return 'positive'
    case 'Archived': return 'orange'
    case 'Draft':
    default: return 'grey'
  }
}

export default { cmsPageApi, cmsPageGroupApi, blogPostApi, statusOptions, statusFilterOptions, statusColor }
