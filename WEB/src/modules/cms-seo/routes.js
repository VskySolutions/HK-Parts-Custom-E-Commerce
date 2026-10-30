/* CMS SEO admin module routes (WO-57). Grouped under the CMS menu (key cms.seo). */
export default [
  {
    path: 'cms/seo',
    name: 'admin-cms-seo',
    meta: { title: 'SEO', permissions: ['Cms.Read'] },
    component: () => import('modules/cms-seo/pages/index.vue')
  }
]
