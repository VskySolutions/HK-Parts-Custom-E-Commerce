/*
 * CMS content admin routes (WO-54): Pages (list + routed create/edit auto-save detail),
 * Page Groups (list + inline drawer create/edit), and Blog Posts (list + routed
 * create/edit detail). The `.../new` create route MUST precede the `.../:id` route.
 * Gated on Cms.Read (reads) / Cms.Write (create/edit). URLs live under /cms/* alongside
 * the Banners/Newsletter CMS pages; the components live in this module to avoid touching
 * the existing modules/cms files.
 */
export default [
  {
    path: 'cms/pages',
    name: 'cms-pages',
    meta: { title: 'Pages', permissions: ['Cms.Read'] },
    component: () => import('modules/cms-content/pages/pages-list.vue')
  },
  {
    // Create: the same detail page in "create mode" (must precede the :id route).
    path: 'cms/pages/new',
    name: 'cms-page-new',
    meta: { title: 'New page', permissions: ['Cms.Write'] },
    component: () => import('modules/cms-content/pages/page-detail.vue')
  },
  {
    path: 'cms/pages/:id',
    name: 'cms-page-detail',
    meta: { title: 'Page', permissions: ['Cms.Read'] },
    component: () => import('modules/cms-content/pages/page-detail.vue')
  },
  {
    path: 'cms/page-groups',
    name: 'cms-page-groups',
    meta: { title: 'Page Groups', permissions: ['Cms.Read'] },
    component: () => import('modules/cms-content/pages/page-groups.vue')
  },
  {
    path: 'cms/blog',
    name: 'cms-blog',
    meta: { title: 'Blog', permissions: ['Cms.Read'] },
    component: () => import('modules/cms-content/pages/blog-list.vue')
  },
  {
    // Create: the same detail page in "create mode" (must precede the :id route).
    path: 'cms/blog/new',
    name: 'cms-blog-new',
    meta: { title: 'New blog post', permissions: ['Cms.Write'] },
    component: () => import('modules/cms-content/pages/blog-detail.vue')
  },
  {
    path: 'cms/blog/:id',
    name: 'cms-blog-detail',
    meta: { title: 'Blog post', permissions: ['Cms.Read'] },
    component: () => import('modules/cms-content/pages/blog-detail.vue')
  }
]
