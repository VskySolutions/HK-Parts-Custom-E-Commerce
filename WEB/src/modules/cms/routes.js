/*
 * CMS module routes (WO-55 Banners, WO-56 Newsletter, WO-105 Search Page Content).
 * The banner "new" route must precede the ":id" route so /cms/banners/new isn't
 * captured as an id.
 */
export default [
  {
    path: 'cms/banners',
    name: 'cms-banners',
    meta: { title: 'Banners', permissions: ['Cms.Read'] },
    component: () => import('modules/cms/pages/banners.vue')
  },
  {
    // Create: the same detail page in "create mode" (must precede the :id route).
    path: 'cms/banners/new',
    name: 'cms-banner-new',
    meta: { title: 'New banner', permissions: ['Cms.Write'] },
    component: () => import('modules/cms/pages/banner-detail.vue')
  },
  {
    path: 'cms/banners/:id',
    name: 'cms-banner-detail',
    meta: { title: 'Banner', permissions: ['Cms.Read'] },
    component: () => import('modules/cms/pages/banner-detail.vue')
  },
  {
    path: 'cms/newsletter',
    name: 'cms-newsletter',
    meta: { title: 'Newsletter subscribers', permissions: ['Cms.Read'] },
    component: () => import('modules/cms/pages/newsletter.vue')
  },
  {
    path: 'cms/search-content',
    name: 'cms-search-content',
    meta: { title: 'Search page content', permissions: ['Cms.Read'] },
    component: () => import('modules/cms/pages/search-content.vue')
  }
]
