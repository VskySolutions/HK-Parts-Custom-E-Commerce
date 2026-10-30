/*
 * Product Q&A module routes (WO-58). Child of the authenticated shell; the admin
 * question-moderation queue lives under the CMS area at /cms/product-qa.
 */
export default [
  {
    path: 'cms/product-qa',
    name: 'cms-product-qa',
    meta: { title: 'Product Q&A', permissions: ['Cms.Read'] },
    component: () => import('modules/product-qa/pages/index.vue')
  }
]
