/* Price Rules admin routes (Catalog → Price Rules). */
export default [
  {
    path: 'catalog/price-rules',
    name: 'catalog-price-rules',
    meta: { title: 'Price Rules', permissions: ['Catalog.Read'] },
    component: () => import('modules/price-rules/pages/index.vue')
  },
  {
    // Create: same form page in create mode (must precede the :id route).
    path: 'catalog/price-rules/new',
    name: 'catalog-price-rule-new',
    meta: { title: 'New price rule', permissions: ['Catalog.Write'] },
    component: () => import('modules/price-rules/pages/detail.vue')
  },
  {
    path: 'catalog/price-rules/:id',
    name: 'catalog-price-rule-detail',
    meta: { title: 'Price rule', permissions: ['Catalog.Read'] },
    component: () => import('modules/price-rules/pages/detail.vue')
  }
]
