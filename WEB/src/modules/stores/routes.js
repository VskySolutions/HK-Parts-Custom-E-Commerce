/* Stores module routes (WO-94 Step 12). */
export default [
  {
    path: 'stores',
    name: 'stores',
    meta: { title: 'Stores', permissions: ['Stores.Read'] },
    component: () => import('modules/stores/pages/index.vue')
  },
  {
    // Create: the same detail page in "create mode" (must precede the :id route).
    path: 'stores/new',
    name: 'store-new',
    meta: { title: 'New store', permissions: ['Stores.Write'] },
    component: () => import('modules/stores/pages/store-detail.vue')
  },
  {
    path: 'stores/:id',
    name: 'store-detail',
    meta: { title: 'Store', permissions: ['Stores.Read'] },
    component: () => import('modules/stores/pages/store-detail.vue')
  }
]
