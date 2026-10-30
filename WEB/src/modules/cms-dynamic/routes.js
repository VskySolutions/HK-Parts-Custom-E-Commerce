/*
 * CMS dynamic-content routes (WO-96 Home sections, WO-97 Product collections,
 * WO-98 Featured products/categories, WO-99 Category page config). Lives in its own module so it
 * never touches the sibling `cms` (banners/newsletter/search) or `cms-content` (pages/blog)
 * modules. URLs share the /cms/* prefix but every route name is unique. The collection "new" route
 * must precede the ":id" route so /cms/collections/new isn't captured as an id.
 */
export default [
  {
    path: 'cms/home-sections',
    name: 'cms-home-sections',
    meta: { title: 'Home Page Sections', permissions: ['Cms.Read'] },
    component: () => import('modules/cms-dynamic/pages/home-sections.vue')
  },
  {
    path: 'cms/collections',
    name: 'cms-collections',
    meta: { title: 'Product Collections', permissions: ['Cms.Read'] },
    component: () => import('modules/cms-dynamic/pages/collections.vue')
  },
  {
    // Create: the same detail page in "create mode" (must precede the :id route).
    path: 'cms/collections/new',
    name: 'cms-collection-new',
    meta: { title: 'New collection', permissions: ['Cms.Write'] },
    component: () => import('modules/cms-dynamic/pages/collection-detail.vue')
  },
  {
    path: 'cms/collections/:id',
    name: 'cms-collection-detail',
    meta: { title: 'Collection', permissions: ['Cms.Read'] },
    component: () => import('modules/cms-dynamic/pages/collection-detail.vue')
  },
  {
    path: 'cms/featured',
    name: 'cms-featured',
    meta: { title: 'Featured', permissions: ['Cms.Read'] },
    component: () => import('modules/cms-dynamic/pages/featured.vue')
  },
  {
    path: 'cms/category-config',
    name: 'cms-category-config',
    meta: { title: 'Category Page Config', permissions: ['Cms.Read'] },
    component: () => import('modules/cms-dynamic/pages/category-config.vue')
  }
]
