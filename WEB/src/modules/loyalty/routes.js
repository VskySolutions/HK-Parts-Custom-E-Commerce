/*
 * Loyalty module routes (WO-27). Child of the authenticated shell; the parent
 * registers this array in router/index.js. Loyalty config is a keyed singleton,
 * so this is a single settings page (no list/detail).
 */
export default [
  {
    path: 'loyalty',
    name: 'loyalty',
    meta: { title: 'Loyalty Points', permissions: ['Catalog.Read'] },
    component: () => import('modules/loyalty/pages/index.vue')
  }
]
