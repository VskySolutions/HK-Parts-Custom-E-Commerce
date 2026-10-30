/*
 * Product Reviews module routes (WO-14). Child of the authenticated shell; the
 * admin moderation queue lives under the Catalog area at /catalog/reviews.
 */
export default [
  {
    path: 'catalog/reviews',
    name: 'catalog-reviews',
    meta: { title: 'Product Reviews', permissions: ['Catalog.Read'] },
    component: () => import('modules/reviews/pages/index.vue')
  }
]
