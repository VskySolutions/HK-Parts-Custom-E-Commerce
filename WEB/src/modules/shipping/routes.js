/* Shipping admin module routes (WO-116). */
export default [
  {
    path: 'shipping',
    name: 'admin-shipping',
    meta: { title: 'Shipping', permissions: ['Stores.Read'] },
    component: () => import('modules/shipping/pages/index.vue')
  },
  {
    path: 'shipping/methods/new',
    name: 'shipping-method-new',
    meta: { title: 'New shipping method', permissions: ['Stores.Write'] },
    component: () => import('modules/shipping/pages/shipping-method-detail.vue')
  },
  {
    path: 'shipping/methods/:id',
    name: 'shipping-method-detail',
    meta: { title: 'Shipping method', permissions: ['Stores.Read'] },
    component: () => import('modules/shipping/pages/shipping-method-detail.vue')
  },
  {
    path: 'shipping/zones/new',
    name: 'shipping-zone-new',
    meta: { title: 'New shipping zone', permissions: ['Stores.Write'] },
    component: () => import('modules/shipping/pages/shipping-zone-detail.vue')
  },
  {
    path: 'shipping/zones/:id',
    name: 'shipping-zone-detail',
    meta: { title: 'Shipping zone', permissions: ['Stores.Read'] },
    component: () => import('modules/shipping/pages/shipping-zone-detail.vue')
  }
]
